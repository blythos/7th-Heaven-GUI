# CLAUDE.md

Guidance for working in this repo. Read `SPEC.md` alongside this file before feature work.

## What this repo is

A fork of `tsunamods-codes/7th-Heaven` — a WPF/.NET mod manager for the PC version of Final
Fantasy VII. This fork (`github.com/blythos/7th-Heaven-GUI`, branch `deck-controller-ui`)
adds a **fullscreen, controller-first "Deck mode"** for the Steam Deck, layered on top of
the existing ViewModels. The stock desktop UI is left intact. See `SPEC.md` for the full
design.

The app is Windows-only WPF and runs on the Steam Deck **via Proton** (set up by the
separate tool MateriaForge). There is no native Linux build and none is planned.

## Golden rules

1. **Deck mode is additive.** Add **new files**; do not modify existing files except:
   - one additive `--deck` branch in `AppUI/App.xaml.cs` startup, and
   - additive-only changes to settings *data* types (`AppCore/Settings.cs` /
     `AppCore/LaunchSettings.cs`) for the new "default play command" setting.
   Everything else Deck-related lives in new files under `AppUI/…/Deck/`.
2. **Never touch the game-injection logic:** `AppWrapper` (esp. `Win32.cs`) and
   `AppCore/RegistryHelper.cs`. Not portable, not in scope.
3. **Do not fork or edit MateriaForge.** The fork is overlaid onto a MateriaForge-managed
   install by a deploy script.
4. **Verify against the code, don't trust prose.** An earlier spec draft made confident
   claims that were false (see "Gotchas"). If code disagrees with `SPEC.md`, trust the code
   and flag it.
5. **Test on Windows first.** The Deck is a periodic checkpoint, not the iteration loop.

## Solution layout

`7thHeaven.sln`, target framework `net10.0-windows7.0` for all projects.

- `AppCore/` — domain model + engine. `Settings.cs`, `LaunchSettings.cs`, `Mod.cs`,
  `Profile.cs`, `Sys.cs`, `ConfigSettings.cs`, `RegistryHelper.cs` (⛔ injection). The UI
  binds to types here.
- `AppWrapper/` — ⛔ game/process injection (`Win32.cs`, FF7 file formats). Do not touch.
- `AppUI/` — the WPF app (this is the exe). All ViewModels, Windows, UserControls, Themes.
  **Deck mode code goes here, in new files.**
- `AppProxy/`, `AppLoader/`, `CatalogValidator/`, `TurBoLog/` — supporting/native; not
  relevant to Deck mode.

## Build & run (Windows)

- The solution has native/toolchain dependencies (vcpkg via `Directory.Build.props`;
  `AppProxy`; bundled `AppUI/DS4WindowsCore.dll`). A full Visual Studio install with the
  workloads in `.vsconfig` is the most reliable build environment; the .NET 10 SDK plus
  native prerequisites may also work.
- **Before any feature work, confirm a clean build of the unmodified fork.** If upstream
  doesn't build, fix that first.
- Build: `dotnet build 7thHeaven.sln -c Debug` (or build in Visual Studio).
- Run stock UI: launch the built `AppUI` exe (startup project).
- Run Deck mode (once implemented): launch the same exe with `--deck`.

## Verified ViewModel shapes (do not re-derive from memory)

These were checked against source. They are the load-bearing facts Deck mode depends on.

- **Mod list & reorder live in `MyModsViewModel`** (exposed as `MainWindowViewModel.MyMods`),
  not in `MainWindowViewModel` directly. Key members: `ModList`, `ToggleActivateMod(...)`,
  `ReorderProfileItem(mod, change)`.
- **`MainWindowViewModel`** is the aggregate root: current profile, `MyMods`, `CatalogMods`,
  and `internal void LaunchGame(bool variableDump, bool debugLogging, bool noMods)`
  (line ~1641). `LaunchGame` just opens the desktop `GameLaunchWindow` — see Play flow.
- **Mod options — `ConfigureModViewModel.ModOptions`** is a `List<ConfigOptionViewModel>`
  tree (`ConfigOptionViewModel` is a nested class in `ConfigureModViewModel.cs`). The
  underlying `OptionType` enum (`AppWrapper/Profile.cs`) has **only `Bool` and `List`**.
  Rendering is checkbox (`Bool`) or combobox (`List`) — **no text/numeric/suggestion types
  exist in mod options.**
- **Driver/graphics — `GLSettingViewModel`** wraps `AppCore.ConfigSettings.Setting`;
  `GLSettingType = { Checkbox, Dropdown, TextEntry }`; underlying `TextEntry` has an optional
  `Suggestions` list. **This** is where stepper / suggestion-list / keyboard-fallback fields
  belong — not mod options.
- **General — `GeneralSettingsViewModel`** is bespoke, with named properties
  (`AutoSortModsByDefault`, `BypassCompatibilityLocks`, `FFNxUpdateChannel` [enum], etc.).
  Contains subscription management: `SubscriptionList` (of `SubscriptionSettingViewModel`),
  `NewUrlText`, `IsResolvingName`, add/remove/`Move` — treat as its own list screen.
