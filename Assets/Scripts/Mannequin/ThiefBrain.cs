using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Воришка — маленький манекен, который не убивает, а крадёт одежду.
///  • Правило взгляда как у сталкера: пока на него смотрят — стоит, отвернулись — бежит.
///  • Сначала тащит к себе в гнездо вещи, лежащие на уровне. В гнезде их снова можно подобрать.
///  • Если на уровне брать нечего — подкрадывается к игроку и выхватывает одну собранную вещь.
///  • Если он замер с вещью, а игрок подошёл вплотную — роняет её под ноги.
/// Здесь только решения. Позы, бег и замирание делает MannequinAnimator, как у сталкера.
/// В онлайне мозг работает только на сервере (NetworkThief): клиентам уходят позиция, анимация
/// (NetworkMannequinAnimation) и какую вещь он несёт (CarriedChanged → ShowCarried у клиентов).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(MannequinVisibility))]
[RequireComponent(typeof(MannequinAnimator))]
public class ThiefBrain : MonoBehaviour
{
    public enum State { Frozen, Moving, Settling, Grabbing }
    public enum Goal { None, Item, Player, Nest }

    private const string StolenReason = "Stolen by a thief";
    private const float NestArriveDistance = 0.8f;
    private static bool quitting;

    [Header("Гнездо")]
    [Tooltip("Куда он сносит добычу. Пусто = объект ThiefNest в сцене, а если его нет — место, где он стоял на старте")]
    [SerializeField] private Transform nest;
    [Tooltip("Как широко он раскидывает вещи в гнезде (м)")]
    [SerializeField] private float nestDropRadius = 1f;
    [SerializeField] private Transform[] dropOffPoints = new Transform[0];

    [Header("Бег")]
    [SerializeField] private float runSpeed = 5.5f;
    [Tooltip("Масштаб тела (его ставит Build Thief Prefab). Скорость для анимации делится на него, чтобы короткие ноги перебирали чаще")]
    [SerializeField] private float bodyScale = 0.6f;
    [Tooltip("Во сколько раз быстрее проигрывать бег, чтобы ноги меньше скользили (передаётся в MannequinAnimator)")]
    [SerializeField] private float runAnimationSpeed = 1.4f;
    [Tooltip("Как часто пересчитывать путь (сек)")]
    [SerializeField] private float repathInterval = 0.15f;
    [Tooltip("Как часто заново выбирать цель (сек)")]
    [SerializeField] private float thinkInterval = 0.3f;

    [Header("Кража")]
    [Tooltip("С какого расстояния хватает вещь, лежащую на уровне")]
    [SerializeField] private float grabReach = 1f;
    [Tooltip("С какого расстояния выхватывает вещь у игрока")]
    [SerializeField] private float stealReach = 0.9f;
    [Tooltip("В какой момент движения руки вещь оказывается у него (доля длины клипа)")]
    [Range(0f, 1f)] [SerializeField] private float grabMoment = 0.45f;
    [Tooltip("Самое долгое «хватание» (сек): длинный клип обрезается")]
    [SerializeField] private float maxGrabTime = 1f;
    [Tooltip("Сколько секунд после кражи у игрока он не трогает игроков")]
    [SerializeField] private float playerStealCooldown = 20f;
    [Tooltip("Игроков дальше этого он не трогает")]
    [SerializeField] private float playerRange = 30f;
    [Tooltip("Если он замер с вещью, а игрок подошёл ближе этого — роняет вещь под ноги")]
    [SerializeField] private float dropWhenCornered = 1.4f;

    [Header("Где он держит вещь")]
    [SerializeField] private Transform handSocket;
    [SerializeField] private Transform headSocket;
    [Tooltip("Размер вещи в руке (м)")]
    [SerializeField] private float carriedSize = 0.4f;
    [Tooltip("Размер украденной шапки на голове (м)")]
    [SerializeField] private float hatSize = 0.24f;

