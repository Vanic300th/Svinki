# Emotes and orange monkey plush

All work is in the local `main` working tree. A Git stash safety snapshot preserves the earlier work before the branch move; the GitHub Desktop stash remains available too.

Hold **V** to open the six-sector wheel, move the mouse to Wave, Cheer, Clap, Dance, Laugh or Shrug, then release **V**. The center cancels selection. **Esc** or the right mouse button cancels without opening the pause menu. The wheel blocks movement, camera look, inventory, pickup, chat and flashlight input while open. Gestures animate the actual pig bones, last three seconds, and stop when moving, falling or taking an item. Players with occupied hands, in a cart, dead or spectating cannot start a gesture.

The owner requests gestures through an ownership-checked server RPC. The server validates round participation and free hands, limits repeated requests, and synchronizes a gesture counter plus its selection. Repeating the same gesture restarts it on every observer; the server clears expired gestures so newly joining observers do not replay an old gesture. Protocol version is **9**; every player should use the same new build.

The monkey is orange with a cream face, hands and feet, preserving its dark eyes. Only the toy copy of the animal mesh was recolored. Its left wrist is attached to the player's right paw; the other arm and body hang freely on thirteen physics bodies. The first person grip matches the pig's selected skin color. Hand targets and rotations follow the camera with damping; the plush has additional angular damping.

**E** picks it up. **LMB** starts an eased wind-up, forward swing and return lasting 0.84 seconds; impacts are evaluated by the server during the strike portion. **RMB / E** releases it. A miss retains the toy. One hit on any mannequin consumes it; an aggressive mannequin falls and remains inactive for 30 seconds. One toy spawns per round participant.

Runtime validation and screenshots are in `ArtSource/EmoteMonkey/`. The runnable Editor check is `Tools/Unity/EmoteMonkeyCheck.cs`. The Windows release archive is `Builds/Svinki-Windows.zip`.
