# Photo camera and random round spawns

Every pig carries a photo camera from the start of each round. It starts stowed, and K equips it immediately when your hands are free. World cameras no longer spawn; one monkey still spawns per participant (up to six). Photos and the brief cosmetic flash do not damage, freeze or knock down mannequins.

| Control | Action |
| --- | --- |
| K | Equip / stow |
| LMB when equipped | Take a photo; one-second shutter cooldown |
| RMB / E when equipped | Stow and free your hands |
| P | Open / close the local round album |
| Left / Right in album | Previous / next photo |
| Esc in album | Close without opening the game menu |

Press K to equip your camera without searching or collecting it. The camera cannot be equipped while eliminated, knocked down, holding a toy/mannequin or riding/pushing a cart.

The camera and album rebind when their components are enabled, including after an Editor script reload. The album photo list is serialised so its textures survive rebinding.

The viewfinder matches a 45-degree, 3:2 camera view. An extra camera renders only for an accepted shutter press; 300 × 200 images exclude the HUD and the held camera. Photos have baked grain, low-quality JPEG compression and point filtering for visible pixels in the album. Each local album keeps its latest 12 images, frees older textures, and clears when the round scene unloads. Album browsing blocks movement, looking, chat, voice, inventory and emote input. Equipped state and cosmetic flashes replicate; round photos are uploaded to the host and shared with everyone on the results screen. Cameras require free hands, and cannot be used while knocked down or eliminated. Eliminated players can still browse their existing album until the round ends.

The host / offline loader randomises all three carts, eight aggressive mannequins, forty display mannequins and five thieves. Samples cover the store floor area, lie on the connected ground NavMesh, leave the entrance clear, reserve clearance against solid fixtures and maintain separation between spawns. Cart and display mannequin fallback homes are updated with their new positions. Monkeys use the same reserved layout. The 96 garments continue to permute among the concealed, reachable shelf slots.

Existing network transforms replicate scene NPC and cart positions, NetworkPickup replicates clothing positions, and dynamically spawned monkeys carry the host-selected spawn pose. Clients never independently roll a layout. Layout and equipment reset when the next round loads. Protocol version is 17: all players must use this build together. Outfit checkpoints remain compatible; camera equipped state and photos are not checkpointed; every new or reconnected avatar already has its camera.

Historical pickup-based validation evidence lives in `ArtSource/PhotoRound`. Unity generated the camera prefab, materials, registry entry and scene reference through editor APIs using `Tools/Unity/PhotoRoundSetup.cs`. `PhotoRoundCheck.Spawns` checked 64 seeds / 3840 clear, connected spawn placements and reproducible seed replay, including the maximum six-player equipment count. Multiplayer and image evidence are recorded separately in that directory.

The initial release passed E pickup, equipment replication and LMB capture for host and guest, with separate albums and no mannequin stun. The same clothing, NPC, cart and camera poses matched to less than 0.00002 m. Album movement blocking, Escape, the 12-image limit, equipment stow, next-round reset and a changed second-round layout passed. Single-player spawn, pickup, shutter and reset also passed. Runtime logs have no exceptions or missing prefab hash errors. The Windows x64 release ZIP passed CRC, required-file and PE architecture checks, contains the camera/layout types and excludes the development driver.

The camera update passed actual E pickup, K equip/stow/re-equip and LMB capture on two native clients; C no longer toggles the camera. Exported images are 300 × 200. All five How to Play pages passed 145 text-bound checks and document K, the photo style, the album, minimap zoom and matching player colours. The guide blocks K while open and restores control after Escape. Screenshots and validation are in ArtSource/CheapCamera.

Background music uses the existing scene track and mixer routing at volume 0.45. `RoundBackgroundMusic` binds to the scene’s Ambient Music source, explicitly loads its audio data and starts looping playback when ready. It checks interrupted playback every half-second, including after component re-enabling or script reload, and respects listener pause, source mute and zero volume. It does not reset a track that is already playing.

The permanent-camera update passed `Tools/CameraEquipCheck.py` in a native development build: host and guest use K immediately without any pickup, world camera count is zero, LMB saves to separate albums and album rebinding preserves photographs. Solo starts with a stowed camera and empty album. On both clients and the subsequent solo scene, music produces a nonzero audio signal, recovers after an explicit stop and component re-enable, and respects listener pause. Evidence is in `ArtSource/CameraAndMusic`.

At the end of a round, Photo albums on the outfit scores screen opens everyone’s latest 12 photos. Select a photographer and use Previous / Next; Outfit scores returns to the ratings. Empty albums have an explicit message. Eliminated players’ photographs remain available. The host retains compressed photos as they are taken, sends them in reliable 1 KB chunks when results appear, and serves the same albums to late spectators. Photographs are cleared for the next round and when leaving the session. Protocol 17 requires matching builds.

Shared-result albums passed `Tools/ResultsAlbumsCheck.py` in a native development build: identical image hashes on host, guest and a late spectator; the latest 12 of 13 host photos; both photos of an eliminated guest; player selection, next/previous, return to scores; empty and nonempty solo albums; cleanup after the round. All gallery text fits, one EventSystem is active and its Canvas has a GraphicRaycaster. Scene and native clients have eight aggressive mannequins; three carts remain. Evidence is in `ArtSource/ResultsAlbums`.

Neutral display population increased from 28 to 40, with eight aggressive mannequins and three carts retained. The Unity Editor check passed 64 distinct seeds (3,968 clear, reachable placements, including six toy reservations per seed) and deterministic seed replay. All 156 scene network IDs are unique and nonzero; the 144 existing IDs were preserved. Evidence is in `ArtSource/NeutralMannequins`. The F1 guide reflects the new population.

The current photo implementation also keeps a permanent local SQLite archive, separate from the latest 12 round photos. `P` → `ROUND / SAVED` switches albums; lockers use saved photographs and restore their six slots across restarts. Round/result cleanup does not delete the permanent archive. Protocol 22 requires matching builds. The factory now has 80 random neutral mannequins plus six fixed, carryable AFK displays in the lift arrival room, alongside 16 aggressive mannequins, 10 thieves and eight watchers. See [Photo persistence](Photo-persistence.md) for storage, network validation, platform dependencies and current test evidence; the population and protocol numbers above describe earlier versions.