    [Header("Замирание")]
    [Tooltip("Сколько длится «дотягивание» позы, когда на него посмотрели")]
    [SerializeField] private float settleDuration = 0.2f;

    [Header("Отладка")]
    [SerializeField] private bool showStateLabel = true;

    public State CurrentState { get; private set; } = State.Frozen;
    public Goal CurrentGoal { get; private set; } = Goal.None;
    /// <summary>Вещь, которую он сейчас несёт (null — руки пустые).</summary>
    public ClothingPickup Carried => carried;
    /// <summary>Взял вещь или положил (null). Для сети: сервер сообщает клиентам, что показать в руках.</summary>
    public event System.Action<ClothingDefinition> CarriedChanged;

    private NavMeshAgent agent;
    private MannequinVisibility visibility;
    private MannequinAnimator anim;

    private Vector3 nestPosition;
    private Vector3 deliveryPosition;
    private int previousDropOff = -1;
    private NavMeshPath deliveryPath;
    private Vector3 goalPoint;
    private ClothingPickup targetItem;
    private PlayerAvatar targetPlayer;
    private ClothingPickup carried;
    private GameObject carriedVisual;
    private readonly HashSet<ClothingPickup> stash = new HashSet<ClothingPickup>(); // что уже лежит в гнезде
    private readonly List<PlayerAvatar> roomPlayers = new List<PlayerAvatar>();

    private float stateTimer, repathTimer, thinkTimer, grabTime, nextPlayerSteal;
    private bool grabDone;

    private void Awake()
    {
        deliveryPath = new NavMeshPath();
        agent = GetComponent<NavMeshAgent>();
        visibility = GetComponent<MannequinVisibility>();
        anim = GetComponent<MannequinAnimator>();
        // Awake работает и у выключенного мозга (на клиентах), так что бег у всех проигрывается одинаково
        anim.MoveAnimationSpeed = runAnimationSpeed;
    }

    private void Start()
    {
        if (nest == null)
        {
            GameObject found = GameObject.Find("ThiefNest");
            if (found != null) nest = found.transform;
        }
        nestPosition = nest != null ? nest.position : transform.position;
        if (NavMesh.SamplePosition(nestPosition, out NavMeshHit nestHit, 3f, NavMesh.AllAreas))
            nestPosition = nestHit.position;
        deliveryPosition = nestPosition;

        agent.speed = runSpeed;
        agent.stoppingDistance = 0.2f;
        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            agent.Warp(hit.position);
        StopAgent();
    }

    private void Update()
    {
        if (!agent.isOnNavMesh) return;

        bool seen = visibility.IsSeen;
        stateTimer += Time.deltaTime;
        thinkTimer -= Time.deltaTime;

        if (seen && carried != null) DropIfCornered();

        switch (CurrentState)
        {
            case State.Frozen:
                // Бежит, только если на него не смотрят и есть зачем
                if (!seen && Think() != Goal.None) EnterMoving();
                break;

            case State.Moving:
                if (seen) { EnterSettling(); break; }
                if (Think() == Goal.None) { EnterSettling(); break; } // делать нечего — встаёт и ждёт
                Steer();
                break;

            case State.Settling:
                if (stateTimer >= settleDuration) EnterFrozen();
                break;

            case State.Grabbing:
                if (seen) { EnterSettling(); break; } // поймали на краже — замирает, вещь не взял
                FaceGoal();
                if (!grabDone && stateTimer >= grabTime * grabMoment)
                {
                    grabDone = true;
                    CompleteGrab();
                }
                if (stateTimer >= grabTime)
                {
                    if (Think(true) != Goal.None) EnterMoving();
                    else EnterSettling();
                }
                break;
        }
    }

    // ---------- Куда бежать ----------

    private Goal Think(bool force = false)
    {
        if (!force && thinkTimer > 0f && IsGoalValid()) return CurrentGoal;
        thinkTimer = thinkInterval;
        ChooseGoal();
        return CurrentGoal;
    }

