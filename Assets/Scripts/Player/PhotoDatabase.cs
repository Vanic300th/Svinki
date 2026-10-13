using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

/// <summary>Small, local SQLite archive. Room caches and other players' images never write here.</summary>
public sealed class PhotoDatabase : IDisposable
{
    public const int ArchiveCapacity = 120, MaximumBytes = 32768;
    public sealed class Entry { public string Id; public byte[] Jpeg; }
    private readonly object gate = new object();
    private IntPtr database;
    private readonly string profile;
    private static readonly IntPtr Transient = new IntPtr(-1);
    public PhotoDatabase(string path, string profileId)
    {
        if (string.IsNullOrEmpty(profileId) || profileId.Length > 128) throw new ArgumentException("Invalid photo profile");
        profile = profileId; Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        try
        {
            Check(Native.sqlite3_open_v2(Utf8(path), out database, 6 | 0x10000, IntPtr.Zero));
            Check(Native.sqlite3_busy_timeout(database, 1000));
            Execute("PRAGMA foreign_keys=ON;"); Execute("PRAGMA journal_mode=WAL;");
            Execute("CREATE TABLE IF NOT EXISTS photos(profile TEXT NOT NULL,id TEXT NOT NULL,created INTEGER NOT NULL,jpeg BLOB NOT NULL,PRIMARY KEY(profile,id));");
            Execute("CREATE TABLE IF NOT EXISTS locker_photos(profile TEXT NOT NULL,slot INTEGER NOT NULL CHECK(slot BETWEEN 0 AND 5),photo TEXT NOT NULL,PRIMARY KEY(profile,slot),FOREIGN KEY(profile,photo) REFERENCES photos(profile,id));");
            Execute("PRAGMA user_version=1;");
        }
        catch { Dispose(); throw; }
    }
    public static string ContentId(byte[] jpeg)
    { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(jpeg)).Replace("-", "").ToLowerInvariant(); }
    public string Save(byte[] jpeg)
    {
        if (jpeg == null || jpeg.Length < 4 || jpeg.Length > MaximumBytes || jpeg[0] != 255 || jpeg[1] != 216) throw new ArgumentException("Invalid camera JPEG");
        string id = ContentId(jpeg);
        lock (gate)
        {
            Execute("BEGIN IMMEDIATE;");
            try
            {
                using (var statement = Prepare("INSERT INTO photos(profile,id,created,jpeg) VALUES(?,?,?,?) ON CONFLICT(profile,id) DO UPDATE SET created=excluded.created;"))
                { statement.Text(1, profile); statement.Text(2, id); statement.Int64(3, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); statement.Blob(4, jpeg); statement.Done(); }
                Prune(); Execute("COMMIT;");
            }
            catch { Execute("ROLLBACK;"); throw; }
        }
        return id;
    }
    public List<Entry> Read()
    {
        lock (gate) using (var statement = Prepare("SELECT id,jpeg FROM photos WHERE profile=? ORDER BY created DESC,rowid DESC LIMIT 126;"))
        {
            statement.Text(1, profile); var entries = new List<Entry>();
            while (statement.Row()) entries.Add(new Entry { Id = statement.String(0), Jpeg = statement.Bytes(1) });
            return entries;
        }
    }
    public Dictionary<int, byte[]> ReadPins()
    {
        lock (gate) using (var statement = Prepare("SELECT l.slot,p.jpeg FROM locker_photos l JOIN photos p ON p.profile=l.profile AND p.id=l.photo WHERE l.profile=? ORDER BY l.slot;"))
        {
            statement.Text(1, profile); var result = new Dictionary<int, byte[]>();
            while (statement.Row()) result[(int)Native.sqlite3_column_int64(statement.Handle, 0)] = statement.Bytes(1);
            return result;
        }
    }
    public void Pin(int slot, string id)
    {
        if (slot < 0 || slot > 5) throw new ArgumentOutOfRangeException(nameof(slot));
        lock (gate)
        {
            using (var statement = Prepare(id == null ? "DELETE FROM locker_photos WHERE profile=? AND slot=?;" : "INSERT INTO locker_photos(profile,slot,photo) VALUES(?,?,?) ON CONFLICT(profile,slot) DO UPDATE SET photo=excluded.photo;"))
            { statement.Text(1, profile); statement.Int64(2, slot); if (id != null) statement.Text(3, id); statement.Done(); }
            Prune();
        }
    }
    private void Prune()
    {
        using (var statement = Prepare("DELETE FROM photos WHERE profile=? AND id NOT IN(SELECT photo FROM locker_photos WHERE profile=?) AND id NOT IN(SELECT id FROM photos WHERE profile=? ORDER BY created DESC,rowid DESC LIMIT 120);"))
        { for (int i = 1; i <= 3; i++) statement.Text(i, profile); statement.Done(); }
    }
    private void Execute(string sql) { using (var statement = Prepare(sql)) statement.Done(true); }
    private Statement Prepare(string sql)
    {
        if (database == IntPtr.Zero) throw new ObjectDisposedException(nameof(PhotoDatabase));
        Check(Native.sqlite3_prepare_v2(database, Utf8(sql), -1, out var pointer, IntPtr.Zero)); return new Statement(this, pointer);
    }
    private void Check(int result) { if (result != 0) throw new IOException("SQLite photo archive: " + Marshal.PtrToStringAnsi(Native.sqlite3_errmsg(database))); }
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value + '\0');
    public void Dispose() { lock (gate) { if (database != IntPtr.Zero) { Native.sqlite3_close_v2(database); database = IntPtr.Zero; } } GC.SuppressFinalize(this); }
    ~PhotoDatabase() { Dispose(); }
    private sealed class Statement : IDisposable
    {
        private readonly PhotoDatabase owner; public IntPtr Handle { get; private set; }
        public Statement(PhotoDatabase db, IntPtr pointer) { owner = db; Handle = pointer; }
        public void Text(int index, string value) { var bytes = Encoding.UTF8.GetBytes(value); owner.Check(Native.sqlite3_bind_text(Handle, index, bytes, bytes.Length, Transient)); }
        public void Blob(int index, byte[] bytes) => owner.Check(Native.sqlite3_bind_blob(Handle, index, bytes, bytes.Length, Transient));
        public void Int64(int index, long value) => owner.Check(Native.sqlite3_bind_int64(Handle, index, value));
        public bool Row() { int result = Native.sqlite3_step(Handle); if (result == 100) return true; if (result == 101) return false; owner.Check(result); return false; }
        public void Done(bool allowRows = false) { while (Row()) if (!allowRows) throw new IOException("Unexpected SQLite result"); }
        public string String(int column) { int length = Native.sqlite3_column_bytes(Handle, column); var bytes = new byte[length]; Marshal.Copy(Native.sqlite3_column_text(Handle, column), bytes, 0, length); return Encoding.UTF8.GetString(bytes); }
        public byte[] Bytes(int column) { int length = Native.sqlite3_column_bytes(Handle, column); if (length < 4 || length > MaximumBytes) throw new IOException("Invalid archived image size"); var bytes = new byte[length]; Marshal.Copy(Native.sqlite3_column_blob(Handle, column), bytes, 0, length); return bytes; }
        public void Dispose() { if (Handle != IntPtr.Zero) Native.sqlite3_finalize(Handle); Handle = IntPtr.Zero; }
    }
    private static class Native
    {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        private const string Library = "/usr/lib/libsqlite3.dylib";
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        private const string Library = "libsqlite3.so.0";
#else
        private const string Library = "sqlite3";
#endif
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_open_v2(byte[] name, out IntPtr db, int flags, IntPtr vfs);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_close_v2(IntPtr db);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_busy_timeout(IntPtr db, int ms);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_errmsg(IntPtr db);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int length, out IntPtr statement, IntPtr tail);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_step(IntPtr statement);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_finalize(IntPtr statement);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_bind_text(IntPtr statement, int index, byte[] value, int length, IntPtr destructor);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_bind_blob(IntPtr statement, int index, byte[] value, int length, IntPtr destructor);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_bind_int64(IntPtr statement, int index, long value);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern long sqlite3_column_int64(IntPtr statement, int column);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_column_text(IntPtr statement, int column);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_column_blob(IntPtr statement, int column);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_column_bytes(IntPtr statement, int column);
    }
}
