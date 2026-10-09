# Eliminated players on the runway

An eliminated round participant remains in the final show as a translucent, glowing pig that floats along the catwalk. The model preserves the appearance chosen before the round: face, colors, markings, tattoos and accessories. Collected clothing is not attached to the ghost. Living participants continue to present their final outfits normally.

The host includes the elimination flag in each captured result entry, together with the pre-round appearance. Both the runway and the score portrait render that result. Ghost materials are cloned for each preview and destroyed with it; the player model and shared source materials remain unchanged. The shader preserves the pig skin's bind-pose pattern and tattoo coordinates.

In single player, death opens the ghost presentation automatically. Next Round then starts a fresh run; a living player's Keep Searching behavior remains unchanged. Multiplayer participants who die continue spectating until the round ends, then appear in its final show.

Network protocol version: **10**. All players need the updated build. Runtime checks and screenshots are in `ArtSource/RunwayGhost/`; the packaged Windows version is `Builds/Svinki-Windows.zip`.

Validation covers actual two-hit death, automatic single-player presentation and reset, unchanged living models, preserved markings/accessories, unchanged shared materials, ghost score portraits, and matching host/standalone-guest result snapshots and visuals. Runtime checks were performed on macOS; the Windows executable has not been run on Windows here.
