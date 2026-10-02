using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Решает, что делает манекен.
/// Два слоя:
///  • Намерение (Intent) — что он знает об игроке: Idle (не знает, ждёт), Chase (видит и гонится),
///    Investigate (потерял — идёт в последнюю точку), Search (дошёл — ищет вокруг).
///  • Моторика (State) — правило взгляда: пока игрок смотрит, манекен стоит, в каком бы намерении ни был.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(MannequinVisibility))]
[RequireComponent(typeof(MannequinAnimator))]
public class MannequinBrain : MonoBehaviour
{
    public enum State { Frozen, Moving, Settling, Attacking }
    public enum Intent { Idle, Chase, Investigate, Search }

    [Tooltip("Пусто = игрок со скриптом GrayboxPlayerController")]
    [SerializeField] private Transform target;

    [Header("Движение")]
    [SerializeField] private float chaseSpeed = 4f;
    [Tooltip("Скорость, когда он ищет тебя вокруг последней точки")]
    [SerializeField] private float searchSpeed = 2.2f;
    [Tooltip("Как часто пересчитывать путь (сек)")]
    [SerializeField] private float repathInterval = 0.1f;

    [Header("Зрение манекена")]
    [Tooltip("Высота глаз манекена над полом")]
    [SerializeField] private float eyeHeight = 1.6f;
    [Tooltip("Дальше этой дистанции манекен тебя не замечает")]
    [SerializeField] private float sightDistance = 40f;
    [Tooltip("Сколько секунд после потери из виду он ещё «видит», куда ты свернул")]
    [SerializeField] private float trackAfterLost = 0.4f;
    [Tooltip("На какой высоте проверять тело игрока. Чем ниже, тем легче спрятаться за низким ящиком")]
    [SerializeField] private float playerBodyHeight = 0.9f;
    [Tooltip("Видит ли манекен твою голову над низкими преградами. Выключено = за ящиком по пояс ты уже спрятан")]
    [SerializeField] private bool seePlayerHead = false;
    [Tooltip("Если ты смотришь на манекена, он знает, где ты")]
    [SerializeField] private bool knowsWhenPlayerLooks = true;
    [Tooltip("Что закрывает манекену обзор. По умолчанию всё, кроме Ignore Raycast")]
    [SerializeField] private LayerMask sightBlockers = Physics.DefaultRaycastLayers;

    [Header("Поиск")]
    [Tooltip("Сколько секунд ищет вокруг последней точки, прежде чем сдаться и замереть")]
    [SerializeField] private float searchDuration = 6f;
    [Tooltip("Радиус поиска вокруг последней точки")]
    [SerializeField] private float searchRadius = 5f;
    [Tooltip("Сколько стоит на каждой точке поиска, «осматриваясь»")]
    [SerializeField] private float lookAroundTime = 0.8f;

    [Header("Замирание")]
    [Tooltip("Сколько длится «дотягивание» позы, когда игрок посмотрел")]
    [SerializeField] private float settleDuration = 0.25f;
    [Tooltip("Сколько манекен продолжает скользить вперёд во время дотягивания (0 = стоп мгновенно, 1 = полная скорость)")]
    [Range(0f, 1f)] [SerializeField] private float settleSlide = 0.5f;

    [Header("Атака")]
    [SerializeField] private float attackRange = 1.3f;
    [SerializeField] private float attackCooldown = 1f;

    [Header("Отладка")]
    [SerializeField] private bool showStateLabel = true;

    public State CurrentState { get; private set; } = State.Frozen;
    public Intent CurrentIntent { get; private set; } = Intent.Idle;

    private NavMeshAgent agent;
    private MannequinVisibility visibility;
    private MannequinAnimator anim;
    private Camera playerCamera;
    private GrayboxPlayerController playerController;
    private readonly RaycastHit[] hits = new RaycastHit[16];

    private float stateTimer, repathTimer, lastAttackEnd = -999f;
    private Vector3 lastKnownPosition;
    private float lastSawPlayerTime = -999f;
    private bool seesPlayer;

