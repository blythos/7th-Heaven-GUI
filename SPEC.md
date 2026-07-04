# 7th Heaven — Deck controller UI

## What this is

A fork of `tsunamods-codes/7th-Heaven` (the WPF mod manager for the PC version of Final
Fantasy VII), maintained at `github.com/blythos/7th-Heaven-GUI`, adding a fullscreen,
controller-first interface designed for the Steam Deck. The existing desktop UI is
untouched; this adds a parallel "Deck mode" view layer on top of the same underlying
ViewModels and engine.

This is a personal project, built primarily for the author's own use. It may later be
proposed upstream as a PR, but should not assume that outcome — it should stand alone as
a usable fork regardless of whether it's ever merged.

> **This spec has been verified against the real source tree.** Where a claim about the
> codebase appears below, it reflects the actual code as of the review, not an assumption.
> Sections that were found wrong in an earlier draft (notably the mod-options renderer and
> the controller-input model) have been corrected. If you find the code disagreeing with
> this document, trust the code and flag the discrepancy — do not silently implement around
> it.

## How Deck mode is entered

Deck mode is an **additive extension**, not an edit of the existing UI:

- **All Deck code lives in new files under `AppUI`** (new `Windows/`, `UserControls/`,
  `ViewModels/`, `Classes/` files under a `Deck/` sub-namespace). It lives inside the
  `AppUI` assembly specifically because several existing ViewModels and members are
  `internal` (e.g. `MainWindowViewModel.LaunchGame`, `GLSettingViewModel`) and are only
  reachable from within that assembly. A separate exe/assembly would require
  `InternalsVisibleTo` or public-ising members — edits we are trying to avoid — and would
  have to re-implement the existing app bootstrap.
- **Entry is via a launch flag: `7thHeaven.exe --deck`.** The *only* permitted edit to an
  existing file is a single additive branch in application startup (`AppUI/App.xaml.cs`):
  if `--deck` is present, open the Deck shell window instead of `MainWindow`. All the
  existing initialisation (Sys, profiles, catalog, services) runs unchanged first.
