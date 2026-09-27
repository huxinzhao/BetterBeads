# Better Beads 0.16.36-Lite

Create framed wall art, floor ornaments, swords, daggers, and hammers at a bead workbench.

## Languages

English and Chinese are included. The mod follows the game's language: Chinese uses i18n/zh.json; English and other languages without a translation use the English fallback in i18n/default.json. Choose your language on the game's title screen before loading a save. Existing design and item names written by players are not translated or renamed.

## Installation

Requires PC Stardew Valley 1.6 and SMAPI 4.5.1 or a newer compatible version. Extract the BetterBeads folder into Mods and launch through SMAPI. Lite and the full edition share an ID: install only one. When updating, close the game, back up your save and personal config/imports files, then replace the old mod folder. Generic Mod Config Menu is optional.

## Getting started

Reach Foraging level 2. Robin will send a letter, and her shop will start selling the Bead Workbench recipe. Craft and place the workbench, then select a product type and size, place beads, or open a template from My designs. You can also import PNG or JPG reference images. New designs are named on their first save or craft.

In My designs, choose Share to copy a selected design code, import one from the clipboard, or export a printable SVG color chart. Codes preserve the exact bead colors, canvas, product type, and weapon metal; importing opens a separate unsaved design that you name on first save. The chart numbers each placed bead and lists exact RGB colors with the nearest MARD 221 reference code. Charts are saved to the mod's `exports` folder. If the system clipboard is unavailable, Copy saves the code there as a `.txt` file instead.

Wall art and floor ornaments support 16×16 and 32×32 canvases. They use 1 Wood per 4 placed beads, rounded up. Swords, daggers, and hammers support 16×16, 24×24, and 32×32. The metal bar cost by size is sword 4/6/8, dagger 3/5/6, hammer 6/9/12. Larger weapons deal 10%/20% more damage. New weapons use the longest connected part of the design for attack reach (0.5–2 times normal) and the total placed bead count for Speed: 1–64 beads have no penalty, 65–256 have −1, 257–576 have −2, and 577 or more have −3. For swords, Swing direction → 45° correction keeps the original pixel art in menus and previews and adjusts only its angle during the vanilla swing. The artwork still follows the game's swing animation and will not stay upright in every frame. Older crafted weapons keep their existing stats and reach. Place at least one bead to craft. Materials come from your backpack first, then ordinary chests within five tiles on each axis on the same map.

Framed bead paintings can be given to villagers as liked gifts. A confirmation shows the painting and its appraised price. Once friendship reaches six hearts, a supported villager displays the latest gifted painting at home starting the next day. Paintings given earlier are remembered. The display is decorative and cannot be reclaimed. The editable `assets/gift-gallery.json` file lists supported homes and villagers; their order chooses wall slots, and optional `WallSlots` values can override individual positions. If the current wall or room is unsuitable, that display is skipped while its gift record remains saved.

Click Fuse to review and confirm the cost. Successful crafting adds the item to your backpack, saves the design, and shows your creation. Save the game normally to persist progress. Wall art hangs on walls; ornaments stand indoors or outdoors. New products receive a fixed sale appraisal when crafted: 95% of appraisals are 0–150% of the materials' base sell value, peaking at 80%; 4% are 151–499%; and 1% are 500–1000%. Each piece at 500% or more has a 1% chance to catch a mysterious collector's eye, increasing that piece's sale price by a randomly chosen 10–100 times and prompting a letter the next day. Collector pieces made in earlier versions keep their original price. Existing pieces without an appraisal keep their previous double-material price. Creative mode is off by default; free creative products sell for zero.

## Controls and colors

- Left click: place; right click: erase.
- Ctrl+Z / Ctrl+Y: undo / redo; Ctrl+S: save.
- Wheel: zoom; middle-drag or Space + left-drag: pan; Home: fit canvas.
- Bead colors → Tray colors: customize 20 favorite colors without repainting existing beads.
- Bead colors → Work colors: pick a color used in the design, preview a replacement, and Apply. One undo restores it.
- On narrow buttons, Cu / Fe / Ir mean copper / iron / iridium; hover to see the full name.

MARD 221 codes are approximate reference labels, not a guarantee of a physical bead match. Import supports PNG/JPG up to 20 MB, 4096px per side, and 16 million pixels. Windows has a file picker; the imports folder is the fallback. Chart grids, color-code text, and complex background removal are not supported.

## Weapon appearances

Use the vanilla forge with weapons of the same type. The left weapon keeps its stats; the right provides its current appearance and is consumed along with 10 Cinder Shards. Scythes are unsupported. The Galaxy Sword template provides a design, not its vanilla stats.

## Scope and troubleshooting

Single-player only. No commissions, story quests, or clothing crafting in Lite. Multiplayer, mobile, and other mods' custom weapon rendering are not supported or verified. Report the mod version, SMAPI log link, and reproduction steps when reporting a problem.

This release adds large bead weapons. Existing 16×16 weapons keep their saved combat values. Forge appearance swaps use the left weapon's combat reach; a large appearance on a vanilla weapon grants no extra reach. In-game swing alignment, hitboxes, and interaction still need actual gameplay verification. No game process was started or Mods/save files modified during preparation.

The Chinese guide follows below.
