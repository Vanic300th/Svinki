# Friendslop playable lobby and lifts at both ends of a run

The repository's existing game is Svinki: clothing search, mannequins, carts, photographs and a final fashion show. Friendslop now has a separate playable social scene, `Assets/_Project/Scenes/Lobby/FriendslopLobby.unity`. The persistent, menu-only `Lobby.unity` continues to own FishNet/EOS. Session members spawn in Friendslop immediately after connecting. All members enter its freight lift; the server closes its doors, flickers the light, then uses the existing FishNet connection-scene pipeline to begin the current `SampleScene` run. A second freight lift at the factory entrance replaces the Ready button: all living participants enter, and a server countdown starts the existing final fashion presentation. Leaving or being knocked down cancels departure. After results, everyone returns to Friendslop, with fresh avatars including former spectators/eliminated participants.

This is a separate lobby integrated with the existing run, not a replacement horror gameplay mode. Future factory gameplay can replace the existing destination through the same scene-loading integration.
## Audit and implementation decisions

Unity 6000.6.2f1, URP 17.7, FishNet 4.7.3, EOS Plugin 6.2 and Input System 1.20. `NetworkLobby` owns session membership, reconnection reservations, phases and the clothing results. FishNet broadcasts carry session snapshots; owned player RPCs request gameplay operations. `Multipass` selects `FishyEOS` online and `Tugboat` for local regression tests. EOS guest authentication, lobby admission and transport were retained.

The old clothing pickup hides collected items and adds clothing to `PlayerOutfit`; it is not a general physical holding system. Existing mannequin/toy carrying is strongly typed and includes ragdoll/combat behavior. `LobbyProp` extends the camera interactor with general small props while retaining the existing E/LMB/RMB conventions, player-owned RPC route, view source and inventory/pickup behavior. `PlayerLobbyHands` represents those props without turning cups or basketballs into combat mannequins.

The scene setup retains all 157 existing factory scene network IDs, 96 clothes and three carts. The factory mannequin population is now doubled from 57 to 114: 80 neutral figures, 16 stalkers, 10 thieves and eight stationary head watchers. The 57 new actors retain the existing components and network authority; movable actors use the existing host-side round placement. Together with the 31 social NetworkObjects there are 245 unique scene IDs across both scenes. The single harmless lobby mannequin is unchanged. Session protocol version 24 requires all participants to update together; it includes the scene population, photo restore and media notification changes. Social objects are authored as scene objects; they do not require another spawnable-prefab registry or a new online framework.

## Layout

The room is 22 by 18 m, centred at (0, 0, -22). Cool fluorescent fixtures, warm lounge lights, concrete, rust/steel, six green lockers, a leather/fabric lounge and a pink jukebox follow the reference's zones. Geometry is deliberately placeholder art. There is a clear central circulation area. Lift and lockers are along the north side; lounge/TV along the west; basketball/darts along the east; hockey is off-centre; the drawing board is on the south wall. The new scene shows its entire room on the existing minimap (designer-configurable `StoreMap.VisibleRange`, while the factory retains its neighbourhood view) and a green L marker for the start lift. The factory minimap marks the return lift with L.

The room is physically separate from the factory, so no factory AI, clothes or carts load while waiting. Its cosmetic mannequin has no brain, throw component, colliders or combat audio. Factory navigation and original entrance geometry were restored; no lobby-specific NavMesh bake changes remain.
## Controls and authority

- E opens a locker/media/board/hockey panel or picks up a loose prop.
- LMB throws a held prop; RMB/E drops it. Existing clothing, camera, carts and toy controls stay available.
- Locker panels select a photograph from the player's permanent local SQLite archive and a mounting slot. Only the assigned owner can change its door or photo slots.
- Drawing uses LMB in the shared board panel, with Draw/Erase buttons. Whole-board clearing is host-only.
- Hockey claims a free paddle; LMB/mouse in its panel moves it within its own half. Closing releases the paddle. The server clamps speed and owns puck physics/goals.
- TV accepts YouTube links and direct compatible HTTP/HTTPS video links. The jukebox offers ccMixter CC BY song/artist search and track-page links; previous Audius links remain supported. Play/pause, TV seek/stop and jukebox skip change shared state; volume/mute affect only the local speaker.
- Every lobby member enters the lift. A three-second countdown closes its visual doors and flickers its light. Exit, knockdown, a new arrival or a missing participant cancels the countdown. Reconnection reservations prevent a disconnect from silently starting the run. The old menu Ready/Start requests cannot bypass physical occupancy. One host checks occupancy at 5 Hz and sends only state changes.
- The factory return lift completes the run after all living participants stay inside for three seconds. Legacy FinishReady/EndRound requests are ignored; the host derives readiness from physical occupancy. Eliminated players and spectators do not block extraction; a reserved disconnected living participant blocks departure until reconnect or reservation expiry. Automatic results on everyone's death remain.

