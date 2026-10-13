using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum LobbyMediaKind : byte { Video, Audio }
public struct ResolvedMedia { public string Url, Title, Id; public AudioType AudioType; public LobbyMediaBackend Backend; }
public interface IMediaSourceProvider
{
    bool TryResolve(string url, LobbyMediaKind kind, out ResolvedMedia media);
}

/// <summary>Providers resolve metadata/compatible sources locally. No media bytes enter FishNet/EOS.</summary>
public static class MediaSourceProvider
{
    private static readonly List<IMediaSourceProvider> providers = new List<IMediaSourceProvider> { new YouTubeMediaProvider(), new CcMixterMediaProvider(), new AudiusMediaProvider(), new DirectMediaProvider() };
    public static void Register(IMediaSourceProvider provider) { if (provider != null && !providers.Contains(provider)) providers.Insert(0, provider); }
    public static bool TryResolve(string input, LobbyMediaKind kind, out ResolvedMedia media)
    {
        media = default;
        if (string.IsNullOrWhiteSpace(input) || input.Length > 2048 || !Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo)) return false;
        foreach (var provider in providers) if (provider.TryResolve(uri.AbsoluteUri, kind, out media)) return true;
        return false;
    }
    private sealed class DirectMediaProvider : IMediaSourceProvider
    {
        public bool TryResolve(string url, LobbyMediaKind kind, out ResolvedMedia media)
        {
            var uri = new Uri(url); string extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            if (YouTubeMediaProvider.IsHost(uri.Host) || CcMixterMediaProvider.IsHost(uri.Host) || uri.Host == "audius.co" || uri.Host == "www.audius.co" || uri.Host == "api.audius.co") { media = default; return false; }
            var audio = extension == ".mp3" ? AudioType.MPEG : extension == ".ogg" ? AudioType.OGGVORBIS : extension == ".wav" ? AudioType.WAV : AudioType.UNKNOWN;
            bool supported = kind == LobbyMediaKind.Audio ? audio != AudioType.UNKNOWN : extension == ".mp4" || extension == ".webm" || extension == ".ogv" || extension == ".mov";
            media = supported ? new ResolvedMedia { Url = uri.AbsoluteUri, Title = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath)), AudioType = audio } : default;
            return supported;
        }
    }
}