    private bool IsGoalValid()
    {
        switch (CurrentGoal)
        {
            case Goal.Item: return carried == null && targetItem != null && targetItem.IsOnFloor;
            case Goal.Player: return carried == null && targetPlayer != null && targetPlayer.isActiveAndEnabled;
            default: return true;
        }
    }

    private void ChooseGoal()
    {
        targetItem = null;
        targetPlayer = null;

        // 1) Несёт добычу — в гнездо
        if (carried != null) { SetGoal(Goal.Nest, deliveryPosition); return; }

        // 2) На уровне лежит вещь — за ней
        if (FindLooseItem(out ClothingPickup item, out Vector3 itemPoint))
        {
            targetItem = item;
            SetGoal(Goal.Item, itemPoint);
            return;
        }

        // 3) Брать нечего — к игроку, у которого есть что украсть
        if (Time.time >= nextPlayerSteal)
        {
            PlayerAvatar victim = FindVictim();
            if (victim != null)
            {
                targetPlayer = victim;
                SetGoal(Goal.Player, victim.Position);
                return;
            }
        }

        // 4) Ничего — домой, ждать
        if (FlatDistance(nestPosition) > NestArriveDistance) SetGoal(Goal.Nest, nestPosition);
        else SetGoal(Goal.None, transform.position);
    }

    private void SetGoal(Goal goal, Vector3 point)
    {
        if (goal != CurrentGoal || (point - goalPoint).sqrMagnitude > 0.25f) repathTimer = 0f;
        CurrentGoal = goal;
        goalPoint = point;
    }

    private bool FindLooseItem(out ClothingPickup best, out Vector3 bestPoint)
    {
        best = null;
        bestPoint = Vector3.zero;
        float bestSqr = float.PositiveInfinity;
        foreach (ClothingPickup pickup in ClothingPickup.All)
        {
            if (pickup == null || !pickup.IsOnFloor || !pickup.CanBeStolen || stash.Contains(pickup)) continue;
            if (pickup.gameObject.scene != gameObject.scene) continue; // вещи только своей комнаты
            float sqr = FlatSqr(pickup.transform.position);
            if (sqr >= bestSqr) continue;
            // Точка на полу под вещью. Нет NavMesh рядом — не дотянется, пропускаем
            if (!NavMesh.SamplePosition(pickup.transform.position, out NavMeshHit hit, pickup.HoverHeight + 1.5f, NavMesh.AllAreas))
                continue;
            best = pickup;
            bestPoint = hit.position;
            bestSqr = sqr;
        }
        return best != null;
    }

    private PlayerAvatar FindVictim()
    {
        PlayerAvatar best = null;
        float bestSqr = playerRange * playerRange;
        PlayerRegistry.GetPlayers(gameObject.scene, roomPlayers); // игроки только своей комнаты
        foreach (PlayerAvatar player in roomPlayers)
        {
            if (player == null || player.Outfit == null || player.Outfit.IsEmpty) continue;
            float sqr = FlatSqr(player.Position);
            if (sqr < bestSqr) { best = player; bestSqr = sqr; }
        }
        return best;
    }

    private void Steer()
    {
        if (CurrentGoal == Goal.Player) goalPoint = targetPlayer.Position; // игрок ходит — догоняем

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;
            agent.SetDestination(goalPoint);
        }

        agent.speed = runSpeed;
        anim.SetMoveSpeed(agent.velocity.magnitude / Mathf.Max(0.1f, bodyScale));