The same lift-to-run and results-to-lobby cycle works offline. Esc opens the existing session menu in either playable scene. Pig appearance changes in the lobby update the existing avatar. Existing walking controls, chat, gestures and proximity voice also accept the playable lobby phase, through the same ownership/rate/alive checks. Outfit collection and combat requests remain limited to factory runs. Joining the lobby spawns a participant immediately; joining an active factory run retains the original spectator policy.
All gameplay requests go through `NetworkPlayer.Lobby` with ownership, phase/alive/knockdown, scene, proximity, wall obstruction, payload and rate checks. The client cannot submit a score. Basketball validates downward travel through upper then lower scoring gates; darts derive bull, sector, double and triple scores from authoritative impacts. Used darts can be picked up again or returned with the board's E action.

## Photo lifetime

Photo slots reuse the existing camera JPEG format, validation and bounded 1 KB packets. The camera automatically saves photographs to local SQLite; `P` → `ROUND / SAVED` switches between the latest 12 round photographs and the permanent archive. The archive and six pinned slots survive subsequent rounds and application restarts. Only the player's six pins are restored to the host, and manual changes require ownership, proximity and a clear interaction line. A late join obtains a bounded room snapshot. There are at most six pins per member, six members, and 32 KB per photo. Uploads have a one-second cooldown. See [Photo persistence](Photo-persistence.md) for retention and platform details.

`SessionLockerPhotoStore` remains the host's session cache and late-join source. `PhotoDatabase` stores the local album and slot assignments as SQLite BLOBs; it is separate from the shared round-results album and outfit checkpoint. Confirmed owner changes update the local database after the host's echo. No new account service was introduced. Photo restoration was introduced in protocol 22; the current media changes require protocol 24 on all participants.

## Media

`IMediaSourceProvider` resolves direct MP4/WebM/OGV/MOV video and MP3/OGG/WAV audio, YouTube video IDs, ccMixter track pages and Audius track IDs. The TV uses the official YouTube IFrame player inside a local CEF browser, rendered to its existing screen texture; a native player button permits playback when browser autoplay is blocked. The jukebox searches ccMixter using its official Query API. The server rechecks CC BY license, file format, size and metadata before enqueueing a stable track page. Each client resolves the page to a validated full MP3; author/featuring credits and source/license links are available in the panel. Empty search shows recent remixes; closing the panel does not stop music. Audius metadata checks remain for existing links. Spotify service pages remain unsupported. Extension acceptance does not guarantee a codec supported by every platform: preparation errors and timeouts are shown locally. Additional providers can use `MediaSourceProvider.Register` and `IMusicCatalogProvider`.

Each client loads its own media. FishNet sends URL/ID, title, contributor, version, play/pause, position and a synchronized tick-based starting time; it never transports video or audio. Direct video uses `VideoPlayer`/RenderTexture; direct/ccMixter/Audius audio uses `UnityWebRequestMultimedia` and a 3D AudioSource. YouTube uses the existing RenderTexture and browser-local playback controls; it pauses locally when the TV is out of view and catches up when visible again. Browser audio has distance/volume attenuation but does not enter Unity's AudioMixer or provide directional panning. Late join uses the same retained SyncVar/SyncList state and computes elapsed time from the synchronized FishNet tick. Direct drift checks run every three seconds; YouTube local API checks run at 0.5 seconds and browser frames are capped at 20 fps. These checks and frames are local and are not network messages. See [Online media](Online-media.md) for setup, controls, platform limitations and current validation.

HTTP/HTTPS only, absolute URI and host validation, no URL credentials, a 2048-character maximum, preparation timeout, audio byte limit, per-player control cooldown, twelve queued tracks, three outstanding tracks per player and duplicate/cooldown checks are enforced by the host. Jukebox completion advances only on the host. Both speakers use distance attenuation and local persisted volume. A quiet lobby background loop reuses the existing ambient track and Ambient mixer group (volume .22). `RoundBackgroundMusic` reads the already replicated TV/jukebox playback states twice per second locally. While either device plays, the loop fades out over one second and pauses; when both pause or stop, it resumes and fades back in. No additional network messages are sent. The factory background track retains its previous behavior.

## Limits and extension points

`LobbyActivityManager` schedules state/cleanup work at 10 Hz; it also interpolates prop visuals locally. Sleeping props send no unchanged poses. Off-map/out-of-bounds props return to their authored homes. Lobby props do not inherit the mannequin knockdown attacks.

`DrawingBoard` retains at most 512 commands / 8192 points, with at most 32 points per message and a per-player cooldown. Late join replays retained commands into a local texture; whole textures are not transmitted. Draw and erase share the ordered command history. The host can clear it at its cap.

