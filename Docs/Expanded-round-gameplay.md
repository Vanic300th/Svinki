# Expanded round gameplay

The main game level now contains 30 departments. Ground-floor area increased from 4,362.5 m² to 13,087.5 m². All 16 new departments have complete NavMesh routes from the entrance. There are exactly three shopping carts and twelve aggressive mannequins. Ninety-six clothing pickups shuffle between authored locations once per round, on the host.

## Controls

- **E**: interact with the wall-mounted READY button; push or release a cart; collect clothing.
- **R**: ride in a cart basket.
- **W/S, A/D, Shift**: push, steer, sprint with a cart.
- **Left mouse button**: launch the cart while pushing.
- **G**: open cart storage while looking at a nearby cart, or while pushing/riding one.
- **Tab / I**: outfit inventory.
- **Left / right arrows**: switch between living players after elimination.

Cart storage holds eight independent garments, including repeated clothing slots. Its window supports storing worn clothes, loading a nearby floor item, wearing cargo, and unloading cargo onto the floor. Transfers require proximity, an unobstructed path, a stationary/slow cart and the current cargo revision. The host performs each transfer atomically and replicates the result. Cargo is visible in the basket and resets in the next round. Wall collisions stop the cart through physics without ejecting or knocking down its driver or basket passenger. Repeated impacts were checked at 8 m/s with both roles; actual cart impacts against players outside the cart still cause temporary falls.

## Mannequin attacks and elimination

Aggressive mannequins freeze when a living player sees them. An attack applies one hit during its animation only if the target is still in range and no wall blocks the strike. The first valid hit causes a two-second physics fall, followed by one second of immunity. A second valid hit in that round eliminates the player. Display mannequin throws and cart impacts cause temporary falls without consuming the two lethal NPC hits.

Eliminated players drop their clothing, cannot interact or control their character, and spectate living players until the next round. Eliminating the host leaves the server running. Eliminated participants do not block READY completion. If all participants are eliminated, the round automatically proceeds to the podium/results. Reconnecting during the same round preserves elimination. Single-player elimination offers a NEXT ROUND button.

The HUD shows remaining hits, through-wall player nicknames, a floorplan minimap with player/cart/READY markers, and the current spectator target. The existing live pig portrait remains connected to the player's actual appearance and pose. Eliminated participants appear on the final runway as glowing, translucent pigs with their pre-round customization; see `Docs/Runway-ghosts.md`. Single-use monkey toys are described in `Docs/Monkey-toy.md`. Clothing uses a brighter, more saturated fabric shader while retaining its original texture and base color.

## Validation

Reports and captures are in `ArtSource/ExpandedRound/`. Tests cover saved scene area/counts/navigation/network IDs, offline cargo capacity and stale requests, ragdoll binding to real pig bones, elimination and restart, and two-process host/client replication. The local host/client tests also verify guest cart steering and release, remote speed reporting, host survival of a physics mannequin throw, automatic results when everyone dies, READY completion by the last survivor, and elimination preserved after reconnect. The network protocol is version 8; all participants need the updated build.

The Windows x64 build completed successfully and is packaged as `Builds/Svinki-Windows.zip` (80.0 MiB, including monkey toys and runway ghosts). ZIP integrity and required runtime files were verified, and the BuildReport confirms both Lobby and SampleScene. Runtime integration checks were performed on macOS with a standalone guest and Editor host; the Windows executable has not been run on Windows here. The cross-platform build reported a missing `vswhere.exe` toolset-detection diagnostic while still returning `Succeeded`.

The Windows builder now activates the existing Windows Build Profile once and waits for recompilation before building, avoiding duplicate platform-switch requests. On failure it restores the original profile; on success it keeps Windows selected. API reference: [Unity BuildProfile.SetActiveBuildProfile](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Build.Profile.BuildProfile.SetActiveBuildProfile.html).

Recreate the authored expansion through Unity Pipeline with `Tools/Unity/ExpandedRoundSetup.cs`, entry `ExpandedRoundSetup.Build`. This edits the existing SampleScene through Unity Editor APIs and saves its navigation/map data.
