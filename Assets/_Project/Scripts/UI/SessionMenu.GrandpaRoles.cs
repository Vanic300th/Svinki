using System;
using TMPro;
using UnityEngine;

public sealed partial class SessionMenu
{
    private GameObject grandpaRoleMenu;
    private RectTransform roleChoices, classChoices;
    private TMP_Text grandpaHeading, grandpaTimer, grandpaDetails;
    private UnityEngine.UI.Button choosePig, chooseHunter, confirmClass;
    private TMP_Text pigChoiceLabel, hunterChoiceLabel;
    private TMP_Text classHeading, classRole, defaultClassName, defaultClassStatus, classTimer, confirmClassLabel;
    private readonly TMP_Text[] lockedClassNames = new TMP_Text[2];
    private readonly TMP_Text[] lockedClassHints = new TMP_Text[2];

    private void BuildGrandpaRoleMenu(Transform canvas)
    {
        var backdrop = Rect("Grandpa role selection", canvas, 0, 0, 0, 0);
        backdrop.anchorMin = Vector2.zero;
        backdrop.anchorMax = Vector2.one;
        backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
        var background = backdrop.gameObject.AddComponent<UnityEngine.UI.Image>();
        background.color = new Color(.09f, .055f, .08f);
        grandpaRoleMenu = backdrop.gameObject;

        roleChoices = SelectionWindow("Role choices", backdrop, 390);
        grandpaHeading = CenterLabel(roleChoices, 0, 0, 900, 80, 49);
        grandpaTimer = CenterLabel(roleChoices, 0, 100, 900, 42, 26);
        (choosePig, pigChoiceLabel) = RoleButton(roleChoices, "Play as Pig", 30, 180, 405,
            () => lobby.VolunteerForGrandpa(false));
        (chooseHunter, hunterChoiceLabel) = RoleButton(roleChoices, "Volunteer for Grandpa", 465, 180, 405,
            () => lobby.VolunteerForGrandpa(true));
        grandpaDetails = CenterLabel(roleChoices, 30, 278, 840, 72, 24);

        classChoices = SelectionWindow("Grandpa class selection", backdrop, 476);
        classHeading = CenterLabel(classChoices, 0, 0, 900, 76, 48);
        classRole = CenterLabel(classChoices, 0, 87, 900, 40, 25);
        (defaultClassName, defaultClassStatus) = ClassCard("Default class", 30, false);
        for (int i = 0; i < lockedClassNames.Length; i++)
            (lockedClassNames[i], lockedClassHints[i]) = ClassCard("Locked class " + (i + 1), 315 + i * 285, true);
        classTimer = CenterLabel(classChoices, 0, 312, 900, 38, 23);
        (confirmClass, confirmClassLabel) = RoleButton(classChoices, "Confirm Grandpa class", 230, 369, 440,
            lobby.ConfirmGrandpaClass);
        grandpaRoleMenu.SetActive(false);
    }

    private RectTransform SelectionWindow(string name, Transform parent, float height)
    {
        var window = Rect(name, parent, 0, 0, 900, height);
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(.5f, .5f);
        window.anchoredPosition = Vector2.zero;
        return window;
    }

    private (TMP_Text name, TMP_Text status) ClassCard(string name, float x, bool locked)
    {
        var card = Rect(name, classChoices, x, 157, 270, 136);
        var image = card.gameObject.AddComponent<UnityEngine.UI.Image>();
        ProjectSvinSkin.Bind(image, locked ? ProjectSvinRole.Track : ProjectSvinRole.Button);
        image.raycastTarget = false;

        var title = CenterLabel(card, 9, 26, 252, 47, 24);
        var status = CenterLabel(card, 9, 75, 252, 35, 18);
        if (locked)
        {
            ProjectSvinSkin.Bind(title, ProjectSvinRole.MutedText);
            ProjectSvinSkin.Bind(status, ProjectSvinRole.MutedText);
        }
        return (title, status);
    }

    private bool UpdateGrandpaRoleMenu()
    {
        if (grandpaRoleMenu == null) return false;
        bool choosingRole = lobby.ChoosingGrandpaRole;
        bool choosingClass = lobby.ChoosingGrandpaClass;
        bool visible = choosingRole || choosingClass;
        bool panelChanged = choosingRole != roleChoices.gameObject.activeSelf ||
            choosingClass != classChoices.gameObject.activeSelf;
        roleChoices.gameObject.SetActive(choosingRole);
        classChoices.gameObject.SetActive(choosingClass);
        grandpaRoleMenu.SetActive(visible);
        if (!visible) return false;
        if (panelChanged)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            sessionEvents?.SetSelectedGameObject(null);
        }

