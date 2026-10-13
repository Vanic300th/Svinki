using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Presentation-only materials preserve the pig's original markings and accessories.</summary>
public sealed class RunwayGhost : MonoBehaviour
{
    private readonly List<Material> materials = new List<Material>();
    public void Apply()
    {
        if (materials.Count > 0) return;
        var template = Resources.Load<Material>("Pigs/RunwayGhost");
        if (template == null) { Debug.LogError("Runway ghost material is missing."); return; }
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            var slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                var original = slots[i];
                if (original == null) continue;
                var ghost = new Material(template) { name = original.name + " (Runway Ghost)" };
                ghost.CopyPropertiesFromMaterial(original);
                ghost.renderQueue = (int)RenderQueue.Transparent;
                ghost.SetColor("_GhostTint", template.GetColor("_GhostTint"));
                ghost.SetFloat("_GhostOpacity", template.GetFloat("_GhostOpacity"));
                ghost.enableInstancing = true;
                materials.Add(ghost); slots[i] = ghost;
            }
            // Per-slot appearance property blocks remain on the renderer, including tattoos.
            renderer.sharedMaterials = slots;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
    private void OnDestroy()
    { foreach (var material in materials) if (material != null) Destroy(material); }
}