    private Vector3 searchPoint;
    private bool hasSearchPoint;
    private float searchEndTime, lookAroundUntil;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        visibility = GetComponent<MannequinVisibility>();
        anim = GetComponent<MannequinAnimator>();
        playerCamera = Camera.main;

        if (target == null)
        {
            var player = FindAnyObjectByType<GrayboxPlayerController>();
            if (player != null) target = player.transform;
            else if (playerCamera != null) target = playerCamera.transform;
        }
        if (target != null) playerController = target.GetComponent<GrayboxPlayerController>();
    }

    private void Start()
    {
        agent.speed = chaseSpeed;
        agent.stoppingDistance = attackRange * 0.8f;
        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            agent.Warp(hit.position);
        StopAgent();
    }

    private void Update()
    {
        if (target == null || !agent.isOnNavMesh) return;

        UpdateAwareness();
        bool seen = visibility.IsSeen;
        stateTimer += Time.deltaTime;

        switch (CurrentState)
        {
            case State.Frozen:
                // Идёт, только если его не видят И ему есть куда идти
                if (!seen && CurrentIntent != Intent.Idle) EnterMoving();
                break;

            case State.Moving:
                if (seen) { EnterSettling(false, true); break; }
                if (CurrentIntent == Intent.Idle) { EnterSettling(false, false); break; } // не нашёл — замирает и ждёт
                Steer();
                if (CurrentIntent == Intent.Chase && InAttackRange() && Time.time - lastAttackEnd > attackCooldown)
                    EnterAttacking();
                break;

            case State.Settling:
                // Небольшое скольжение по инерции — игрок успевает заметить движение
                if (!agent.isStopped)
                {
                    float k = 1f - Mathf.Clamp01(stateTimer / settleDuration);
                    agent.speed = Mathf.Max(0.01f, chaseSpeed * settleSlide * k);
                    agent.velocity = Vector3.ClampMagnitude(agent.velocity, agent.speed);
                }
                if (stateTimer >= settleDuration) EnterFrozen();
                break;

            case State.Attacking:
                if (seen) { EnterSettling(true, true); break; } // замирает прямо в ударе
                FaceTarget();
                if (stateTimer >= anim.AttackLength)
                {
                    lastAttackEnd = Time.time;
                    EnterMoving();
                }
                break;
        }
    }

    // ---------- Что манекен знает об игроке ----------

    private void UpdateAwareness()
    {
        seesPlayer = CanSeePlayer();
        if (seesPlayer)
        {
            lastKnownPosition = target.position;
            lastSawPlayerTime = Time.time;
            CurrentIntent = Intent.Chase;
            return;
        }

        if (CurrentIntent == Intent.Chase)
        {
            // Первые доли секунды после потери ещё «видит», куда ты свернул за угол
            if (Time.time - lastSawPlayerTime <= trackAfterLost) lastKnownPosition = target.position;
            else CurrentIntent = Intent.Investigate;
        }
    }

    private bool CanSeePlayer()
    {
        // Если игрок видит манекена — манекен видит игрока
        if (knowsWhenPlayerLooks && visibility.IsSeenRaw) return true;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        if ((target.position - transform.position).sqrMagnitude > sightDistance * sightDistance) return false;

        float bodyHeight = playerController != null
            ? Mathf.Lerp(playerBodyHeight, Mathf.Min(playerBodyHeight, 0.45f), playerController.CrouchAmount)
            : playerBodyHeight;
        if (HasClearLine(eye, target.position + Vector3.up * bodyHeight)) return true; // тело игрока
        if (seePlayerHead && playerCamera != null && HasClearLine(eye, playerCamera.transform.position)) return true; // голова
        return false;
    }

    private bool HasClearLine(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.01f) return true;

        int count = Physics.RaycastNonAlloc(from, dir / dist, hits, dist - 0.05f, sightBlockers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform t = hits[i].collider.transform;
            if (t.IsChildOf(transform) || t.IsChildOf(target)) continue; // свои коллайдеры и сам игрок не мешают
            return false;
        }
        return true;
    }

    // ---------- Куда идти ----------

    private void Steer()
    {
        Vector3 goal = lastKnownPosition;
        agent.speed = CurrentIntent == Intent.Search ? searchSpeed : chaseSpeed;

        if (CurrentIntent == Intent.Investigate && Reached())
            StartSearch();

        if (CurrentIntent == Intent.Search)
        {
            if (Time.time >= searchEndTime)
            {
                CurrentIntent = Intent.Idle; // сдался
                return;
            }

            if (!hasSearchPoint) PickSearchPoint();
            else if (Reached())
            {
                // Постоял, «осмотрелся» — к следующей точке
                if (lookAroundUntil <= 0f) lookAroundUntil = Time.time + lookAroundTime;
                else if (Time.time >= lookAroundUntil) PickSearchPoint();
            }
            goal = searchPoint;
        }

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;
            agent.SetDestination(goal);
        }
        anim.SetMoveSpeed(agent.velocity.magnitude);
    }

    private void StartSearch()
    {
        CurrentIntent = Intent.Search;
        searchEndTime = Time.time + searchDuration;
        hasSearchPoint = false;
    }

    private void PickSearchPoint()
    {
        hasSearchPoint = true;
        lookAroundUntil = 0f;
        repathTimer = 0f;
        for (int i = 0; i < 8; i++)
        {
            Vector2 r = Random.insideUnitCircle * searchRadius;
            Vector3 p = lastKnownPosition + new Vector3(r.x, 0f, r.y);
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                searchPoint = hit.position;
                return;
            }
        }
        searchPoint = lastKnownPosition;
    }

    private bool Reached()
    {
        if (agent.pathPending) return false;
        return agent.remainingDistance <= agent.stoppingDistance + 0.2f;
    }

    // ---------- Переходы моторики ----------

    private void EnterMoving()
    {
        CurrentState = State.Moving;
        stateTimer = 0f;
        agent.isStopped = false;
        agent.speed = chaseSpeed;
        agent.updateRotation = true;
        repathTimer = 0f;
        anim.StartMoving();
    }

    private void EnterSettling(bool keepCurrentPose, bool lookAtPlayer)
    {
        CurrentState = State.Settling;
        stateTimer = 0f;
        if (settleSlide <= 0f) StopAgent();
        anim.BeginSettle(settleDuration, lookAtPlayer ? LookTarget() : null, keepCurrentPose);
    }

    private void EnterFrozen()
    {
        CurrentState = State.Frozen;
        stateTimer = 0f;
        StopAgent();
        anim.Freeze();
    }

    private void EnterAttacking()
    {
        CurrentState = State.Attacking;
        stateTimer = 0f;
        StopAgent();
        agent.updateRotation = false;
        anim.PlayAttack();
        // Урона и смерти пока нет — просто удар
    }

    // ---------- Помощники ----------

    private bool InAttackRange()
    {
        Vector3 d = target.position - transform.position;
        d.y = 0f;
        return d.magnitude <= attackRange;
    }

    private void FaceTarget()
    {
        Vector3 d = target.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation,
            Quaternion.LookRotation(d), 540f * Time.deltaTime);
    }

    private Transform LookTarget()
    {
        Camera cam = visibility.ObserverCamera;
        return cam != null ? cam.transform : target;
    }

    private void StopAgent()
    {
        if (!agent.isOnNavMesh) return;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showStateLabel || !Application.isPlaying) return;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.1f, $"{CurrentState} / {CurrentIntent}");

        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (target != null)
        {
            // Линия взгляда манекена: зелёная — видит тебя, серая — нет
            Gizmos.color = seesPlayer ? Color.green : new Color(0.5f, 0.5f, 0.5f, 0.4f);
            Gizmos.DrawLine(transform.position + Vector3.up * eyeHeight, target.position + Vector3.up * playerBodyHeight);
        }
        if (CurrentIntent != Intent.Idle)
        {
            Gizmos.color = Color.yellow; // последняя известная точка
            Gizmos.DrawWireSphere(lastKnownPosition, 0.3f);
        }
        if (CurrentIntent == Intent.Search && hasSearchPoint)
        {
            Gizmos.color = Color.cyan; // текущая точка поиска
            Gizmos.DrawWireSphere(searchPoint, 0.25f);
        }
    }
#endif
}
