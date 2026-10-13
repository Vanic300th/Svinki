using UnityEngine;
/// <summary>Display only. Ownership is checked by PlayerLocker; the session owns textures.</summary>
public sealed class LockerPhotoSlot : MonoBehaviour
{
    [SerializeField] private int slot;
    [SerializeField] private Renderer frame;
    private Material material;
    private Texture shown;
    public int Slot => slot;
    public Texture Image => shown;
    private void Awake() => material = frame.material;
    public void Show(Texture image) { if (shown == image) return; shown = image; material.mainTexture = image; }
    private void OnDestroy() { if (material != null) Destroy(material); }
}
