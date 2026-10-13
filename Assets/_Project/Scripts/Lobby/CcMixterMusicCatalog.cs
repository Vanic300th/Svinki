using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

/// <summary>Official Query API. Only CC BY full audio; no HTML scraping or account keys.</summary>
public sealed class CcMixterMusicCatalog : IMusicCatalogProvider
{
    private const string Api = "https://ccmixter.org/api/query?f=json&lic=by&limit=12";
    public string Name => "ccMixter";

    public IEnumerator Search(string query, Action<List<CatalogTrack>, string> result)
    {
        query = query?.Trim() ?? "";
        if (query.Length > 120) { result(new List<CatalogTrack>(), "Use up to 120 characters"); yield break; }
        string url = Api + "&reqtags=remix" + (query.Length == 0 ? "" : "&search=" + Uri.EscapeDataString(query));
        yield return Json(url, (rows, error) =>
        {
            var tracks = new List<CatalogTrack>();
            if (rows != null) foreach (var row in rows)
            {
                var track = Read(row);
                if (track != null && tracks.Count < 6) tracks.Add(track);
            }
            result(tracks, error);
        });
    }

    public IEnumerator Resolve(ResolvedMedia input, Action<CatalogTrack, string> result)
    {
        if (input.Backend != LobbyMediaBackend.CcMixter || !CcMixterMediaProvider.ValidId(input.Id))
        { result(null, "Invalid ccMixter track"); yield break; }
        yield return Json(Api + "&ids=" + input.Id, (rows, error) =>
        {
            CatalogTrack track = rows?.Count == 1 ? Read(rows[0]) : null;
            // The host validates the actual license and file, never client-supplied metadata.
            if (track != null && track.Url != input.Url) track = null;
            result(track, error ?? (track == null ? "Track unavailable, unsupported, or not licensed CC BY" : null));
        });
    }

    public static CatalogTrack Read(JToken token)
    {
        try
        {
            if (!(token is JObject row) || row["upload_extra"]?.Value<bool?>("nsfw") == true) return null;
            string source = row.Value<string>("file_page_url");
            if (!MediaSourceProvider.TryResolve(source, LobbyMediaKind.Audio, out var media) || media.Backend != LobbyMediaBackend.CcMixter ||
                row.Value<string>("upload_id") != media.Id) return null;
            string license = row.Value<string>("license_url");
            if (!Uri.TryCreate(license, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https") || uri.Host != "creativecommons.org" ||
                !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
                !Regex.IsMatch(uri.AbsolutePath, "^/licenses/by/(1\\.0|2\\.0|2\\.5|3\\.0|4\\.0)/$")) return null;
            string title = row.Value<string>("upload_name"), artist = row.Value<string>("user_real_name") ?? row.Value<string>("user_name");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist) || !(row["files"] is JArray files)) return null;
            foreach (var file in files)
            {
                string audio = file.Value<string>("download_url");
                if (!SafeAudioUrl(audio) || file.Value<int?>("file_is_remote") != 0 ||
                    !(file["file_format_info"] is JObject info) || info.Value<string>("media-type") != "audio") continue;
                long size = file.Value<long?>("file_rawsize") ?? 0;
                string[] time = (info.Value<string>("ps") ?? "").Split(':');
                if (size <= 0 || size > 32 * 1024 * 1024 || time.Length != 2 ||
                    !int.TryParse(time[0], NumberStyles.None, CultureInfo.InvariantCulture, out int minutes) ||
                    !int.TryParse(time[1], NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) ||
                    seconds >= 60 || minutes < 0 || minutes > 30) continue;
                int duration = minutes * 60 + seconds;
                if (duration < 30 || duration > 1800) continue;
                string featuring = row["upload_extra"]?.Value<string>("featuring");
                string credit = Limit(artist, 120) + (string.IsNullOrWhiteSpace(featuring) ? "" : " feat. " + Limit(featuring, 160));
                string version = uri.AbsolutePath.Split('/')[3];
                return new CatalogTrack { Id = media.Id, Title = Limit(title, 160), Artist = Limit(artist, 120), Credit = credit,
                    Url = media.Url, Source = media.Url, StreamUrl = audio, Duration = duration,
                    LicenseUrl = "https://creativecommons.org" + uri.AbsolutePath, LicenseName = "CC BY " + version };
            }
        }
        catch (Exception) { } // Untrusted metadata must fail closed, including unexpected JSON types.
        return null;
    }

    private static string Limit(string value, int size) => value.Length > size ? value.Substring(0, size) : value;
    private static bool SafeAudioUrl(string value) => value != null && value.Length <= 2048 &&
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.IsDefaultPort &&
        uri.Host == "ccmixter.org" && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 &&
        uri.AbsolutePath.StartsWith("/content/", StringComparison.Ordinal) && uri.AbsolutePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);

    private static IEnumerator Json(string url, Action<JArray, string> result)
    {
        using (var request = UnityWebRequest.Get(url))
        {
            request.timeout = 12; request.redirectLimit = 3;
            request.SetRequestHeader("Accept", "application/json");
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (request.downloadedBytes > 512 * 1024) { request.Abort(); break; }
                yield return null;
            }
            if (request.result != UnityWebRequest.Result.Success || request.downloadedBytes > 512 * 1024)
            { result(null, "ccMixter unavailable. Try again later."); yield break; }
            JArray rows = null; string error = null;
            try { rows = JArray.Parse(request.downloadHandler.text); }
            catch (Exception) { error = "ccMixter returned an invalid response"; }
            result(rows, error);
        }
    }
}
