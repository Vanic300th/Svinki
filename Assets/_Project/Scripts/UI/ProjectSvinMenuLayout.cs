using TMPro;
using UnityEngine;

/// <summary>Persistent prefab references. Layout, fonts, individual images and text remain editable.</summary>
public sealed class ProjectSvinMenuLayout : MonoBehaviour
{
    public GameObject menuPanel, appearancePanel, background;
    public TMP_Text projectTitle, gameTitle, status, roster, appearanceTitle;
    public TMP_InputField nickname, joinCode;
    public UnityEngine.UI.Button[] mainButtons;
    public UnityEngine.UI.Button continueButton;
    public RectTransform sessionActions, appearanceChoices, previewRect;
    public UnityEngine.UI.Button rotateLeft, rotateRight;
    public UnityEngine.UI.Button[] appearancePresets;
    public UnityEngine.UI.Button settingsClose;
    public UnityEngine.UI.Button actionTemplate, appearanceChoiceTemplate;
    public UnityEngine.UI.Button playButton, playBackButton;
    public GameObject playOptionsPanel;
    public UnityEngine.UI.Button customizeButton, appearanceClose;
    public GameObject appearanceOverlay;
    public UnityEngine.UI.Button russianLanguageButton, englishLanguageButton;
    private void LateUpdate()
    {
        if (appearanceOverlay == null || appearancePanel == null) return;
        var available = ((RectTransform)transform).rect.size;
        var size = appearancePanel.GetComponent<RectTransform>().rect.size;
        float fit = Mathf.Min(1, (available.x - 64) / Mathf.Max(1, size.x), (available.y - 64) / Mathf.Max(1, size.y));
        appearancePanel.transform.localScale = Vector3.one * Mathf.Max(.25f, fit);
    }
}
