using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// HUD поверх игры (префаб Assets/Prefabs/UI/GameHUD): прицел, подсказка, фонарик, уведомления.
/// Объекты собраны в префабе руками; скрипты игры только меняют текст и включают/выключают их.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameHud : MonoBehaviour
{
    [Header("Crosshair")]
    [SerializeField] private GameObject crosshair;
    [Header("Prompt under the crosshair")]
    [SerializeField] private GameObject prompt;
    [SerializeField] private TMP_Text promptText;
    [Header("Flashlight status")]
    [SerializeField] private GameObject flashlight;
    [SerializeField] private TMP_Text flashlightText;
    [SerializeField] private string flashlightOn = "Flashlight [F]: ON";
    [SerializeField] private string flashlightOff = "Flashlight [F]: OFF";
    [Header("Notification at the top")]
    [SerializeField] private GameObject notification;
    [SerializeField] private TMP_Text notificationText;

    private static readonly List<GameHud> active = new List<GameHud>();
    private float notificationUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active.Clear();

    /// <summary>HUD из той же сцены, что и объект; если такого нет — любой включённый.</summary>
    public static GameHud Find(Component near)
    {
        GameHud any = null;
        foreach (GameHud hud in active)
        {
            if (hud == null) continue;
            if (near != null && hud.gameObject.scene == near.gameObject.scene) return hud;
            if (any == null) any = hud;
        }
        return any;
    }

    private void Awake()
    {
        // В префабе всё включено, чтобы было видно при редактировании; в игре показываем по запросу.
        Show(crosshair, false);
        Show(prompt, false);
        Show(flashlight, false);
        Show(notification, false);
    }

    private void OnEnable() { if (!active.Contains(this)) active.Add(this); }
    private void OnDisable() => active.Remove(this);

    private void Update()
    {
        if (notification != null && notification.activeSelf && Time.time > notificationUntil)
            notification.SetActive(false);
    }

    public void SetCrosshair(bool visible) => Show(crosshair, visible);

    /// <summary>Подсказка под прицелом. Пустая строка или null — спрятать.</summary>
    public void ShowPrompt(string text)
    {
        if (string.IsNullOrEmpty(text)) { Show(prompt, false); return; }
        SetText(promptText, text);
        Show(prompt, true);
    }

    public void HidePrompt() => Show(prompt, false);

    public void SetFlashlight(bool visible, bool isOn)
    {
        if (visible) SetText(flashlightText, isOn ? flashlightOn : flashlightOff);
        Show(flashlight, visible);
    }

    /// <summary>Сообщение сверху экрана на несколько секунд («Thief stole: …»).</summary>
    public void ShowNotification(string text, float seconds)
    {
        SetText(notificationText, text);
        notificationUntil = Time.time + seconds;
        Show(notification, true);
    }

    private static void SetText(TMP_Text label, string text)
    {
        if (label != null && label.text != text) label.text = text;
    }

    private static void Show(GameObject target, bool visible)
    {
        if (target != null && target.activeSelf != visible) target.SetActive(visible);
    }
}
