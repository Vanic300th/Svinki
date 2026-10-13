# English and Russian

Open Settings / Настройки in the main menu or pause menu, then choose English or Русский. Changes apply immediately, including panels opened later. The selection is local to each player, survives restarting the game, and defaults to English when no preference exists.

Menus, pig customisation, HUD and interaction prompts, inventory and cart storage, emotes, voice status, clothing names, outfit advice, photo albums, all five F1 guide pages, and store signs use Unity Localization. Nicknames, lobby codes, chat messages and network snapshots remain literal. Each client translates the host's outfit results locally.

The reviewed catalogue is `Tools/Localization/en-ru.json`; `GameLanguageSetup.Apply()` updates the Unity tables, legacy English entry IDs, locales, startup selector and bilingual font. `Assets/Resources/GameLanguageIndex.json` contains the source matching index, not Russian translations. Translation assets and the font are Addressable; player builds rebuild their content automatically.

Verified in Unity Play mode: language buttons switch English/Russian, a fresh startup restores Russian, all five Russian guide pages fit with Cyrillic glyphs, the photo album fits, inactive HUD labels initialise safely, clothing/rating templates translate and a nickname such as Hat stays literal. All 1760 table values are nonempty. Evidence and screenshots are in `ArtSource/Localization`.

Windows Addressables content built successfully during runtime verification. A final rebuild after adding the minimap labels was blocked by newly added lobby scripts referencing missing `LobbyPanel` and `PlayerLocker` types (`DrawingBoard`, `LobbyMediaPlayer`, `LobbyActivityManager`). Those independent lobby changes were preserved. This check did not produce a new standalone game release.