- **Settings are batch-saved, not live-apply.** `ConfigureGLWindow` calls `item.Save(...)`
  then `_settings.Save()`; `GeneralSettingsViewModel.SaveSettings(...)`. Deck's live-apply
  must call save/persist after each change explicitly.
- **Profiles — `OpenProfileViewModel`**: `Profiles` (list of names), `SelectedProfile`,
  static `InputNewProfileName(...)`, `CopyProfile`, `SwitchToProfile`, `DeleteProfile`.
  `Sys.ActiveProfile`, `Sys.Settings.CurrentProfile`.
- **Catalog — `CatalogViewModel`** (`MainWindowViewModel.CatalogMods`). `Mod.Category` is a
  free-form `string`; build categories dynamically; display via
  `ResourceHelper.ModCategoryTranslations`.
- **Launch work — `GameLauncher` / `GameLaunchViewModel`**: real launch happens in
  `GameLauncher`; progress via `GameLaunchViewModel.StatusLog` +
  `GameLauncher.Instance.ProgressChanged`; result via `LaunchCompleted(bool wasSuccessful)`.
  Deck mode drives these directly and renders progress inline — it must **not** call
  `MainWindowViewModel.LaunchGame()` (that opens a separate desktop window).
- **Theming — `AppUI/Classes/Themes/ITheme.cs`**: string (hex) properties
  `PrimaryAppBackground`, `SecondaryAppBackground`, `PrimaryControlBackground`,
  `PrimaryControlForeground`, `PrimaryControlSecondary`, `PrimaryControlPressed`,
  `PrimaryControlMouseOver`, `PrimaryControlDisabledBackground`,
  `PrimaryControlDisabledForeground` (+ `Name`, `BackgroundImage*`). **No accent property.**
  Register new themes in the `AppTheme` enum + dictionary in `ThemeSettingsViewModel`.

## Accessibility caveat

Several ViewModels/members are `internal` to `AppUI` (e.g. `LaunchGame`,
`GLSettingViewModel`). **This is why Deck code must live inside the `AppUI` assembly** — a
separate assembly can't reach them without `InternalsVisibleTo`/public-ising. Keep Deck code
in `AppUI`.

## Controller input — read this before building navigation

- The existing app has **no controller-driven UI navigation.** The controller code
  (`GameController.cs` [SharpDX DirectInput], `ControllerInterceptor.cs`,
  `DS4ControllerService.cs`) runs **only during gameplay**, started by `GameLauncher`, to
  map a pad to FF7's in-game controls. WPF does not read gamepads natively.
- Deck navigation is therefore net-new. Build a **logical command layer**; the v1 input
  source is the **keyboard** (so it's testable with a keyboard alone, and a Steam Input
  keystroke layout drives it on the Deck). A controller-reading source is added later and
  raises the same logical commands. See `SPEC.md` → "Input architecture".
- **Glyphs ≠ input routing.** v1 uses a manual Xbox/PS/Nintendo override in Settings.
  Auto-detection is deferred. Do not try to derive glyphs from XInput (it normalises brand
  away).

## Conventions

- Sentence case for all UI text.
- Flat visual style; the 2px accent border is reserved solely for focus.
- Interaction-pattern controls (toggle, dropdown cycle/expand, stepper, suggestion field,
  keyboard-fallback field) are reusable and ViewModel-agnostic, driven by the command layer;
  each surface adapts them via a thin adapter.
- New Deck code namespaced under `AppUI…Deck`.

## Gotchas discovered in review (don't repeat these)

- Mod options are **`Bool`/`List` only** — no text/numeric/suggestion fields. Those are a
  driver/graphics (`GLSettingViewModel`) thing.
- `MainWindowViewModel.LaunchGame()` opens a **separate desktop window** and does not itself
  do the launch work — drive `GameLauncher`/`GameLaunchViewModel` instead.
- Settings are **batch-saved**, not live-apply.
- The mod list/reorder lives in **`MyModsViewModel`**, not `MainWindowViewModel`.
- On the Deck, MateriaForge's default controller config is **trackpad-as-mouse**. The fix is
  **two Steam Input action sets**, not one flat keystroke layout: "Launcher" (buttons →
  keystrokes for the Deck UI) and "Game" (gamepad passthrough). FF7 runs as a child process
  of the same Steam entry, and the existing in-game controller code needs the raw device —
  a session-wide keystroke layout silently breaks gameplay input. Switch via
  `ISteamInput::ActivateActionSet` (manual switching via the Steam overlay is the fallback).
  Full reasoning: `SPEC.md` → "Deck deployment".
- Don't assume the Deck's Proton prefix needs the **x86** .NET runtime — the code pins no
  bitness (release CI builds `Any CPU`; no `Prefer32Bit` anywhere). Verify the prefix's
  actual bitness; an overlay build that won't launch may be a 32/64-bit runtime mismatch,
  not a code or deployment bug. See `SPEC.md` → "Deck deployment".

## Git

- Work on `deck-controller-ui`. Small, focused commits. First commit: the README Deck note.
- Do not commit build output.