        switch (CurrentGoal)
        {
            case Goal.Item:
                if (FlatDistance(targetItem.transform.position) <= grabReach) EnterGrabbing();
                break;
            case Goal.Player:
                if (FlatDistance(targetPlayer.Position) <= stealReach) EnterGrabbing();
                break;
            case Goal.Nest:
                if (FlatDistance(goalPoint) <= NestArriveDistance) ArriveAtNest();
                break;
        }
    }

    private void ArriveAtNest()
    {
        if (carried != null) DropCarried(true);
        Think(true);
    }

    // ---------- Кража ----------

    private void CompleteGrab()
    {
        if (CurrentGoal == Goal.Item)
        {
            if (targetItem != null && targetItem.IsOnFloor &&
                FlatDistance(targetItem.transform.position) <= grabReach * 1.5f)
            {
                targetItem.TakeAway(); // прячем с уровня; в комплект игроку не попадает
                Carry(targetItem);
            }
        }
        else if (CurrentGoal == Goal.Player)
        {
            if (targetPlayer != null && targetPlayer.Outfit != null && FlatDistance(targetPlayer.Position) <= stealReach * 1.6f)
            {
                var candidates = new List<ClothingPickup>();
                foreach (ClothingDefinition clothing in targetPlayer.Outfit.Items)
                {
                    var pickup = ClothingPickup.FindPickedUp(clothing, gameObject.scene);
                    if (pickup != null) candidates.Add(pickup);
                }
                if (candidates.Count == 0) { nextPlayerSteal = Time.time + 2; return; }
                ClothingPickup selected = candidates[Random.Range(0, candidates.Count)];
                if (targetPlayer.Outfit.Remove(selected.Clothing.Slot, StolenReason, out _))
                {
                    nextPlayerSteal = Time.time + playerStealCooldown;
                    Carry(selected);
                }
            }
        }
    }

    private void Carry(ClothingPickup pickup)
    {
        carried = pickup;
        pickup.ClaimByThief();
        ChooseDelivery();
        stash.Remove(pickup);
        ShowCarried(pickup.Clothing);
        CarriedChanged?.Invoke(pickup.Clothing);
    }

    private void ChooseDelivery()
    {
        deliveryPosition = nestPosition;
        if (dropOffPoints == null || dropOffPoints.Length == 0) return;
        int first = Random.Range(0, dropOffPoints.Length);
        for (int n = 0; n < dropOffPoints.Length; n++)
        {
            int index = (first + n) % dropOffPoints.Length;
            Transform point = dropOffPoints[index];
            if (point == null || index == previousDropOff || (point.position - transform.position).sqrMagnitude < 16) continue;
            if (!NavMesh.SamplePosition(point.position, out NavMeshHit hit, 1.5f, agent.areaMask)) continue;
            if (!agent.CalculatePath(hit.position, deliveryPath) || deliveryPath.status != NavMeshPathStatus.PathComplete) continue;
            deliveryPosition = hit.position; previousDropOff = index; return;
        }
    }

    /// <summary>Показать вещь в руке (шапку — на голове). null — убрать. Клиенты в онлайне вызывают это по сигналу сервера.</summary>
    public void ShowCarried(ClothingDefinition clothing)
    {
        if (carriedVisual != null) Destroy(carriedVisual);
        carriedVisual = clothing != null ? CreateCarriedVisual(clothing) : null;
    }

    private void DropIfCornered()
    {
        if (PlayerRegistry.Nearest(transform.position, gameObject.scene, dropWhenCornered) != null) DropCarried(false);
    }

    /// <summary>Положить вещь: в гнездо (россыпью вокруг) или прямо перед собой.</summary>
    private void DropCarried(bool intoNest)
    {
        if (carried == null) return;
        ClothingPickup item = carried;
        carried = null;
        ShowCarried(null);
        CarriedChanged?.Invoke(null);

        Vector3 point;
        if (intoNest)
        {
            Vector2 r = Random.insideUnitCircle * nestDropRadius;
            point = deliveryPosition + new Vector3(r.x, 0f, r.y);
        }
        else point = transform.position + transform.forward * 0.5f;

        if (NavMesh.SamplePosition(point, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) point = hit.position;
        else point = intoNest ? deliveryPosition : transform.position;
        item.PlaceAt(point);
        item.ProtectFromThieves(30);

        if (intoNest) stash.Add(item);
        else stash.Remove(item);
    }

    /// <summary>Копия модели вещи у него в руке (или шапка на голове).</summary>
    private GameObject CreateCarriedVisual(ClothingDefinition clothing)
    {
        if (clothing == null || clothing.Model == null) return null;
        bool onHead = clothing.Slot == ClothingSlot.Head && headSocket != null;
        Transform socket = onHead ? headSocket : handSocket != null ? handSocket : transform;

        var holder = new GameObject("Stolen: " + clothing.DisplayName);
        holder.transform.SetParent(socket, false);
        GameObject model = Instantiate(clothing.Model, holder.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(clothing.DisplayRotation);
        model.transform.localScale = Vector3.one;

        foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        ClothingVisuals.Prepare(model, clothing.FabricMaterial, 0);
        if (renderers.Length == 0) return holder;

        // Размер — в метрах мира: сокет сидит на уменьшенных костях, поэтому меряем по настоящим границам
        Bounds b = WorldBounds(renderers);
        float biggest = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (biggest > 0.0001f) holder.transform.localScale *= (onHead ? hatSize : carriedSize) / biggest;
        b = WorldBounds(renderers);
        Vector3 target = onHead ? socket.position + socket.up * (b.extents.y * 0.4f) : socket.position;
        holder.transform.position += target - b.center;
        return holder;
    }

    // ---------- Переходы ----------

    private void EnterMoving()
    {
        CurrentState = State.Moving;
        stateTimer = 0f;
        agent.isStopped = false;
        agent.updateRotation = true;
        agent.speed = runSpeed;
        repathTimer = 0f;
        anim.StartMoving();
    }

    private void EnterSettling()
    {
        CurrentState = State.Settling;
        stateTimer = 0f;
        StopAgent();
        agent.updateRotation = true;
        anim.BeginSettle(settleDuration, null);
    }

    private void EnterFrozen()
    {
        CurrentState = State.Frozen;
        stateTimer = 0f;
        StopAgent();
        anim.Freeze();
    }

    private void EnterGrabbing()
    {
        CurrentState = State.Grabbing;
        stateTimer = 0f;
        grabDone = false;
        grabTime = Mathf.Clamp(anim.AttackLength, 0.3f, Mathf.Max(0.3f, maxGrabTime));
        StopAgent();
        agent.updateRotation = false;
        anim.PlayAttack(); // клип «атаки» у Воришки — это движение рукой за вещью
    }

    // ---------- Помощники ----------

    private void FaceGoal()
    {
        Vector3 point = goalPoint;
        if (CurrentGoal == Goal.Item && targetItem != null) point = targetItem.transform.position;
        else if (CurrentGoal == Goal.Player && targetPlayer != null) point = targetPlayer.Position;

        Vector3 d = point - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 720f * Time.deltaTime);
    }

    private void StopAgent()
    {
        if (!agent.isOnNavMesh) return;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }

    private float FlatSqr(Vector3 point)
    {
        Vector3 d = point - transform.position;
        d.y = 0f;
        return d.sqrMagnitude;
    }

    private float FlatDistance(Vector3 point) => Mathf.Sqrt(FlatSqr(point));

    private static Bounds WorldBounds(Renderer[] renderers)
    {
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private void OnDisable()
    {
        // Воришку выключили или удалили с вещью в руках — вещь не должна пропасть с уровня
        if (carried != null && !quitting && gameObject.scene.isLoaded) DropCarried(false);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        quitting = false;
        Application.quitting -= MarkQuitting;
        Application.quitting += MarkQuitting;
    }

    private static void MarkQuitting() => quitting = true;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Vector3 nestPoint = Application.isPlaying ? nestPosition : nest != null ? nest.position : transform.position;
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(nestPoint, nestDropRadius); // гнездо

        if (!Application.isPlaying || !showStateLabel) return;
        string label = $"{CurrentState} / {CurrentGoal}";
        if (carried != null && carried.Clothing != null) label += "\ncarrying: " + carried.Clothing.DisplayName;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.4f, label);

        if (CurrentGoal != Goal.None)
        {
            Gizmos.color = Color.yellow; // куда бежит
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, goalPoint + Vector3.up * 0.5f);
        }
    }
#endif
}