`LobbyAnomalyManager` makes one choice between rounds at a 30% chance. `LobbyAnomalyEvent` allows additional effects; the first event changes the quiet mannequin's location. There is no periodic relocation while people are waiting.

Seated sofa animation is not implemented: the current controller has cart-specific riding but no generic seated pose. The couches are physical lounge props. Final art, additional anomaly effects and further official media providers remain extensions.

## Reproduction and validation

- `Tools/Unity/FriendslopSceneSetup.cs`: guarded extraction of the social room into a new scene, selective entrance/final-station restoration, original network-ID preservation and build registration.
- `Tools/Unity/FriendslopSceneCheck.cs`: separate-scene inventory, safe lobby, original factory population, scene IDs, scripts and lift approach clearance.
- `Tools/Unity/SocialLobbyPreview.cs`: roof-hidden layout capture of Friendslop.
- `Tools/Unity/SocialLobbyBuild.cs`: native development build including bootstrap, Friendslop and factory scenes.
- `Tools/SocialLobbyVideoFixture.swift`: local H.264 fixture, no external media dependency.
- `Tools/SocialLobbyNetworkCheck.py`: Host/Client/Late Join, ownership, board, props, media, run lift, retained match photographs, repeat transitions and solo regression. Temporary local HTTP/transport endpoints; restores preferences/checkpoint.

The earlier annex-authoring scripts are historical construction artifacts; their temporary reports were removed during cleanup. Use the Friendslop setup/check for the current topology.
Screenshots are saved under `ArtSource/SocialLobby`; the pre-change scene/scripts are in its `Before` directory. Development probes are compiled only in Editor/DEBUG and require the existing explicit `--session-test-path` opt-in. Actual EOS internet-session testing requires authenticated participants; local multiplayer regression uses the project's existing Tugboat path.

## Current files and validation

`Assets/_Project/Scenes/Lobby/FriendslopLobby.unity` contains the social room and run-start lift. `SampleScene.unity` replaces the entrance Ready station with a return lift and retains existing factory gameplay. `ProjectSettings/EditorBuildSettings.asset` registers the new scene. No new spawnable-prefab registry or networking framework was introduced.

`NetworkLobby.Friendslop` extends the existing partial session manager with physical lobby membership checks, avatar lifecycle and scene loading. The camera album now survives player destruction between factory and lobby; explicit next-run/session cleanup still clears it. Integration also updates existing session menus, help, HUD and lobby interaction phase validation. EOS authentication/lobby/transport implementation remains unchanged.

The temporary integration reports were removed during project cleanup. Native local transport validation is recorded as the regression runs. Actual EOS internet sessions require authenticated participants and are not claimed as tested here. The pre-existing missing hoodie1 prefab reference remains outside this task.

Visual checks: `layout-preview.png`, `lobby-first-person.png` and `final-show.png` under `ArtSource/SocialLobby`. Placeholder art and the persistence/media/sofa limitations described above still apply.

The native build succeeded. Its two reported scene errors are the pre-existing missing hoodie1 prefab reference, also present in the pre-change scene. Script compilation passes. The full Host/Client/Late Join regression passed: lobby movement/chat/gestures/voice relay, ownership and invalid-request rejection, shared drawing, server prop physics/cleanup, physical sports scores, direct media synchronization, reconnect, lift cancellation/run start, return-lift occupancy/cancellation and factory results, return to lobby, match-photo pinning and new-join photo recovery, repeated runs and offline transitions. Test-only physics teleports synchronize Rigidbody and Transform before simulation; these fixtures do not replace gameplay scoring. Preferences/checkpoint were restored and test players/HTTP server stopped.

To play in the Editor, start from `Assets/_Project/Scenes/Bootstrap/Lobby.unity`; connection or Play Solo loads Friendslop automatically. `FriendslopLobby.unity` is the designer's separate room scene. The original Windows build target/profile is restored after native test builds.

## Lobby polish validation (2026-10-10)

World-space legacy and TMP labels use dedicated depth-tested shaders, so walls hide upper room signs. `WorldTextDepth` keeps the correct atlas/material after localized font replacement without changing Canvas UI fonts or materials. Scene references retain both shaders for player builds.

`Tools/Unity/LobbyPolishSetup.cs` applies ambient wiring, world text materials and guarded/idempotent population doubling through the Editor. `Tools/Unity/LobbyPolishCheck.cs` checks rendered occlusion and Play Mode fade/pause/resume plus RU/EN font changes. The audio fixture injects received media states locally and disables downloads; it does not claim a new Host/Client transport test. `PhotoRoundCheck.Spawns` validates 64 seeds at the doubled population including room for six equipment spawns. Temporary polish reports were removed during project cleanup. No player build was produced for this polish pass; earlier native-build/network checks above describe the prior lobby integration.
