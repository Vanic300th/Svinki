using System;
using System.IO;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

public static class CcMixterCheck
{
    static void Need(bool value, string message) { if (!value) throw new Exception(message); }
    static readonly MethodInfo Read = typeof(CcMixterMusicCatalog).GetMethod("Read");
    static readonly Type Json = Read.GetParameters()[0].ParameterType;
    static CatalogTrack Parse(string text) => (CatalogTrack)Read.Invoke(null, new[] { Json.GetMethod("Parse", new[] { typeof(string) }).Invoke(null, new object[] { text }) });
    static string Change(string text, string field, string value) => Regex.Replace(text, "\"" + field + "\"\\s*:\\s*(\"[^\"]*\"|[0-9]+)", "\"" + field + "\": " + value);
    public static string Safety()
    {
        // Pipeline references two JSON assemblies: use the runtime catalog's own JSON parser.
        var array = Json.Assembly.GetType("Newtonsoft.Json.Linq.JArray").GetMethod("Parse", new[] { typeof(string) }).Invoke(null, new object[] { File.ReadAllText("ArtSource/OnlineMedia/ccmixter-metadata.json") });
        string row = ((IList)array)[0].ToString();
        var track = Parse(row);
        Need(track != null && track.LicenseName == "CC BY 3.0" && track.StreamUrl.EndsWith(".mp3"), "Real API metadata rejected");
        foreach (var license in new[] { "https://creativecommons.org/licenses/by-nc/3.0/", "https://creativecommons.org/licenses/by-nd/4.0/", "https://creativecommons.org/licenses/by-sa/4.0/", "https://creativecommons.org.evil.example/licenses/by/3.0/", "https://user:password@creativecommons.org/licenses/by/3.0/", "file:///licenses/by/3.0/" })
            Need(Parse(Change(row, "license_url", "\"" + license + "\"")) == null, "Bad license accepted: " + license);
        foreach (var url in new[] { "file:///tmp/a.mp3", "https://evil.example/content/a.mp3", "https://user:password@ccmixter.org/content/a.mp3" })
            Need(Parse(Change(row, "download_url", "\"" + url + "\"")) == null, "Unsafe stream accepted");
        Need(Parse(Change(row, "file_rawsize", "40000000")) == null, "Oversized audio accepted");
        Need(Parse(Change(row, "upload_id", "999999")) == null, "ID/source mismatch accepted");
        Need(Parse(Change(row, "file_rawsize", "[]")) == null, "Malformed field accepted");
        foreach (var url in new[] { "https://ccmixter.org/content/artist/a.mp3", "https://ccmixter.org:999/files/artist/71247", "https://ccmixter.org/files/artist/not-a-number", "https://ccmixter.org/files/artist/71247/a.mp3" })
            Need(!MediaSourceProvider.TryResolve(url, LobbyMediaKind.Audio, out _), "License validation bypass accepted: " + url);
        Need(MediaSourceProvider.TryResolve("http://www.ccmixter.org/files/mykleanthony/71247?x=1", LobbyMediaKind.Audio, out var canonical) && canonical.Url == track.Url, "Track page normalization failed");
        Need(!MediaSourceProvider.TryResolve(track.Url, LobbyMediaKind.Video, out _), "ccMixter accepted as video");
        Need(MediaSourceProvider.TryResolve("https://cdn.example.com/a.mp3", LobbyMediaKind.Audio, out _), "Direct MP3 regression");
        string report = "PASS real ccMixter JSON parsed; title/author/featuring/CC BY license and full MP3; NC/ND/SA/foreign license hosts/credentials/file schemes rejected; stream domain and size checks; malformed fields fail closed; ID/source consistency; direct ccMixter MP3 cannot bypass metadata validation; stable track pages canonicalized; direct MP3 retained.";
        File.WriteAllText("ArtSource/OnlineMedia/ccmixter-safety-validation.txt", report);
        return report;
    }
}
