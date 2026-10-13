using TMPro;
using UnityEngine;

/// <summary>World labels keep depth testing when localization replaces their font atlas.</summary>
[DisallowMultipleComponent]
public sealed class WorldTextDepth : MonoBehaviour
{
    [SerializeField] private Shader depthShader;
    private Material instance;
    private TMP_Text tmp;
    private TextMesh legacy;
    private Renderer target;

    private void OnEnable()
    {
        tmp = GetComponent<TextMeshPro>();
        legacy = GetComponent<TextMesh>();
        target = GetComponent<Renderer>();
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(TextChanged);
        Font.textureRebuilt += FontRebuilt;
        RefreshMaterial();
    }
    private void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(TextChanged);
        Font.textureRebuilt -= FontRebuilt;
    }
    private void TextChanged(Object text) { if (text == tmp) RefreshMaterial(); }
    private void FontRebuilt(Font font) { if (legacy != null && legacy.font == font) RefreshMaterial(); }
    public void RefreshMaterial()
    {
        if (depthShader == null || target == null) return;
        var source = tmp != null ? tmp.fontSharedMaterial : legacy != null && legacy.font != null ? legacy.font.material : null;
        if (source == null) return;
        if (instance == null) instance = new Material(source) { name = source.name + " World depth", hideFlags = HideFlags.DontSave };
        else if (source != instance) instance.CopyPropertiesFromMaterial(source);
        instance.shader = depthShader;
        if (tmp != null)
        {
            if (tmp.fontSharedMaterial != instance) tmp.fontSharedMaterial = instance;
        }
        else target.sharedMaterial = instance;
    }
    private void OnDestroy() { if (instance != null) Destroy(instance); }
}
