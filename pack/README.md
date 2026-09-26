# ForgePlanner

A crafting planner that lives inside Valheim. Press **F7**, build a shopping
list, and see exactly how much ore, wood and hide you need — including the
smelting, the coal, and which station you have to stand at.

Everything is read from your running game: recipes, conversions, item names,
icons, trader price lists and your language. Nothing is hardcoded, so the
numbers keep matching after a game patch instead of going quietly stale.

![The planner open in game](https://staticdelivery.nexusmods.com/mods/3667/images/3879/3879-1789882735-361758933.png)

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
- **Search and eight tabs:** All, plus Weapons, Armour, Tools, Food, Materials,
  Buildings and Other. Search works in both English and Russian whatever
  language the game is running in.

![The plan and the totals](https://staticdelivery.nexusmods.com/mods/3667/images/3879/3879-1789882735-33851083.png)

**Pin the plan and close the window.** A copy of it stays on screen in a small
panel and fills in as you gather - `1 / 10`, so you can see both how far along
you are and how far there is to go. It counts your backpack only: the window
counts nearby chests as well, but out in the field the question is whether you
have it on you, not whether it is somewhere at home. It is a copy on purpose:
wiping the plan to price up something else does not wipe what you pinned. Clicks
pass straight through it, and you can drag it somewhere else while the Esc menu
is open.

**Hover a row to see the item card.** Stats of the item at the quality you
picked, side by side with what you are wearing in the same slot: green with a
`+` where it is better, red with a `−` where it is worse, `=` where they match.
Damage, armour, block, resistances, durability, weight, movement penalty - the
same numbers the game shows, computed by the game itself. Below them, the recipe
with what is already in your backpack. For things you cannot wear, the card is
just the recipe. Comparison can be switched off in Settings.

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

![Settings](https://staticdelivery.nexusmods.com/mods/3667/images/3879/3879-1789882739-1718361296.png)

## Usage

Press **F7** in a loaded world, or use the **ForgePlanner** button on any
crafting station's panel. The cursor is released while the window is open, and
the game behaves exactly as it does when you press `E` at a workbench: no
swinging, building, walking or hotbar switching by accident. Press **F7** or
**Esc** to close.

![The button on a station panel](https://staticdelivery.nexusmods.com/mods/3667/images/3879/3879-1789882739-1591389284.png)

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
| `Pin` | `Enabled` | `true` | Keep pinned materials on screen after the planner is closed |
| `Pin` | `Corner` | `TopRight` | Which corner of the screen the pinned list sits in |
| `Pin` | `OffsetX` | `42` | Distance from the side of the screen; written by dragging the panel |
| `Pin` | `OffsetY` | `268` | Distance from the top or bottom; the default clears the minimap |
| `Pin` | `RefreshSeconds` | `1` | How often the remaining amounts are recounted |
| `Pin` | `MaxRows` | `8` | How many materials to list before collapsing the rest |
| `Tooltip` | `Enabled` | `true` | Show the item card when hovering a row in the planner |
| `Tooltip` | `Compare` | `true` | Compare the hovered item with what you have equipped |
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

## Licence

MIT. See `LICENSE`. Not affiliated with Iron Gate Studio.

The window borrows the game's own font, panel and button art at runtime, so it
matches your UI scale and your language. It ships no Valheim assets of its own —
the few icons it does carry are its own, and `LICENSE` says which.