        bool ru = GameLanguage.Code == "ru";
        if (choosingClass) UpdateGrandpaClassChoices(ru);
        else UpdateGrandpaRoleChoices(ru);
        return true;
    }

    private void UpdateGrandpaRoleChoices(bool ru)
    {
        var choice = lobby.LocalGrandpaChoice;
        bool volunteer = choice?.wantsGrandpa == true;
        bool ticking = lobby.Snapshot.grandpaSelectionDeadline > 0;
        int remaining = lobby.GrandpaChoiceRemaining;
        bool canChoose = choice?.connected == true && (!ticking || remaining > 0);

        SetRoleText(grandpaHeading, ru ? "Выбери роль" : "Choose your role");
        SetRoleText(grandpaTimer, ticking
            ? (ru ? "До назначения ролей " : "Roles assigned in ") + remaining + (ru ? " с" : " s")
            : ru ? "Ждём загрузки игроков…" : "Waiting for players to load…");
        SetRoleText(pigChoiceLabel, ru ? "Играть за свинью" : "Play as a pig");
        SetRoleText(hunterChoiceLabel, ru ? "Хочу играть за деда" : "Volunteer for Grandpa");
        SetRoleText(grandpaDetails, ru
            ? "Среди добровольцев решает жребий.\nЕсли желающих нет, дед выбирается из всех."
            : "A draw picks one volunteer.\nWith no volunteers, anyone can become Grandpa.");
        choosePig.interactable = chooseHunter.interactable = canChoose;
        choosePig.targetGraphic.color = ProjectSvinSkin.Selection(!volunteer);
        chooseHunter.targetGraphic.color = ProjectSvinSkin.Selection(volunteer);
    }

    private void UpdateGrandpaClassChoices(bool ru)
    {
        var choice = lobby.LocalGrandpaChoice;
        var role = lobby.AssignedGrandpaRole;
        bool hunter = role == GrandpaRole.Hunter;
        bool solo = lobby.ChoosingSoloGrandpaClass;
        bool ready = choice?.grandpaClassReady == true;
        int remaining = lobby.GrandpaChoiceRemaining;

        SetRoleText(classHeading, ru ? "Выбери класс" : "Choose your class");
        SetRoleText(classRole, hunter ? (ru ? "Роль · Дед" : "Role · Grandpa") : (ru ? "Роль · Свинья" : "Role · Pig"));
        SetRoleText(defaultClassName, hunter ? (ru ? "Обычный дед" : "Standard Grandpa") :
            PigClassPreset.Find(PigClassPreset.DefaultId)?.DisplayName ?? (ru ? "Обычная свинья" : "Standard pig"));
        SetRoleText(defaultClassStatus, ru ? "Выбран" : "Selected");
        for (int i = 0; i < lockedClassNames.Length; i++)
        {
            SetRoleText(lockedClassNames[i], ru ? "Класс закрыт" : "Class locked");
            SetRoleText(lockedClassHints[i], ru ? "Требуется разблокировка" : "Unlock required");
        }

        SetRoleText(classTimer, solo ? "" :
            (ru ? "До начала " : "Round starts in ") + remaining + (ru ? " с" : " s"));
        SetRoleText(confirmClassLabel, solo ? (ru ? "Начать" : "Start") : ready
            ? (ru ? "Ждём остальных…" : "Waiting for the others…")
            : (ru ? "Подтвердить класс" : "Confirm class"));
        confirmClass.interactable = lobby.Offline ? !lobby.Busy :
            role != GrandpaRole.None && choice?.connected == true && !ready && (solo || remaining > 0);
    }

    private (UnityEngine.UI.Button, TMP_Text) RoleButton(Transform parent, string name,
        float x, float y, float width, Action click)
    {
        var rect = Rect(name, parent, x, y, width, 78);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        ProjectSvinSkin.Bind(image, ProjectSvinRole.Button);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => click());
        return (button, CenterLabel(rect, 12, 4, width - 24, 70, 29));
    }

    private TMP_Text CenterLabel(Transform parent, float x, float y, float width, float height, int size)
    {
        var label = Label("", x, y, width, height, size, parent);
        label.alignment = TextAlignmentOptions.Center;
        return label;
    }

    private static void SetRoleText(TMP_Text label, string value)
    {
        if (label.text != value) GameLanguage.SetLiteral(label, value);
    }
}
