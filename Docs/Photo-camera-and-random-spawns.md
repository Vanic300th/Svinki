# Photo camera and random round spawns

The photo camera is equipment found in the store, with one camera and one monkey spawned per participant (up to six). Every pig can collect one camera. Photos and the brief cosmetic flash do not damage, freeze or knock down mannequins.

| Control | Action |
| --- | --- |
| E at camera | Collect a camera |
| K | Equip / stow |
| LMB when equipped | Take a photo; one-second shutter cooldown |
| RMB / E when equipped | Stow and free your hands |
| P | Open / close the local round album |
| Left / Right in album | Previous / next photo |
| Esc in album | Close without opening the game menu |

The viewfinder matches a 45-degree, 3:2 camera view. An extra camera renders only for an accepted shutter press; 300 × 200 images exclude the HUD and the held camera. Photos have baked grain, low-quality JPEG compression and point filtering for visible pixels in the album. Each local album keeps its latest 12 images, frees older textures, and clears when the round scene unloads. Album browsing blocks movement, looking, chat, voice, inventory and emote input. Equipment ownership, equipped state and cosmetic flashes replicate; image textures remain local. Cameras require free hands, and cannot be used while knocked down or eliminated. Eliminated players can still browse their existing album until the round ends.

The host / offline loader randomises all three carts, twelve aggressive mannequins, twenty-eight display mannequins and five thieves. Samples cover the store floor area, lie on the connected ground NavMesh, leave the entrance clear, reserve clearance against solid fixtures and maintain separation between spawns. Cart and display mannequin fallback homes are updated with their new positions. Monkeys and cameras use the same reserved layout, with distinct spawn points. The 96 garments continue to permute among the concealed, reachable shelf slots.

Existing network transforms replicate scene NPC and cart positions, NetworkPickup replicates clothing positions, and dynamically spawned equipment carries the host-selected spawn pose. Clients never independently roll a layout. Layout and equipment reset when the next round loads. Protocol version is 12: all players must use this build together. Outfit checkpoints remain compatible; transient camera equipment and photos are not checkpointed.

Validation evidence lives in `ArtSource/PhotoRound`. Unity generated the camera prefab, materials, registry entry and scene reference through editor APIs using `Tools/Unity/PhotoRoundSetup.cs`. `PhotoRoundCheck.Spawns` checked 64 seeds / 3840 clear, connected spawn placements and reproducible seed replay, including the maximum six-player equipment count. Multiplayer and image evidence are recorded separately in that directory.

The initial release passed E pickup, equipment replication and LMB capture for host and guest, with separate albums and no mannequin stun. The same clothing, NPC, cart and camera poses matched to less than 0.00002 m. Album movement blocking, Escape, the 12-image limit, equipment stow, next-round reset and a changed second-round layout passed. Single-player spawn, pickup, shutter and reset also passed. Runtime logs have no exceptions or missing prefab hash errors. The Windows x64 release ZIP passed CRC, required-file and PE architecture checks, contains the camera/layout types and excludes the development driver.

The camera update passed actual E pickup, K equip/stow/re-equip and LMB capture on two native clients; C no longer toggles the camera. Exported images are 300 × 200. All five How to Play pages passed 145 text-bound checks and document K, the photo style, the album, minimap zoom and matching player colours. The guide blocks K while open and restores control after Escape. Screenshots and validation are in ArtSource/CheapCamera.
