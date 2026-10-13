using System;
using UnityEngine;

/// <summary>The explicitly approved wardrobe; source folders are not an import queue.</summary>
[CreateAssetMenu(menuName = "Svinki/Clothing import policy")]
public sealed class ClothingLibraryImportPolicy : ScriptableObject
{
    [Tooltip("Only these definitions are rebuilt and admitted to the player catalog automatically.")]
    public ClothingDefinition[] approvedDefinitions = Array.Empty<ClothingDefinition>();

    [Tooltip("Allow deliberately selected unrigged models. Existing compatibility IDs do not need this enabled.")]
    public bool allowLegacyModels;
}
