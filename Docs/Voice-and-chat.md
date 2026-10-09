# Proximity voice and side chat

Hold **B** to talk during a multiplayer round. Releasing B, opening text chat or the emote wheel, leaving the game window, disconnecting, or dying stops microphone capture. The HUD shows the microphone status. The first use requests microphone access where the operating system requires it. The system uses the default microphone and downmixes stereo input to mono, resampling the device rate to 16 kHz. Speech is not saved to disk. Eliminated players can hear nearby living players from the spectator camera; their own microphone stays muted.

Voices play from the speaking pig's position with spatial panning, full volume up to 2 metres, linear attenuation between 2 and 25 metres, and silence beyond 25 metres. Walls do not change volume. Voice is available during the round; lobby and runway screens do not transmit voice.

Audio uses the existing FishNet connection (local transport or EOS online transport): 16 kHz mono, 20 ms independently decodable IMA ADPCM frames, 164 bytes per frame. The server checks ownership, live round membership, packet size, frame sequence and rate. Playback has a bounded jitter buffer, suppresses local echo, rejects stale/duplicate frames, and recovers independently after packet loss. No extra voice service account is needed.

Text still appears above the speaking pig and now remains in a panel on the left. The latest **50 messages** stay available across rounds in the same session. Press **Enter** to open chat and scroll back through the history; Enter sends and Esc cancels. Oldest messages are removed when the limit is reached. Leaving or changing session clears the history. Message markup is displayed literally.

Network protocol is **10**. Every player must use the updated build. The Windows release is `Builds/Svinki-Windows.zip`.

Validation artifacts: `ArtSource/VoiceChat/`. The codec, UI and multiplayer tests use synthetic audio; they do not capture a person's microphone or conversation. Runtime validation uses the Unity Editor host and a separate Mac development player. Bidirectional voice, no local echo, packet rejection, the death gate, persistent replicated text and actual listener output were checked: RMS 0.149511 at 2 m, 0.074695 at 13.5 m and 0 at 35 m. Physical microphone capture and EOS internet sessions were not exercised; the Windows executable is packaged but not executed on macOS.

Implementation references: [Unity microphone capture](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Microphone.Start.html), [Unity audio buffer reads](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioClip.GetData.html), [FishNet RPCs](https://fish-networking.gitbook.io/docs/guides/features/network-communication/remote-procedure-calls).
