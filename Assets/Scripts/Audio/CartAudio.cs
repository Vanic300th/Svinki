using UnityEngine;

/// <summary>
/// Звуки тележки: качение (зацикленный звук, громкость и тон зависят от скорости)
/// и скрип колёс поверх него раз в несколько секунд.
/// Стоит на дочернем объекте "Audio" префаба тележки (Assets/Prefabs/Shopping Cart).
/// Сеть не трогает: скорость берётся из ShoppingCart.Speed — у хоста из физики, у остальных игроков
/// из движения тележки, которое уже синхронизируется. Поэтому тележку слышат все.
/// </summary>
[DisallowMultipleComponent, DefaultExecutionOrder(180)]
public sealed class CartAudio : MonoBehaviour
{
    [Header("Источники звука (дочерние объекты)")]
    [Tooltip("Зацикленное качение. Клип, громкость и тон выставляет этот скрипт.")]
    [SerializeField] private AudioSource rolling;
    [Tooltip("Разовые звуки: скрип колёс.")]
    [SerializeField] private AudioSource oneShots;

    [Header("Качение")]
    [SerializeField] private AudioClip rollingLoop;
    [Tooltip("С какой скорости (м/с) тележку становится слышно.")]
    [SerializeField, Min(0f)] private float minSpeed = 0.4f;
    [Tooltip("На какой скорости (м/с) качение звучит в полную силу. Обычный толчок — 4.5 м/с, с бегом — 8.")]
    [SerializeField, Min(0.1f)] private float fullSpeed = 8f;
    [Tooltip("Громкость: X — на минимальной скорости, Y — на полной.")]
    [SerializeField] private Vector2 volume = new Vector2(0.15f, 0.6f);
    [Tooltip("Высота тона: X — медленно, Y — быстро.")]
    [SerializeField] private Vector2 pitch = new Vector2(0.85f, 1.15f);
    [Tooltip("Как быстро звук догоняет скорость тележки. Больше — резче.")]
    [SerializeField, Min(0.1f)] private float response = 6f;

    [Header("Скрип колёс")]
    [SerializeField] private SoundEffect squeak;
    [Tooltip("Пауза между скрипами, с: случайно от X до Y.")]
    [SerializeField] private Vector2 squeakInterval = new Vector2(2f, 3.5f);
    [Tooltip("С какой скорости (м/с) колёса скрипят.")]
    [SerializeField, Min(0f)] private float squeakMinSpeed = 1f;

    [Header("Отладка")]
    [SerializeField] private bool logEvents;
    public int SqueakCount { get; private set; }
    /// <summary>Сглаженная скорость, по которой считается звук (для проверки).</summary>
    public float AudibleSpeed => speed;

    // Тележку переставляют на новое место в начале раунда и при падении под карту:
    // у остальных игроков это выглядит как огромная скорость на пару кадров. Такое не озвучиваем.
    private const float TeleportSpeed = 15f;

    private ShoppingCart cart;
    private float speed, nextSqueak, quietUntil;
    private bool wasMoving;

    private void Awake()
    {
        cart = GetComponentInParent<ShoppingCart>();
        if (rolling != null) { rolling.playOnAwake = false; rolling.loop = true; }
    }

    private void OnEnable()
    {
        speed = 0f; wasMoving = false;
        quietUntil = Time.time + 1f; // старт сцены и подключение к игре: тележки встают на свои места
        if (rolling != null) { rolling.volume = 0f; rolling.Stop(); }
    }

    private void OnDisable()
    {
        if (rolling != null) rolling.Stop();
    }

    private void LateUpdate()
    {
        float now = Time.time;
        float target = cart != null ? cart.Speed : 0f;
        if (!float.IsFinite(target) || target > TeleportSpeed) { quietUntil = now + 0.5f; speed = 0f; }
        if (now < quietUntil) target = 0f;
        speed = Mathf.Lerp(speed, target, 1f - Mathf.Exp(-Time.deltaTime * response));

        float t = Mathf.InverseLerp(minSpeed, fullSpeed, speed);
        UpdateRolling(t);
        UpdateSqueak(now, t);
    }

    private void UpdateRolling(float t)
    {
        if (rolling == null || rollingLoop == null) return;
        if (speed < minSpeed * 0.5f)
        {
            if (rolling.isPlaying) rolling.Stop();
            return;
        }
        // Плавно входим от нуля на половине минимальной скорости, чтобы звук не включался щелчком.
        float fadeIn = Mathf.InverseLerp(minSpeed * 0.5f, minSpeed, speed);
        rolling.volume = Mathf.Lerp(volume.x, volume.y, t) * fadeIn;
        rolling.pitch = Mathf.Lerp(pitch.x, pitch.y, t);
        if (!rolling.isPlaying)
        {
            rolling.clip = rollingLoop;
            rolling.time = Random.Range(0f, rollingLoop.length); // разные тележки не звучат одинаково
            rolling.Play();
        }
    }

    private void UpdateSqueak(float now, float t)
    {
        bool moving = speed >= squeakMinSpeed;
        // Первый скрип — не сразу после старта, а через случайную паузу.
        if (moving && !wasMoving) nextSqueak = now + Random.Range(0.4f, Mathf.Max(0.4f, squeakInterval.x));
        wasMoving = moving;
        if (!moving || now < nextSqueak) return;
        nextSqueak = now + RandomInterval();
        if (squeak != null && squeak.Play(oneShots, Mathf.Lerp(0.6f, 1f, t)))
        {
            SqueakCount++;
            if (logEvents) Debug.Log($"[CartAudio] squeak ({(cart != null ? cart.name : name)}, {speed:0.0} м/с)", this);
        }
    }

    private float RandomInterval() =>
        Random.Range(Mathf.Min(squeakInterval.x, squeakInterval.y), Mathf.Max(squeakInterval.x, squeakInterval.y));

    private void OnValidate()
    {
        volume.x = Mathf.Clamp01(volume.x); volume.y = Mathf.Clamp01(volume.y);
        pitch.x = Mathf.Clamp(pitch.x, 0.1f, 3f); pitch.y = Mathf.Clamp(pitch.y, 0.1f, 3f);
        squeakInterval.x = Mathf.Max(0.2f, squeakInterval.x); squeakInterval.y = Mathf.Max(0.2f, squeakInterval.y);
    }
}
