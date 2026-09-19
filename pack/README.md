# ForgePlanner

A crafting planner that lives inside Valheim. Press **F7**, build a shopping
list, and see exactly how much ore, wood and hide you need — including the
smelting, the coal, and which station you have to stand at.

Everything is read from your running game: recipes, conversions, item names,
icons, trader price lists and your language. Nothing is hardcoded, so the
numbers keep matching after a game patch instead of going quietly stale.

![The planner open in game](https://raw.githubusercontent.com/Biozip/ForgePlanner/main/media/01-planner.png)

## What it does

- **Plan several items at once.** Three bronze picks and two maces, not one
  item at a time. Double-click a row to add it.
- **Quality levels 1–4.** The upgrade surcharge is taken from the game, not
  guessed. Valheim doubles it per level (1, 2, 4), which is where most
  spreadsheets and wikis get it wrong.
- **Raw materials or recipe materials.** Either break everything down to ore,
  wood and hide, or stop at what the workbench actually asks for.
- **Smelting included.** Coal, smelting time and how many loads each station
  needs.
- **Counts what you already have.** Your inventory plus nearby chests.
- **Tells you which stations you need** — and whether one is standing nearby,
  how far, in which direction, and at what level. If the one you have is too
  low, it says which level the plan wants. If there is none, one button adds
  the station itself to the plan.
- **Names the trader for anything you cannot find.** Some materials drop
  nowhere; the line says who sells it and for how much.
- **Buildings too.** Walls, floors, workstations — the hammer menu is in the
  same catalogue.
- **Filter by biome**, with the same colours as the website.
- **Search and seven sections:** All, Weapons, Armour, Tools, Food, Materials,
  Buildings, Other. Search works in both English and Russian whatever language
  the game is running in.

![The plan and the totals](https://raw.githubusercontent.com/Biozip/ForgePlanner/main/media/02-plan-totals.png)

### Spoiler-free by default

A planner opened in your first hour would otherwise retell the whole game: a
thousand entries with flametal weapons at the top. So a biome stays locked
until the boss before it falls. Meadows, Black Forest and Ocean are always
open — you get there on your own.

Locked biomes dim rather than disappear, and the whole thing is one switch in
Settings if you would rather see everything. If you have already beaten the
bosses, you lose nothing: it is all open.

The victory keys are read from the boss prefabs themselves, so the mod does not
carry a list of boss names that a future update could quietly invalidate.

![Settings](https://raw.githubusercontent.com/Biozip/ForgePlanner/main/media/03-settings.png)

## Usage

Press **F7** in a loaded world, or use the **ForgePlanner** button on any
crafting station's panel. The cursor is released while the window is open, and
the game behaves exactly as it does when you press `E` at a workbench: no
swinging, building, walking or hotbar switching by accident. Press **F7** or
**Esc** to close.

![The button on a station panel](https://raw.githubusercontent.com/Biozip/ForgePlanner/main/media/05-station-button.png)

## Configuration

`BepInEx/config/dev.forgeplanner.forgeplan.cfg`, written on first run.

| Section | Key | Default | Meaning |
| --- | --- | --- | --- |
| `General` | `Open` | `F7` | Hotkey that opens the planner |
| `General` | `Russian` | `false` | Window labels in Russian |
| `General` | `HideUnreached` | `true` | Lock biomes until their boss is beaten |
| `Stock` | `CountChests` | `true` | Count nearby chests, not just your backpack |
| `Stock` | `Radius` | `20` | How far a chest counts as yours, in metres |
| `Station` | `Button` | `true` | Show the ForgePlanner button on station panels |
| `Station` | `Corner` | `BottomRight` | Which side of the panel the button's shelf sits on |
| `Station` | `OffsetX` | `14` | Shelf offset from the panel edge, in points |
| `Station` | `DropDown` | `0` | How far below the panel edge to drop the shelf |
| `Debug` | `SelfTest` | `false` | Dump the parsed catalogue to the log on world load |

The three `Station` keys are there for repair rather than taste: the crafting
panel belongs to the game, and a future Valheim update may move it. If the
button ends up in the wrong place, it can be put back without a new release of
the mod.

## Companion website

[forgeplanner.pages.dev](https://forgeplanner.pages.dev) runs the same
calculation in a browser, for planning before you launch the game.

The two are kept honest about each other: the mod reads the live game, the site
reads extracted game data, and a parity harness compares the catalogue,
conversions and per-level costs entry by entry. Four real calculation bugs were
found that way in a single day — potential idols counted as ingredients, bronze
taken from scrap instead of ore, a five-ingot recipe beating the one-ingot one,
and a wrong upgrade curve.

Progression is the one thing the site cannot do: it does not know which bosses
you have beaten.

## Requirements

- Valheim (tested against 1.0.15)
- [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) 5.4.2350 or newer

## Manual installation

Drop `Forgeplan.dll` into `BepInEx/plugins/`.

![About](https://raw.githubusercontent.com/Biozip/ForgePlanner/main/media/04-about.png)

## Licence

MIT. See `LICENSE`. Not affiliated with Iron Gate Studio.

The window borrows the game's own font, panel and button art at runtime, so it
matches your UI scale and your language. It ships no Valheim assets of its own —
the few icons it does carry are its own, and `LICENSE` says which.
