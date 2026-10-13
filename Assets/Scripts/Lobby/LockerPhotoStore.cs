using System.Collections.Generic;
/// <summary>Persistence seam for an eventual photo/profile adapter. Currently session scoped.</summary>
public interface ILockerPhotoStore
{
    IEnumerable<KeyValuePair<string, byte[]>> Entries { get; }
    void Put(string key, byte[] jpeg);
    void Clear();
}
public sealed class SessionLockerPhotoStore : ILockerPhotoStore
{
    private readonly Dictionary<string, byte[]> entries = new Dictionary<string, byte[]>();
    public IEnumerable<KeyValuePair<string, byte[]>> Entries => entries;
    public void Put(string key, byte[] jpeg) { if (jpeg == null) entries.Remove(key); else entries[key] = jpeg; }
    public void Clear() => entries.Clear();
}
