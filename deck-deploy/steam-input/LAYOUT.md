# Steam Input layout — 7th Heaven Deck mode

Two action sets, per `SPEC.md` → "Deck deployment": a keystroke set for the Deck-mode UI
and a gamepad-passthrough set for the game. One flat keystroke layout for the whole
session would break FF7's own controller input — the game runs as a child process of the
same Steam entry, and 7th Heaven's in-game controller code needs the raw pad.

`controller_neptune_7th_heaven_deck_mode.vdf` is a hand-authored Steam Input **template**
for the Deck's built-in controller (`controller_neptune`). It was written against the
format of MateriaForge's own shipped template and community VDFs, but **it has not yet
been validated on hardware** — treat every row of the tables below as an item on the
on-Deck checklist (../README.md).

## "Launcher" action set (active in the Deck-mode UI)

Emits exactly the keys `KeyboardInputSource` maps (see its class remarks), so the pad
drives the same logical command layer the keyboard does.

| Input | Emits | Deck UI meaning |
|---|---|---|
| D-pad | Arrow keys | Move focus |
| Left stick | Arrow keys | Move focus |
| A | Return | Activate / toggle |
| B | Esc | Back / cancel |
| X | R | Reorder mode |
| Y | O | Options (search in catalog) |
| LB | Q | Previous section |
| RB | E | Next section |
| LT | Page Up | Page up |
| RT | Page Down | Page down |
| Menu/Start | P (held while pressed) | Play; the app times the hold for the variant picker |
| View/Select | Delete | Uninstall focused mod (Installed mods), behind a confirm |
| Right trackpad | Mouse + left click | The UI has full mouse support |
| Back grips (L4/L5/R4/R5 lower pair) | Switch to "Game" set | See "Switching" below |

## "Game" action set (active once FF7 is running)

Mirrors MateriaForge's stock config ("Gamepad with Mouse Trackpad + Click"): full XInput
passthrough — A/B/X/Y, d-pad, both sticks + clicks, analog triggers, bumpers,
Start/Select — plus right-trackpad mouse with left click. The raw pad therefore reaches
FF7 and 7th Heaven's injection-side controller code exactly as stock does today. The
back grips switch back to "Launcher".

## Installing and selecting the layout

`deploy.sh` copies the VDF into `<steam root>/controller_base/templates/` — the same
mechanism MateriaForge uses for its own config. Manual equivalent:

```sh
cp controller_neptune_7th_heaven_deck_mode.vdf ~/.steam/steam/controller_base/templates/
```

Then restart Steam (or re-enter game mode), open the 7th Heaven entry → controller
settings → layout picker → **Templates** → "7th Heaven Deck mode".

## Switching between the action sets

Three mechanisms, in order of preference:

1. **`ISteamInput::ActivateActionSet` (planned, not yet built).** The app calls it right
   before spawning FF7.exe and again on return. Requires the Steamworks SDK link-up —
   promoted from stretch goal to load-bearing in `SPEC.md`. Until that lands, use 2/3.
2. **Back-grip binding (in this VDF, needs on-Deck verification).** Both lower back
   grips are bound to `controller_action CHANGE_PRESET` in each set. The exact
   argument convention for `CHANGE_PRESET` (`<preset> 0 0`, 1-based) is drawn from
   community configs, not official documentation — if it does not work on hardware, fix
   it in the Steam layout editor (add "Change action set" as a button command, which the
   editor exposes natively) and re-export, or fall back to 3.
3. **Steam overlay (always works).** Steam button → controller icon → switch action set.
   Clunky but functional; this is the documented interim UX if 2 fails.

Remember the flow: **Launcher set** while browsing/configuring mods → press Play →
**switch to Game** (ideally automatic) → play FF7 → quit game → **switch back to
Launcher** for the post-game UI.

## Known unknowns (verify on hardware, fix here if wrong)

- `CHANGE_PRESET` argument convention (see above).
- Back-grip input names (`button_back_left` / `button_back_right`) — if the grips do
  nothing, the names may differ for neptune; rebind in the layout editor and re-export.
- Whether a template VDF with two presets surfaces both action sets in game mode's
  simplified controller UI (it does in the full desktop-style editor).
- Trigger threshold feel for Page Up/Down (currently default `Full_Press`).
