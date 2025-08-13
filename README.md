# Jobstone Necklace Auto‑Switcher (Dalamud plugin)

> **Note:** This is my first attempt at making a mod/plugin — feedback is welcome!

Automatically switches the **Jobstones Necklaces** pendant to match your **current job** whenever your job (or soul crystal) changes. No manual toggling required.

> **Requires:**
> - [Penumbra](https://github.com/xivdev/Penumbra)
> - [Jobstones Necklaces by Tamrine](https://www.xivmodarchive.com/modid/79598)

## Why?
If you love the Jobstones Necklaces mod but forget to change the pendant when you swap jobs, this plugin flips the right option for you using Penumbra’s IPC and triggers a redraw so the change is instant.

## Features
- Detects job/gear changes and updates the pendant automatically
- Per‑job mapping to the mod’s option names
- Optional glow toggle (if exposed by the mod)
- Choose which Penumbra collection to modify (Default or Character)
- Safe fallbacks if the mod is missing/disabled

## Installation (manual)
1. Install **Penumbra** and the **Jobstones Necklaces** mod.
2. Download the latest `JobstoneNecklaceSwitcher` package from **Releases**.
3. In Dalamud dev settings, enable loading plugins from disk and point to the extracted folder; or drop it into your DevPlugins directory.
4. Launch the game and run `/jsneck` to open the config.

## Configuration
1. Set **Target Collection** to the Penumbra collection where Jobstones Necklaces is active.
2. Confirm the **Option Group** and **Option Names** match your mod (e.g., group `Pendant`, option `PLD`, `VPR`, etc.).
3. Toggle **Glow** if desired.
4. Swap jobs to verify the pendant updates.

## Compatibility & limitations
- Works with all races/genders supported by the underlying mod.
- If you’re not wearing Wayfarer/Byregotia necklaces at that moment, changes are harmless but won’t be visible.
- If the mod renames options in a future update, just adjust the mapping in the plugin config.

## Credits & attribution
- **Original Mod:** [Jobstones Necklaces by Tamrine](https://www.xivmodarchive.com/modid/79598) — [Author page](https://www.xivmodarchive.com/user/90096)
- Quoted permission from the author’s page:
  > *“Use, port, edit, remake, and repost my mods to your heart’s content. I see modding as a discipline based on sharing freely.”*
- Thanks to **K'hera** for giving me the idea.
- This plugin does **not** bundle any mod assets; it merely toggles options via Penumbra IPC.

## License
MIT — see [LICENSE](./LICENSE)
