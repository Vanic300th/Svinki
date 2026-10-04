using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Что игрок уже собрал: по одной вещи на слот (голова, торс, ноги, обувь).
/// Только данные — картинку в HUD рисует MannequinWardrobe, а на персонаже WorldOutfitRenderer.
/// Воришка забирает вещи отсюда же; в онлайне сервер синхронизирует комплект через NetworkPlayer.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerOutfit : MonoBehaviour
{
    private static readonly int SlotCount = Enum.GetValues(typeof(ClothingSlot)).Length;

    [SerializeField] private HatClothing startingHat;
    [SerializeField] private ShirtClothing startingTop;
    [SerializeField] private PantsClothing startingPants;
    [SerializeField] private ShoesClothing startingShoes;

    private readonly Dictionary<ClothingSlot, ClothingDefinition> worn = new Dictionary<ClothingSlot, ClothingDefinition>();

    /// <summary>Вещь добавлена (или заменила другую в том же слоте).</summary>
    public event Action<ClothingDefinition> Added;
    /// <summary>Вещь пропала. Второй параметр — подпись для HUD («Воришка украл»); null — снять молча.</summary>
    public event Action<ClothingDefinition, string> Removed;
    /// <summary>Любое изменение комплекта.</summary>
    public event Action Changed;

    public int Count => worn.Count;
    public bool IsEmpty => worn.Count == 0;
    public bool IsComplete => worn.Count >= SlotCount;
    public IEnumerable<ClothingDefinition> Items => worn.Values;

    public IEnumerable<ClothingDefinition> StartingItems
    {
        get
        {
            if (startingHat != null) yield return startingHat;
            if (startingTop != null) yield return startingTop;
            if (startingPants != null) yield return startingPants;
            if (startingShoes != null) yield return startingShoes;
        }
    }

    private void Awake()
    {
        foreach (ClothingDefinition clothing in StartingItems) Add(clothing);
    }

    public bool Has(ClothingSlot slot) => worn.ContainsKey(slot);

    public ClothingDefinition Get(ClothingSlot slot) => worn.TryGetValue(slot, out ClothingDefinition item) ? item : null;

    public void Add(ClothingDefinition clothing)
    {
        if (clothing == null) return;
        if (worn.TryGetValue(clothing.Slot, out ClothingDefinition current) && current == clothing) return;
        worn[clothing.Slot] = clothing;
        Added?.Invoke(clothing);
        Changed?.Invoke();
    }

    public bool Remove(ClothingSlot slot, string reason, out ClothingDefinition removed)
    {
        if (!worn.TryGetValue(slot, out removed)) return false;
        worn.Remove(slot);
        Removed?.Invoke(removed, reason);
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Привести комплект к списку вещей (так клиент повторяет то, что решил сервер).
    /// Лишнее снимается без подписи, недостающее добавляется. События срабатывают как обычно.
    /// </summary>
    public void SetItems(IEnumerable<ClothingDefinition> items)
    {
        var wanted = new Dictionary<ClothingSlot, ClothingDefinition>();
        foreach (ClothingDefinition clothing in items)
            if (clothing != null) wanted[clothing.Slot] = clothing;

        var extra = new List<ClothingSlot>();
        foreach (KeyValuePair<ClothingSlot, ClothingDefinition> pair in worn)
            if (!wanted.TryGetValue(pair.Key, out ClothingDefinition keep) || keep != pair.Value)
                extra.Add(pair.Key);
        foreach (ClothingSlot slot in extra) Remove(slot, null, out _);
        foreach (ClothingDefinition clothing in wanted.Values) Add(clothing);
    }

    /// <summary>Убрать случайную вещь. false — убирать нечего.</summary>
    public bool RemoveRandom(string reason, out ClothingDefinition removed)
    {
        removed = null;
        if (worn.Count == 0) return false;
        int pick = UnityEngine.Random.Range(0, worn.Count);
        foreach (ClothingSlot slot in worn.Keys)
        {
            if (pick-- > 0) continue;
            return Remove(slot, reason, out removed); // выходим сразу, поэтому изменение словаря во время обхода безопасно
        }
        return false;
    }
}
