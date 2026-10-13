using UnityEngine;

/// <summary>
/// Один игровой звук: варианты клипа + разброс громкости и высоты тона.
/// Создаётся как ассет (Create → Svinki → Audio → Sound Effect) и настраивается в инспекторе.
/// Проигрывается через AudioSource, который стоит на объекте в префабе или сцене.
/// </summary>
[CreateAssetMenu(fileName = "New Sound", menuName = "Svinki/Audio/Sound Effect")]
public sealed class SoundEffect : ScriptableObject
{
    [Tooltip("Варианты звука. Каждый раз берётся случайный.")]
    [SerializeField] private AudioClip[] clips = new AudioClip[0];

    [Tooltip("Громкость: случайное значение между X (мин) и Y (макс).")]
    [SerializeField] private Vector2 volume = new Vector2(0.9f, 1f);

    [Tooltip("Высота тона: случайное значение между X (мин) и Y (макс). 1 = без изменений.")]
    [SerializeField] private Vector2 pitch = new Vector2(0.95f, 1.05f);

    [Tooltip("Не играть один и тот же вариант два раза подряд.")]
    [SerializeField] private bool avoidRepeat = true;

    [System.NonSerialized] private int lastIndex = -1;

    public bool HasClips => clips != null && clips.Length > 0;

    /// <summary>Проиграть случайный вариант на источнике. volumeScale умножает громкость.</summary>
    public bool Play(AudioSource source, float volumeScale = 1f)
    {
        if (source == null || !source.isActiveAndEnabled || !HasClips) return false;
        AudioClip clip = Pick();
        if (clip == null) return false;
        source.pitch = Random.Range(Mathf.Min(pitch.x, pitch.y), Mathf.Max(pitch.x, pitch.y));
        source.PlayOneShot(clip, Random.Range(Mathf.Min(volume.x, volume.y), Mathf.Max(volume.x, volume.y)) * volumeScale);
        return true;
    }

    private AudioClip Pick()
    {
        if (clips.Length == 1) return clips[0];
        int index = Random.Range(0, clips.Length);
        if (avoidRepeat && index == lastIndex) index = (index + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
        lastIndex = index;
        return clips[index];
    }

    private void OnValidate()
    {
        volume.x = Mathf.Clamp01(volume.x); volume.y = Mathf.Clamp01(volume.y);
        pitch.x = Mathf.Clamp(pitch.x, 0.1f, 3f); pitch.y = Mathf.Clamp(pitch.y, 0.1f, 3f);
    }
}
