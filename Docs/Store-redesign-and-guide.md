# Store redesign, minimap and How to Play

The store keeps its expanded 13,087.5 m² floor area, but its north wing now has a stepped outline and offset passageways rather than a narrow rectangular grid. Thirty departments have different widths, coloured floors, angled dividers, curved displays, benches and stock cabinets. The scene retains three carts and twelve aggressive mannequins.

All 96 clothing positions are authored search locations, including low shelves, open cabinets, positions behind screens and the raised bridge. RoundClothingLayout still shuffles the existing networked pickups; the editor setup preserves their scene IDs. Navigation is rebaked after the hiding fixtures are placed.

The minimap panel remains 340 × 400 reference pixels, with a 315 × 315 floorplan. Its viewing range is reduced by 70%, showing approximately 53 × 53 metres around the player instead of the entire store. It follows the current spectator target when spectating and keeps north at the top. Player arrows are 21 pixels. Each player has a distinct host-assigned colour: coral, cyan, lime, purple, gold or pink. Their arrow and through-wall nickname use the same colour on every client, including their own map. Colours remain assigned between rounds and during the reconnect grace period; leaving releases the slot without changing anyone else's colour. Yellow squares are carts and green R marks the entrance when they are in view. The floorplan and markers are clipped to the map viewport. Rotated dividers are drawn at their actual angle. The pig portrait sits below the map.

How to Play is shown before the first normal solo, create-lobby, continue or join action in each game launch. Five tabs cover movement/HUD, clothing/round rules, carts/toys, friends/danger, and cameras/photos. It includes minimap zoom, matching player colours, random spawns, K to equip the camera, low-quality pictures and P to browse the album. Continue performs the pending action; Escape cancels it. F1 and the menu’s How to Play button reopen the guide. The guide blocks movement, interaction, chat, voice capture, emotes and spectator switching while open. Online rounds continue while it is open.

The READY wall button sits three metres further back along the entrance's left wall, away from the player spawn. Its gathering area moves with it; the minimap R follows the button. The guide describes its new location.

Network protocol is 13 after relocating the shared finish button and gathering area. All players should use the latest build for matching level positions, controls and player colours.

## Validation

- Saved scene: 30 connected departments, original expanded area, three carts, twelve attackers and unique network scene IDs.
- All 96 clothing locations have a complete navigation path and an unobstructed pickup ray within interaction range.
- Twelve shuffled layouts passed 1,152 physical reachability checks.
- Real camera aim and E input collected a hidden garment and equipped it in solo mode.
- Initial whole-store view, protocol 11 Editor host and Mac development client: both maps showed the local orange marker and remote cyan marker more than 100 metres apart. All 96 shuffled pickup poses matched across peers.
- Local zoom view: two native network clients retain the 315 × 315 floorplan, keep the player centred across three map locations, show nearby friends in cyan and hide distant markers. Screenshots and runtime evidence are in ArtSource/MinimapZoom.
- Player colours: six native clients receive six distinct colours; every client displays the same map colours, and visible nicknames match their map markers. Reconnecting and moving to the next round preserve colours; a leaving player does not recolour the remaining players. Evidence is in ArtSource/PlayerColours.
- Final native Mac build: F1 opens the guide during a round, the backdrop covers the HUD, and Escape restores control without opening pause.
- Windows x64 release ZIP passed CRC, executable architecture and required-file checks; development-only test code is absent.
- Four guide pages passed text bounds/overflow checks. Deferred start, Continue, F1 reopening and Escape cancellation passed with one EventSystem.

Verification scripts are in Tools/Unity/StoreRedesignSetup.cs and StoreRedesignCheck.cs; evidence is in ArtSource/StoreRedesign.
