using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Svinki/Grandpa's Fridge/Pig Class Preset")]
public sealed class PigClassPreset : ScriptableObject
{
    public const string DefaultId = "standard";

    [SerializeField] private string id = DefaultId;
    [SerializeField] private string englishName = "Standard pig";
    [SerializeField] private string russianName = "Обычная свинья";
    [SerializeField, Range(.5f, 1.5f)] private float movementMultiplier = 1f;

    private static PigClassPreset[] presets;

    public string Id => id;
    public string DisplayName => GameLanguage.Code == "ru" ? russianName : englishName;
    public float MovementMultiplier => movementMultiplier;

    public static PigClassPreset[] Presets => presets ??= Resources.LoadAll<PigClassPreset>("GrandpaPigClasses")
        .Where(preset => !string.IsNullOrWhiteSpace(preset.Id))
        .OrderBy(preset => preset.Id != DefaultId).ThenBy(preset => preset.Id).ToArray();

    public static PigClassPreset Find(string id) => Presets.FirstOrDefault(preset => preset.Id == id);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => presets = null;
}
