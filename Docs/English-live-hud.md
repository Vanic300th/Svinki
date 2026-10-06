# English interface and live pig HUD

The game now uses English for its menus, appearance choices, gameplay prompts, item names, inventory, round readiness, runway presentation, outfit ratings and connection errors. Player-authored nicknames and chat are preserved. Clothing asset IDs and checkpoint IDs are unchanged.

`MannequinWardrobe` retains its existing scene/network bindings but renders the `Pigs/PigAvatar` prefab. Each frame it copies the local player's appearance and rig pose after player animation, cart poses and ragdoll updates. Garments use the same pig skeleton binding and leg masking as the world character. The portrait has its own lights, camera framing and UI layer, so it shows the complete pig even in first person. Camera framing follows the head, hands, hips and feet during movement or a fall.

English is the startup locale. `Assets/Localization/GameEnglish_en.asset` contains the English authoring reference, while existing runtime code composes its English strings directly. `Tools/Unity/EnglishGameSetup.cs` updates serialized display text and the portrait through Unity Editor APIs. Its translation map is `Tools/Unity/EnglishGameStrings.tsv`.

Verification: `EnglishHudCheck.Run` starts an isolated offline run, checks all 282 English entries and every TMP label, equips all four clothing slots, checks removal/restoration, compares the source and HUD bones during movement and crouching, and captures inventory, runway and results screens. Screenshots and the result are under `ArtSource/EnglishHud`. A local FishNet host was also checked with the player's original appearance and outfit restored.
