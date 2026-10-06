using System.Collections.Generic;
using UnityEngine;

// All clothes share plain materials that respond to scene lighting.
public static class ClothingVisuals
{
    private static readonly Dictionary<Material, Material> plain = new Dictionary<Material, Material>();
    private static readonly Dictionary<Material, Material> targeted = new Dictionary<Material, Material>();
    public static Material Highlight(Material source)
    {
        if (source == null) return null;
        if (targeted.TryGetValue(source, out Material result) && result != null) return result;
        result = new Material(Plain(source)) { name = source.name + " (Targeted)" };
        result.EnableKeyword("_EMISSION");
        if (result.HasProperty("_EmissionColor")) result.SetColor("_EmissionColor", new Color(.35f,.52f,.58f));
        targeted[source] = result; return result;
    }
    public static Material Plain(Material source)
    {
        if (source == null) return null;
        if (plain.TryGetValue(source, out Material result) && result != null) return result;
        result = new Material(source) { name = source.name + " (Worn)" };
        result.DisableKeyword("_EMISSION");
        if (result.HasProperty("_EmissionColor")) result.SetColor("_EmissionColor", Color.black);
        if (result.HasProperty("_Cull")) result.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        plain[source] = result;
        return result;
    }
    public static void Prepare(GameObject model, Material fabric, int layer)
    {
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (Light light in model.GetComponentsInChildren<Light>(true)) light.enabled = false;
        foreach (ParticleSystem particles in model.GetComponentsInChildren<ParticleSystem>(true))
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer) { renderer.enabled = false; continue; }
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = Plain(fabric != null ? fabric : materials[i]);
            renderer.sharedMaterials = materials;
        }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        foreach (Material material in plain.Values) if (material != null) Object.Destroy(material);
        plain.Clear();
        foreach (Material material in targeted.Values) if (material != null) Object.Destroy(material);
        targeted.Clear();
    }
}