- Steam / MateriaForge launch the same binary with `--deck` appended (see "Deck
  deployment").

## Non-goals (explicitly out of scope)

- **Do not touch the game-injection logic.** `AppWrapper` (Win32 process hooking —
  `AppWrapper/Win32.cs`) and `AppCore/RegistryHelper.cs` target `net10.0-windows7.0`, use
  Win32/registry APIs, and hook the running FF7 process. They are not portable and are not
  part of this project.
  - **Nuance:** "do not touch AppCore" is scoped to that *game-injection* logic. `AppCore`
    also contains the domain model the UI must bind to (`Settings.cs`, `Mod.cs`,
    `Profile.cs`, `Sys.cs`, `LaunchSettings.cs`, `ConfigSettings.cs`). **Additive** changes
    to data/settings types are permitted where required — specifically adding the
    "default play command" setting (see "Play flow"). Do not modify existing behaviour or
    injection code.
- **Do not fork or modify MateriaForge.** MateriaForge (`github.com/dotaxis/MateriaForge-rs`)
  handles the Proton prefix, runner selection, and launch configuration on the Deck. This
  project produces a custom 7th Heaven binary that gets *overlaid* into a
  MateriaForge-managed install (see "Deck deployment"). MateriaForge's own source is never
  edited.
- **CRT shader support is a separate project.** Not in scope here.
- **No native Linux rewrite.** The app remains WPF/.NET, built as a Windows binary, and
  runs on the Deck via Proton exactly as stock 7th Heaven does today.

## Repo setup (do this first)

1. Confirm the working branch is `deck-controller-ui` (already created and pushed to the
   author's fork, `github.com/blythos/7th-Heaven-GUI`), not `master`.
2. **Establish a clean build of the *unmodified* fork on Windows before changing
   anything.** The solution has native/toolchain dependencies (vcpkg via
   `Directory.Build.props`; `AppProxy`; a bundled `DS4WindowsCore.dll`) and targets
   `net10.0-windows7.0`, so a working build environment (Visual Studio with the workloads
   in `.vsconfig`, or the .NET 10 SDK plus native prerequisites) must be confirmed first.
   If upstream does not build cleanly, stop and resolve that before any feature work.
3. Add a short note near the top of `README.md` (or a new `DECK_UI.md` linked from the
   README) stating: this fork adds an experimental controller-first fullscreen mode for
   Steam Deck; it is a personal project, not affiliated with or endorsed by the Tsunamods
   team. Commit this as the first change on the branch.
4. Ensure a `CLAUDE.md` exists at the repo root (provided alongside this spec) describing
   the codebase structure and the verified ViewModel shapes.

## Primary dev/test loop

**Build and test on Windows first, for everything except Deck-specific concerns.** WPF
builds and runs natively on Windows.

- Because navigation is **keyboard-first** (see "Input architecture"), the entire UI is
  testable on Windows *with just a keyboard* from the first milestone — no controller
  required to make progress.
- A physical Xbox or PlayStation controller is then used to test the controller-mapping
  layer once it exists. **Note:** WPF has no native gamepad input, and the Deck presents
  input through Steam Input rather than as a raw pad — so "connect a controller and it just
  works" is *not* true out of the box on either platform. The mapping layer is real work
  (see "Input architecture"), which is exactly why the navigable core is keyboard-driven.

**Reserve testing on the physical Steam Deck for things that cannot be verified on
Windows:**
- On-screen keyboard behaviour (Steam+X manual invoke; note that for non-Steam shortcuts
  the Deck may surface the *desktop* keyboard rather than the game-mode OSK).
- Fullscreen/borderless behaviour specifically under gamescope.
- Proton-specific WPF rendering quirks.
- Real performance on Deck hardware/screen.
- The Steam Input layout for Deck mode (see "Deck deployment").
- The MateriaForge overlay deployment script (Linux-only by definition).

Do not treat Deck deployment as the default iteration loop — it's a periodic checkpoint,
not a per-change requirement.

## Input architecture (foundational — build this first)

The existing app has **no controller-driven UI navigation**. The only controller code in
the repo (`GameController.cs`, `ControllerInterceptor.cs`, `DS4ControllerService.cs`) runs
during *gameplay* to map a pad to FF7's in-game controls, and is started/stopped by
`GameLauncher`. WPF itself does not consume gamepad input. Therefore the navigation layer
is net-new and is the foundation everything else sits on.

**Design it as a logical command layer, not a device reader:**

- The UI reacts to **logical commands**, never to raw device input:
  `NavigateUp/Down/Left/Right`, `Activate`, `Back`, `ReorderToggle`, `OpenOptions`,
  `Search`, `SectionPrev`, `SectionNext`, `PageUp`, `PageDown`, `PlayShort`, `PlayLong`.
- **v1 input source (Windows + Deck): the keyboard.** Build the UI to be fully navigable
  via standard WPF keyboard focus (`KeyboardNavigation`/`FocusManager`) and key bindings
  that raise the logical commands. This is testable immediately with a keyboard, and on the
  Deck a Steam Input layout that emits keystrokes drives it directly — sidestepping the
  "WPF can't read XInput / MateriaForge's default layout is mouse" problem entirely.
- **Controller-mapping source (added after the keyboard core works):** a component that
  reads a physical controller and raises the same logical commands. On Windows this can
  reuse the existing SharpDX DirectInput enumeration path. Because the UI only ever sees
  logical commands, adding or swapping input sources never touches the views.
- Long-press (Menu button) is handled in this layer by timing key/button down↔up, not by
  the views.

**Glyphs are a separate concern from input routing.** Which glyph set the legend and
prompts display is independent of how commands arrive:

- **v1: a manual override in Settings** — Xbox / PlayStation / Nintendo, default Xbox
  (matches the Deck's built-in controls and most XInput controllers). Applied globally to
  the legend and any in-UI prompts.
- **Auto-detection is deferred**, with two documented real paths (do not attempt in v1):
  (a) On Windows/desktop, infer brand from the connected device's VID/PID via the existing
  SharpDX DirectInput enumeration. (b) On the Deck, use Steam Input's controller-type/glyph
  APIs (`ISteamInput`), which requires the Steamworks stretch integration below.
- **Do not attempt to derive glyphs from XInput** — XInput normalises all pads to an
  Xbox-style device and discards brand identity, so it cannot distinguish a DualSense from
  an Xbox pad. (This is also why raw-XInput was rejected as the navigation source.)

## Architecture approach

- New fullscreen WPF window/view(s) added under `AppUI` (in a `Deck/` sub-namespace),
  bound to **existing** ViewModels wherever possible rather than duplicating business
  logic. The real homes of the data:
  - `MyModsViewModel` (exposed as `MainWindowViewModel.MyMods`) — **the active mod list,
    activation toggle (`ToggleActivateMod`), and reorder (`ReorderProfileItem`).** This is
    the primary binding target for the dashboard and reorder mode, *not* `MainWindowViewModel`
    directly.
  - `MainWindowViewModel` — the aggregate root: current profile, and the play action
    (`LaunchGame`). Holds `MyMods` and `CatalogMods`.
  - `ConfigureModViewModel` — mod options. Exposes `ModOptions` as a
    `List<ConfigOptionViewModel>` tree (`ConfigOptionViewModel` is a nested class in
    `ConfigureModViewModel.cs`).
  - `GLSettingViewModel` — driver/graphics settings (a **different shape** — see "Settings
    surfaces").
  - `GeneralSettingsViewModel` — general app settings + subscription management (a **third,
    bespoke shape** — see "Settings surfaces").
  - `CatalogViewModel` (`MainWindowViewModel.CatalogMods`) — catalog browsing.
  - `OpenProfileViewModel` — profile list, create/copy/switch.
  - `GameLaunchViewModel` / `GameLauncher` — the actual launch work and its progress/result
    (see "Play flow").
- Build the **interaction-pattern controls** (checkbox toggle, dropdown cycle-or-expand,
  numeric stepper, suggestion-list field, keyboard-fallback text field) as small,
  reusable, ViewModel-agnostic components driven by the logical command layer. This is the
  single most reused piece of the project. Each settings/options surface composes these via
  a thin per-surface adapter — there is **no single "one renderer, one shape" assumption**.
- **Live-apply:** Deck mode applies each change immediately. **Note the existing behaviour:**
  `GLSettingViewModel` and `GeneralSettingsViewModel` are *batch-saved* on window-confirm
  (`ConfigureGLWindow` calls `item.Save(_settings)` then `_settings.Save()`;
  `GeneralSettingsViewModel.SaveSettings(...)`). Neither auto-persists on property change.
  So live-apply is *new behaviour the adapters must implement* — call the relevant
  save/persist after each interaction. Do not assume the VMs already live-apply.

## Settings surfaces (three distinct shapes — do not conflate)

There are three genuinely different shapes. The reusable interaction-pattern controls are
shared; the bindings are not.

- **Mod options — `ConfigOptionViewModel` (via `ConfigureModViewModel.ModOptions`).**
  **Verified:** the underlying `OptionType` enum has exactly two values — `Bool` and
  `List`. `ConfigureModViewModel` renders only a checkbox (`Bool`) or a combobox (`List`).
  **There is no text entry, no numeric entry, and no suggestions in mod options.** The mod-
  options renderer therefore handles only:
  - `Bool` → inline toggle pill (A flips it; no sub-screen).
  - `List` → dropdown (≤ threshold: cycle in place with left/right; > threshold: A expands a
    right-hand choice panel; see "Mod options screen").
  - nested children → drill-down.
- **Driver/graphics — `GLSettingViewModel`.** Wraps `AppCore.ConfigSettings.Setting`. Its
  `GLSettingType` enum is `{ Checkbox, Dropdown, TextEntry }`, and the underlying
  `TextEntry` carries an optional `Suggestions` list. **This is the surface where the
  numeric stepper, suggestion-list field, and keyboard-fallback text field actually apply.**
  Build a thin adapter mapping `GLSettingViewModel`'s properties onto the shared controls:
  - `Checkbox` → toggle pill.
  - `Dropdown` → dropdown (cycle/expand).
  - `TextEntry` with `Suggestions` → suggestion-list field (treat like a dropdown).
  - `TextEntry`, numeric default → left/right stepper.
  - `TextEntry`, free text, no suggestions → keyboard-fallback field.
- **General — `GeneralSettingsViewModel`.** A bespoke VM with individually named properties
  (`AutoSortModsByDefault`, `AutoUpdateModsByDefault`, `BypassCompatibilityLocks`,
  `FFNxUpdateChannel` [enum], `CheckForUpdatesAuto`, etc.). Build a hand-authored Deck
  screen, one row per known property, reusing the shared controls per row. It also contains
  a genuinely separate sub-feature — **subscription URL management** (`SubscriptionList` of
  `SubscriptionSettingViewModel`, `NewUrlText`, `IsResolvingName`, add/remove/`Move`) —
  which is a small list-management UI, not a settings row. Treat it as its own screen
  reusing the Profiles list + add/remove pattern.

**The non-negotiable v1 requirement:** the interaction-pattern controls are reusable and
ViewModel-agnostic, driven by the command layer, and each surface has its own adapter.
Driver/graphics and general settings must both be fully controller-navigable.

## Navigation model

Three-pane layout on the main dashboard:

1. **Left — section sidebar** (always visible, never collapses): Play, My Mods, Browse
   Catalog, Load Order, Settings. Current section highlighted. `L1`/`R1` move between
   sections.
2. **Center — list for the current section.**
3. **Right — contextual details pane** for whatever's focused in the center column. Not a
   modal/popup — updates live as focus moves.

A persistent, **context-sensitive button legend** sits at the bottom of every screen and
must update immediately when the input context changes (e.g. entering reorder mode swaps
the legend from "A: Select / B: Back" to "X: Drop / ↕: Move / B: Cancel"). It reads from
the command layer's current context.

**Focus routing between panes — the right-hand pane is used two ways; both need explicit
handling:**
- **Passive/mirroring** (dashboard, catalog browse): the right pane just displays details
  for whatever's focused in the center column. Focus never moves into it.
- **Active sub-list** (options dropdown expansion): A on a dropdown row moves focus *into*
  the right-hand choice panel, which becomes independently up/down-navigable; B returns
  focus to the row list and closes the panel.

A single generic "right pane" component must not assume only one behaviour — give it an
explicit mode flag (or two components) so this isn't conflated during implementation.

## Visual design

The design-preview mockups used during the conversation communicated layout and interaction
intent only; their exact colour tokens are not literal implementation values.

**Implement Deck mode's look as a new theme conforming to `AppUI/Classes/Themes/ITheme.cs`**
(verified: the interface exposes `PrimaryAppBackground`, `SecondaryAppBackground`,
`PrimaryControlBackground`, `PrimaryControlForeground`, `PrimaryControlSecondary`,
`PrimaryControlPressed`, `PrimaryControlMouseOver`, `PrimaryControlDisabledBackground`,
`PrimaryControlDisabledForeground`, plus `Name`/`BackgroundImage*`). Register it in the
`AppTheme` enum + the theme dictionary in `ThemeSettingsViewModel`, like the existing
themes, rather than hardcoding colours inline. Provide **both a light and dark variant**,
matching the existing Light/Dark pairs.

Aesthetic direction:
- **Flat** — no gradients, drop shadows, or glow/blur.
- **Generous whitespace and padding** — this is a handheld controller UI, not a dense
  desktop app; larger focus targets, more breathing room.
- **Thin hairline borders** (~0.5px-equivalent) for structural dividers.
- **A single accent-coloured border (~2px), reserved exclusively for current
  keyboard/controller focus.** The only place a thick colored border appears.
- **Pill-shaped badges** for category tags and warnings; small circular badges for the
  controller button glyphs in the legend.
- **Sentence case throughout** — "Browse catalog", not "Browse Catalog".

**Starting palette** (light variant — a reasonable default, refine once running):

| `ITheme` property | Suggested value | Role |
|---|---|---|
| `PrimaryAppBackground` | `#F5F4F2` | Page background |
| `SecondaryAppBackground` | `#FFFFFF` | Cards/panes |
| `PrimaryControlBackground` | `#FFFFFF` | Row/control background |
| `PrimaryControlForeground` | `#1A1A18` | Primary text |
| `PrimaryControlSecondary` | `#6B6A66` | Secondary/muted text |
| `PrimaryControlPressed` | `#E8E6E1` | Pressed/active row state |
| `PrimaryControlMouseOver` | `#EFEDE8` | Focus pre-highlight (distinct from the focus border) |
| `PrimaryControlDisabledBackground` | `#F0EFEC` | Disabled controls |
| `PrimaryControlDisabledForeground` | `#B5B3AE` | Disabled text |

**Accent colour** for the focus border and pill badges is **not** part of `ITheme`. Add it
as a new `ITheme` property (extend the interface + all existing implementations) or as a
fixed resource if a single accent across all themes is acceptable. Author preference not yet
gathered — flag when it comes up. For the dark variant, mirror the role structure using the
app's existing dark-theme values as reference.

## Control scheme (default — Xbox glyphs; see "Input architecture")

| Input | Logical command / meaning |
|---|---|
| D-pad / left stick | Move focus within current column |
| A | Select / toggle |
| B | Back / cancel |
| X | Enter reorder mode (mod list only) |
| Y | Open options for focused mod (mod list) / open search overlay (catalog) |
| L1 | Previous sidebar section |
| R1 | Next sidebar section |
| L2 | Page up |
| R2 | Page down |
| Menu/Options/Start (short) | Launch game using stored default play command |
| Menu/Options/Start (long) | Open picker to change the default play command |

This table should also live in-app as a discoverable help overlay (a "controls" entry in
Settings), not just in this spec.

## Screen-by-screen detail

### Dashboard / My Mods (binds `MyModsViewModel`)

- Sidebar as above. Center: active mod list (`MyModsViewModel.ModList`), each row showing
  enabled state via a small status dot/toggle, focus ring via the 2px accent outline.
- **Reorder mode**: X on a focused row "lifts" it (e.g. reduced opacity on the rest of the
  list). D-pad up/down slides it through the list live via `ReorderProfileItem`. X again (or
  A) drops it; B cancels and restores. Legend reflects the mode while active.
- **Mod conflict warnings**: a small warning badge on any row with a detected load-order
  conflict. Selecting the row expands conflict detail in the right pane (not a blocking
  dialog).

### Mod options screen (Y from My Mods) — binds `ConfigureModViewModel`

Full-screen takeover, not a dialog. Renders the focused mod's `ModOptions` tree. **Only two
control types occur here** (mod options are `Bool`/`List` — see "Settings surfaces"):
- **`Bool`** → inline toggle pill. A flips it.
- **`List`** → row shows current value + chevron.
  - ≤ threshold options: cycle in place with left/right (no sub-screen).
  - > threshold options: A expands a right-hand panel listing every choice, current one
    pre-highlighted (radio-style checkmark). The threshold (default 5) is a **named
    constant**.
- Nested/child options drill down (selecting a parent replaces the list with its children
  plus a "back" affordance) rather than rendering an indented tree.

(The numeric stepper, suggestion-list field, and keyboard-fallback field are **not** used
here — they belong to driver/graphics settings.)

### Browse catalog (binds `CatalogViewModel`)

- Left column: **categories**, built dynamically from the distinct `Category` strings in
  the catalog (`Mod.Category` is a free-form string — do not hardcode a list). Display
  labels via `ResourceHelper.ModCategoryTranslations` so known categories render localised.
  Center: mods within the selected category. Right: details pane with an Install action.
- **Y opens a search overlay** — slides over the category list, contains a true free-text
  field with no suggestions, so it shows the keyboard-fallback hint immediately (see "Text
  input"). Category browsing remains the default keyboard-free path.

### Load order

Not fully designed yet. Implement using the same list + reorder-mode pattern as My Mods (X
to lift/move). Confirm with the author if it turns out to need anything beyond the standard
reorder pattern before building.

### Settings (driver/graphics/general)

Uses the shared interaction-pattern controls, adapted per-ViewModel as described in
"Settings surfaces". The subscription-URL management inside `GeneralSettingsViewModel` is
its own list + add/remove screen, not a settings row.

### Profiles (binds `OpenProfileViewModel`)

7th Heaven already has full profile infrastructure (`Sys.ActiveProfile`,
`Sys.Settings.CurrentProfile`; `OpenProfileViewModel` with `SwitchToProfile`, `CopyProfile`,
`DeleteProfile`, and the static `InputNewProfileName`). A "profile" is a saved mod loadout
(e.g. "Vanilla run" vs "New Threat run"), not a user account — do not design a login screen.

Build a controller-friendly picker (reuse the list pattern). Creating a *new* profile
requires typing a name (`InputNewProfileName`) — a free-text-with-no-suggestions case, so
the keyboard-fallback pattern applies.

### Play flow (drives `GameLaunchViewModel` / `GameLauncher` directly)

**Verified:** `MainWindowViewModel.LaunchGame(bool variableDump, bool debugLogging, bool
noMods)` is a thin method that sets a flag, subscribes to `GameLauncher.Instance.
LaunchCompleted`, and opens a **separate desktop window** (`GameLaunchWindow.Show(...)`).
The real work (file copies, conflict checks, applying settings) is in `GameLauncher`, and
progress + result already exist: `GameLaunchViewModel.StatusLog`,
`GameLauncher.Instance.ProgressChanged`, and `LaunchCompleted(bool wasSuccessful)`.

Therefore:
- **Do not call `MainWindowViewModel.LaunchGame()` from Deck mode** — it would pop the
  desktop `GameLaunchWindow`, which is wrong under gamescope. Instead drive `GameLauncher` /
  `GameLaunchViewModel` directly (the way `GameLaunchWindow` does) and render an **inline**
  controller-friendly progress screen bound to `StatusLog`, with a clear error state
  reading `LaunchCompleted(wasSuccessful=false)`. Reuse this existing infrastructure rather
  than building launch logic from scratch.
- **The four variants** (With Mods [default], Without Mods, With Debug Log, With Variable
  Dump) are combinations of the three flags. Add a new setting storing which is the
  "default play command" (**does not currently exist — add it additively to
  `AppCore/Settings.cs` or `LaunchSettings.cs`**).
- **Menu/Options/Start short press**: launch with the stored default flags, from any screen.
- **Long press**: open a small picker of the four variants; selecting one sets it as the
  new default and closes. Note the existing flow pops a `MessageDialogWindow` "are you sure"
  confirmation for the Debug Log and Variable Dump variants — provide a controller-friendly
  equivalent (or an inline confirm) for those two.

## Text input / on-screen keyboard

This is a WPF/.NET app, not SDL-based and not Steamworks-integrated. Auto-OSK popup on the
Deck is triggered by `SDL_StartTextInput()` or Valve's `ShowGamepadTextInput` — neither
applies here by default, so **do not assume the keyboard appears automatically.** Design for
the manual fallback:

- Whenever a true free-text field (no suggestions) is focused, the legend must show:
  **"Steam + X: keyboard"**.
- This affects only two spots: catalog search, and new-profile naming. Every other text-like
  field is routed through dropdown/suggestion/stepper patterns to minimise how often the
  fallback is needed.
- Deck caveat to verify on-hardware: for non-Steam shortcuts, Steam+X may surface the
  *desktop* keyboard rather than the game-mode OSK.
- **Stretch goal, not v1**: link the Steamworks SDK (`steam_api64.dll`) to use Valve's
  official APIs. Relevant ones: `ShowGamepadTextInput` / `ShowFloatingGamepadTextInput`
  (auto-keyboard), `ISteamInput` glyph/type APIs (real glyph auto-detection), and
  `SetGameLauncherMode` (Valve's documented API for translating controller input to
  keyboard/mouse for required native launchers — directly relevant to a WPF launcher on
  Deck). Only pursue once the core UI is working and tested on-Deck.

## Deck deployment (separate script, not part of the app)

A small shell script under `/deck-deploy/` that:

1. Assumes MateriaForge has already been run normally on the Deck to install and configure
   stock 7th Heaven (Proton prefix, `MateriaForge.toml`, runner selection). **Verified:**
   MateriaForge writes `MateriaForge.toml` into the install folder and selects the Proton
   runner; leave these as generated.
2. Copies this fork's built output over the stock files in that same install folder, leaving
   `MateriaForge.toml` and the Proton prefix untouched, and ensures the launch command
   passes `--deck`. **Verified mechanism** (MateriaForge `src/launcher/main.rs`): the
   "Launch 7th Heaven" shim forwards its own CLI arguments to the exe, falling back to
   `launch_args` from the toml only when none are given — so `--deck` goes in the Steam
   entry's launch options, and the toml stays untouched. See `deck-deploy/README.md`.

**Steam Input layout (Deck-only, must be resolved).** MateriaForge installs its own
controller config for 7th Heaven — **verified against its shipped VDF**
(`resources/controller_neptune_gamepad+mouse+click.vdf`, titled "Gamepad with Mouse
Trackpad + Click"): full XInput passthrough for buttons/sticks/d-pad plus right-trackpad
mouse, *not* mouse-only as an earlier draft claimed. Deck mode's XInput controller source
may therefore partially work even under the stock config; the keystroke layout below is
still the supported v1 path (keystrokes are the guaranteed input, and they free X/Y and
the bumpers from their gamepad meanings). But a single flat "every button is a keystroke"
layout for the whole session is **wrong**: FF7 launches as a **child process of the same Steam entry** (not a
separate Steam app), and 7th Heaven already has its own working in-game controller system —
`GameController.cs` (DirectInput polling), `ControllerInterceptor.cs` (maps buttons FF7
doesn't natively support into keyboard input it does understand), and
`DS4ControllerService.cs` — all started by `GameLauncher` at launch and stopped on exit.
That existing system needs to see the **raw physical device**. With a keystroke-only layout
active for the whole session, Steam Input has already turned the controller into a keyboard
as far as anything downstream can tell by the time the game starts, and FF7's own controller
input silently breaks.

Ship **two Steam Input action sets**, not one:

- **"Launcher"** — buttons → keystrokes matching the keyboard command layer (d-pad→arrows,
  A→Enter, B→Esc, X/Y/L1/R1/L2/R2/Menu→their mapped keys). Active while Deck mode's own UI
  has focus.
- **"Game"** — buttons → standard gamepad passthrough (or Steam Input disabled for this set
  entirely), so the raw controller reaches 7th Heaven's existing injection code exactly as
  it does on Windows today. Active once FF7 has launched.

Switching between them: the clean mechanism is `ISteamInput::ActivateActionSet`, called by
the app itself right before spawning FF7.exe and again on return. This requires linking the
Steamworks SDK, which promotes that part of the existing Steamworks stretch goal (see "Text
input / on-screen keyboard") from deferred to load-bearing for a smooth on-Deck experience.
If the Steamworks integration isn't ready yet, the fallback is shipping both action sets in
the layout anyway and documenting manual switching via the Steam overlay when going from
launcher to game and back — clunky, but functional, and worth having as the interim state
rather than a layout that silently breaks gameplay input. Do **not** silently rely on
MateriaForge's default mouse config — it is the opposite of what Deck mode needs.

**Prefix/runtime compatibility.** The overlay assumes the fork runs in the same Proton
prefix + .NET runtime MateriaForge provisioned for the stock build. Keep the fork's target
framework and dependency set aligned with the stock build; re-verify on-Deck after any
dependency change. **Do not assume the prefix needs the x86 (or x64) .NET runtime
specifically** — nothing in the code pins a bitness. Checked, not assumed: the official
release CI builds `Platform="Any CPU"` (`.github/workflows/main-4.5.2.yml`), and no
`Prefer32Bit` is set anywhere in the solution, so neither the stock build nor this fork's
overlay is pinned to a specific bitness by the code itself. Verify which bitness the
MateriaForge-provisioned prefix actually has, and if the overlay build won't launch, check
for a runtime-bitness mismatch **before** assuming a code or deployment bug. This is a
real, recurring Proton/Wine gotcha: `winetricks`/`protontricks` .NET installers frequently
put only the 32-bit runtime into a 64-bit prefix by default, and an app that actually needs
the 64-bit runtime then fails to detect .NET as installed at all, even though something was
installed. Since MateriaForge already gets stock 7th Heaven running today, it has presumably
solved this for whichever bitness stock 7th Heaven needs — but that shouldn't be assumed
without checking.

**Early spike, before relying on the overlay**: verify MateriaForge does not do
integrity/hash checking that would "self-heal" by re-downloading stock files over the
overlay. The README documents no per-launch integrity check, but MateriaForge is actively
developed and a version-triggered re-download on *update* is plausible. Test on the actual
Deck early. If it's a problem, run the overlay after every MateriaForge update.

## Explicitly deferred / open items (do not block v1)

- Scrolling for very long lists beyond L2/R2 paging (e.g. jump-to-letter).
- Any mod whose configuration pops a fully custom dialog instead of the standard
  `ConfigOptionViewModel` system — handle case-by-case if/when encountered.
- Controller glyph **auto-detection** (manual override is the v1 solution; two upgrade paths
  documented in "Input architecture").
- Load order's exact interaction details — confirm before building if it needs anything
  beyond the standard reorder pattern.
- The accent-colour question (new `ITheme` property vs fixed resource).
- Steamworks SDK integration (auto-OSK, glyph detection, `SetGameLauncherMode`).

## Suggested build order (separate sessions, not one pass)

1. **Foundation + dashboard shell.** The `--deck` entry point (additive startup branch); the
   logical **command layer** with the keyboard input source; the Deck shell window; sidebar
   navigation; My Mods list bound to `MyModsViewModel`; contextual legend; passive right-
   hand details pane; the new Deck `ITheme` (light + dark); Play button (basic, single
   default variant, driving `GameLauncher`/`GameLaunchViewModel` inline). **This is the
   first Windows-testable milestone.**
2. Reorder mode (X lift/move) on My Mods via `ReorderProfileItem`.
3. Mod-options renderer for `ConfigureModViewModel`: **checkbox (`Bool`) and dropdown
   (`List`, both thresholds) only**, plus nested drill-down. Wire into mod options. (The
   stepper/suggestion/keyboard-text controls are *not* exercised here — mod options have no
   text types.)
4. Settings adapters, where the remaining controls are built and first tested:
   driver/graphics (`GLSettingViewModel` adapter — this surface is where the numeric
   stepper, suggestion-list field, and keyboard-fallback field actually apply) and general
   settings (`GeneralSettingsViewModel` hand-authored per-property screen).
5. Catalog browsing (`CatalogViewModel`): dynamic category list, details pane, install
   action, Y-search overlay with keyboard hint.
6. Profiles: picker over `OpenProfileViewModel` (establishes the list + add/remove pattern).
   6a. Subscription-URL management screen (part of `GeneralSettingsViewModel`, but a
   distinct list + add/remove UI) — build it here, reusing the Profiles pattern, since it
   depends on that pattern.
7. Play flow completion: default-play-command setting, short/long-press on Menu, inline
   loading/progress + error screen over `GameLaunchViewModel`.
8. Mod conflict warning badges.
9. Deck deployment script under `/deck-deploy/`, the Steam Input keystroke layout, and the
   MateriaForge integrity-check spike.
10. First real Steam Deck verification pass across everything above.

## Revisions from Windows testing (2026-07-03)

Feedback from the first hands-on Windows session. These supersede the corresponding
sections above and are the next implementation priorities:

1. **The legend must reflect the active input device.** When driving with a keyboard the
   legend shows the actual keys (Enter, Esc, R, O, Q/E…), not Xbox glyphs. Implemented as
   a glyph-set layer (`DeckGlyphs`): the keyboard set is the default until a controller
   source exists; the manual Xbox/PS/Nintendo override in Settings remains the plan for
   pad brands.
2. **The details/description pane gets more room.** It carries the most important content
   (mod description, and later screenshots) and should be substantially wider than the
   current fixed 380px column.
3. **Top bar replaces the left sidebar.** Sections move to a horizontal bar along the top
   of the screen, cycled with LB/RB. The freed left column goes to content.
4. **Drop the "Play" and "Load order" sections.** Play is the Menu/Start button (and a
   persistent legend affordance), not a place you navigate to; load order is handled
   entirely by reorder mode inside My mods. Remaining sections: My mods, Browse catalog,
   Settings.
5. **Add a quit affordance.** Deck mode has no window chrome, so it needs an explicit way
   to exit the app — e.g. an entry in Settings and/or a hold-to-quit on the Back button at
   the top level. (Ctrl+Q exists as a dev shortcut on Windows.)
