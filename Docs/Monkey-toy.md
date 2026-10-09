# Single-use monkey toy

The toy uses the Chimpanzee mesh and its original vertex colors from `Assets/EverythingLibrary_Animals_002/EverythingLibrary_Animals_002.blend`. A new skinned rig has fifteen bones; thirteen connected physics bodies simulate the torso, head, arms, wrists and legs. The editable rig is in `ArtSource/MonkeyToy/ChimpanzeeToy.blend`, with its exported model in `Assets/Art/Items/MonkeyToy/`.

Each round spawns exactly one toy per participating player, near the entrance. Single player spawns one. Joining as a spectator does not add toys, and consumed toys remain gone until the next round.

- **E** picks up a nearby toy.
- **Left mouse** swings it.
- **Right mouse / E** drops it without consuming it.

The toy hangs from one pinned wrist, preserving the bone's grip orientation. The free arm, torso, head and legs remain physical. Stronger damping, reduced joint angles and a slower, smoothed grip keep their sway small during movement and swings. These settings apply only to the toy; mannequin falls keep their existing physics. A miss preserves the toy. The first successful mannequin hit consumes it, including a hit against an ordinary display mannequin. Only aggressive mannequins get the thirty-second stun: their actual skinned skeleton falls under physics, while navigation, attacks and animation are suspended. Their previous component states are restored after thirty seconds. A repeat hit on an already stunned mannequin still consumes the additional toy and does not extend the timer.

The host validates grabs and swings, owns hit detection and the stun timer, and replicates carriers, swings, ragdoll poses, and despawns. Owning guests simulate a local visual toy for immediate camera response; those visuals cannot apply hits. Walls obstruct strikes. Players cannot combine carrying a monkey with carrying a mannequin or riding/pushing a cart. Knockdown, elimination or disconnect drops the held toy.

Network protocol version: **10**. Everyone must use the new build. Recreate the prefab, registry and scene configuration through Unity Pipeline with `Tools/Unity/MonkeyToySetup.cs`, entry `MonkeyToySetup.Build`. Gameplay checks and captures are in `ArtSource/MonkeyToy/`.

## Validation

Offline checks cover the hanging rig, retained misses, single-use hits against aggressive and ordinary mannequins, the actual thirty-second timer, and restored navigation. A macOS Editor host and standalone guest confirmed guest pickup, server hit detection, shared physical falls and toy despawns. A late spectator added no toy. The next round with three participants spawned exactly three fresh toys on all three peers. Both standalone clients completed without runtime exception log entries.

The current Windows package is `Builds/Svinki-Windows.zip`; its build and archive checks are recorded in `ArtSource/MonkeyToy/windows-validation.txt`. Runtime integration was tested on macOS; the Windows executable has not been run on Windows here.

For the orange version, single-hand grip and eased attack see [Emotes and plush](Emotes-and-plush.md).
