# Outfit rating: original and expanded wardrobe

All 33 pig-rigged garments share the same rating system. The original 15 items and six reference outfits are retained. The sixteen supplied examples add thirteen unique outfits: Street, Desert and Classic repeat earlier combinations. The combined catalogue therefore contains **19 reference outfits**, without counting duplicates twice.

The reference image identifies models by filename. These keys are used directly, including `hoodie4`, `hoodie5`, `pants6`–`pants9`, and `sweater2`, `sweater4`–`sweater6`; folder numbers are not model numbers.

| Supplied example | Top | Trousers | Headwear | Footwear |
| --- | --- | --- | --- | --- |
| Street | hoodie1 | pants2 | hat1 | sneakers2 |
| Desert | shirt3 | pants3 | cap1 | sneakers3 |
| Commando | sleeve1 | pants7 | cap1 | sneakers3 |
| Classic | shirt1 | pants1 | hat2 | loafers1 |
| Sportswear | shirt2 | pants6 | cap2 | sneakers4 |
| French | sweater1 | pants8 | hat2 | loafers1 |
| Army | hoodie4 | pants7 | cap2 | sneakers4 |
| Baron | hoodie5 | pants8 | hat3 | loafers2 |
| Gardener | shirt6 | pants9 | hat3 | loafers2 |
| Forester | shirt4 | pants6 | hat1 | loafers1 |
| Tiger | shirt5 | pants1 | cap1 | sneakers3 |
| Sakura | shirt7 | pants9 | hat2 | sneakers2 |
| Sky | sweater2 | pants2 | hat2 | sneakers4 |
| Autumn | sweater4 | pants8 | cap2 | loafers1 |
| Chess | sweater5 | pants1 | hat1 | sneakers3 |
| Grandma's Garden | sweater6 | pants9 | hat3 | loafers2 |

The previous Camouflage casual, Cozy and Sport combinations remain alongside these. All earlier generated clothing definitions and the four original starter definitions are linked to their matching rating profiles. Existing inventories and saved item IDs remain recognised.

## Calculation

The four slots are headwear, top, trousers and footwear. Repeating a slot cannot inflate the score. Pairs present in any reference outfit receive full compatibility and pattern scores. Other pairs use differences in dressiness, warmth and colour. Floral, checked and speckled fabrics now count as patterns alongside stripes and camouflage. Plain garments help balance a bold print; unrelated bold prints reduce pattern harmony.

Pair weights are unchanged: top/trousers 30%, top/footwear 22%, trousers/footwear 16%, headwear/top 14%, headwear/trousers 10%, headwear/footwear 8%.

Quality = 65% compatibility + 20% colours + 15% patterns. Final score = quality × rated slots / 4, rounded to 0–100. One, two or three pieces remain capped at 25, 50 or 75. Unknown garments receive no invented rating. The examples guide successful combinations; players can mix the original and expanded wardrobe freely.

The host captures the final outfit and calculates the scores. All participants receive the same result snapshot and dressed pig on the runway. Eliminated pigs appear as ghosts. Session protocol **17** requires matching game versions.

## Store and setup

The existing 96 concealed search anchors now contain every one of the 33 garments, with two or three copies of each. Their positions are shuffled each round. The new garments are registered for network pickup, dropping, inventory replication and runway display. The in-game F1 guide explains the combined rating.

`Assets/Resources/OutfitRating.asset` contains profiles, aliases and reference outfits. **Svinki → Clothing → Combine original and expanded rating examples** merges the default collection with existing profiles and examples, retaining tuned metadata and avoiding duplicate combinations.

`Tools/Unity/CombinedWardrobeSetup.cs` is the isolated Editor setup runner: imports the full wardrobe, updates the existing search anchors and verifies asset/network integration. It changes the saved level; run it outside Play Mode. `Tools/Unity/OutfitRatingCheck.cs` checks all **2,800** full outfits, the 19 references, original definitions, mixed wardrobes, input-order independence, incomplete outfits, duplicate slots, unknown items, captured inventories and network JSON round-tripping. Reports and the front/back reference sheet are in `ArtSource/OutfitRating`.

The score screen includes Photo albums. Everyone can select a photographer and browse their latest 12 round images, including photos taken by eliminated players. The album view returns to the scores without starting another round.
