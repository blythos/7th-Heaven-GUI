# Deck deployment

Overlays this fork's build onto a **MateriaForge-managed** 7th Heaven install on the
Steam Deck. MateriaForge itself is never modified (golden rule 3): it provisions the
Proton prefix, runner, and Steam entry; we replace the 7th Heaven binaries it installed
and add `--deck` to the launch options.

Contents:

- `deploy.sh` — the overlay script (run on the Deck, desktop mode)
- `steam-input/controller_neptune_7th_heaven_deck_mode.vdf` — two-action-set controller
  layout template
- `steam-input/LAYOUT.md` — binding tables, install/selection steps, action-set
  switching (including the fallback), and the layout's known unknowns

## Prerequisites

1. MateriaForge has been run normally on the Deck and **stock** 7th Heaven launches and
   plays. Everything below assumes that baseline works.
2. The fork is built on Windows: `dotnet build AppUI/AppUI.csproj -c Debug` (or Release),
   plus the separately built `AppLoader.dll` / `AppLoader.pdb` / `nethost.dll` copied
   into the output folder (see repo build notes — the launcher copies these three into
   the game dir, so a build without them boots the UI but cannot inject mods).

## Deploying

1. Copy the whole build output folder (the one containing `7th Heaven.exe`) to the Deck
   (USB, `scp`, syncthing — anything).
2. On the Deck, in desktop mode:

   ```sh
   ./deploy.sh /path/to/build-output            # searches $HOME for the install
   ./deploy.sh /path/to/build-output /path/to/install   # or explicit
   ```

   The script validates both folders (`7th Heaven.exe` + `MateriaForge.toml`), backs up
   every stock file it overwrites into `<install>/deck-overlay-backup/` (first run
   only), copies the build over the install, and installs the Steam Input template. It
   never touches `MateriaForge.toml`, the Proton prefix, or the install's `7thWorkshop/`
   user data, and it refuses to copy a `7thWorkshop/` from the build folder (that would
   plant the dev machine's settings on the Deck).
3. In Steam, open the 7th Heaven entry → **Properties → Launch Options** and set:

   ```
   --deck
   ```

   Why this works (verified in MateriaForge source, `src/launcher/main.rs`): the
   "Launch 7th Heaven" shim forwards its own CLI arguments to `7th Heaven.exe`
   (`env::args().skip(1)`), falling back to `launch_args` from `MateriaForge.toml` only
   when none are given. Steam launch options on a non-Steam entry are passed as CLI
   arguments, so `--deck` reaches the app without editing the toml. Note the fallback
   semantics: setting launch options **replaces** any `launch_args` from the toml.
4. Select the controller layout and verify both action sets — `steam-input/LAYOUT.md`.

**Rollback:** copy the contents of `<install>/deck-overlay-backup/` back over the
install, or just re-run MateriaForge (which reinstalls stock — see below).

## Integrity-check spike (result: no self-heal at launch)

Verified against `dotaxis/MateriaForge-rs` **main**, tree `bc4d627`, 2026-07-04:

- **Per-launch path** (`src/launcher/main.rs`): reads config values from
  `MateriaForge.toml` (`type`, `app_id`, `target`, `launch_args`), checks
  `7th Heaven.exe` exists (`bail!` if missing), and runs it in the prefix. **No
  downloads, no hash checks, no file overwrites at launch** — the overlay survives
  normal play indefinitely.
- **Installer path** (`src/installers/common.rs`): `download_asset()` downloads from
  GitHub releases with **no hash/version/existence checks** and overwrites — but it only
  runs when the user runs the MateriaForge installer itself. So:

> **Rule: re-run `deploy.sh` after every MateriaForge run or update.** Running
> MateriaForge reverts the install to stock wholesale; nothing reverts it behind your
> back between runs.

Re-verify on the Deck after MateriaForge releases a new version (the project is
actively developed): run MateriaForge, confirm the overlay is gone (stock UI launches),
re-run `deploy.sh`, confirm Deck mode is back. If a future MateriaForge version adds
launch-time verification, this section is wrong — check `launcher.log` in the install
folder for unexpected download activity.

Also verified in passing: MateriaForge's shipped controller config
(`resources/controller_neptune_gamepad+mouse+click.vdf`) is titled "Gamepad with Mouse
Trackpad + Click" — **full XInput passthrough plus trackpad mouse**, not mouse-only.
Deck mode's XInput controller source may therefore partially work even under the stock
config; the two-action-set layout is still the supported path (keystrokes are the
guaranteed v1 input, and the keystroke set frees X/Y/bumpers from their gamepad
meanings).

## Proton prefix runtime — verify bitness, don't assume

Per `SPEC.md` → "Deck deployment": nothing in this repo pins a bitness (release CI
builds `Platform="Any CPU"`, no `Prefer32Bit` anywhere), so **verify what the
MateriaForge-provisioned prefix actually contains** rather than assuming x86 or x64.

Two distinct runtime needs — don't conflate them:

1. **The launcher app** (`7th Heaven.exe`, AnyCPU WPF): needs whichever .NET desktop
   runtime bitness matches the process Proton starts.
2. **Mod injection into FF7**: FF7.exe is a 32-bit process, and on the Windows dev
   machine injection required the **x86** .NET 10 runtime specifically (verified
   2026-07). Expect the prefix to need the x86 runtime for this even if the launcher
   itself runs x64.

Checking what's in the prefix (from desktop mode; prefix lives under the install's
compatdata — find it via `MateriaForge.toml` or the Steam entry's settings):

```sh
ls "<prefix>/drive_c/Program Files/dotnet"        # x64 runtimes
ls "<prefix>/drive_c/Program Files (x86)/dotnet"  # x86 runtimes
```

Triage rule: **if the overlay build won't launch (or launches but won't inject), check
for a runtime-bitness mismatch before assuming a code or deployment bug.**
`winetricks`/`protontricks` .NET installers frequently install only the 32-bit runtime
into a 64-bit prefix; an app needing the 64-bit runtime then reports .NET as missing
entirely. MateriaForge already gets stock running, so whatever stock needs is present —
but this fork targets `net10.0-windows7.0` like stock, so a working stock baseline
should mean a working overlay. Verify, and record the actual prefix contents here on
the first on-Deck pass.

## On-Deck verification checklist (first real pass)

Deployment:

- [ ] `deploy.sh` finds the install, backs up, overlays; stock file count in
      `deck-overlay-backup/` looks sane
- [ ] Launch without `--deck` → **stock desktop UI** still works under Proton
      (overlay didn't break the baseline)
- [ ] Add `--deck` → fullscreen Deck shell appears under gamescope (game mode), no
      window chrome, correct scale on the 1280×800 panel

Input (Launcher action set — every row of the table in LAYOUT.md):

- [ ] D-pad + left stick move focus; A/B/X/Y act per legend; LB/RB switch sections;
      LT/RT page; Menu tap launches, Menu hold opens the variant picker
- [ ] Right trackpad moves the cursor, click selects (mouse support)
- [ ] Legend switches to pad glyphs when using the pad (XInput source may also be
      live — note which source is actually driving)
- [ ] Text entry: catalog search / new-profile name / add-catalog URL — does Steam+X
      surface a usable keyboard over the app (game mode vs desktop keyboard caveat,
      SPEC → "Text input")?

Action-set switching:

- [ ] Back grips switch Launcher ↔ Game (the `CHANGE_PRESET` binding — see LAYOUT.md
      known unknowns; fix in the layout editor if dead)
- [ ] Steam overlay switching works as fallback

Game:

- [ ] Play → FF7 launches with mods; in-game controller input works (Game set = raw
      pad reaches the existing controller/injection code)
- [ ] Quit FF7 → back in Deck shell; switch back to Launcher set; UI still navigable
- [ ] `--deck` quit affordance works (Settings → Quit / Esc at top bar)

Environment:

- [ ] Record prefix dotnet folders (x86/x64, versions) in the section above
- [ ] Run MateriaForge once more → confirm overlay reverted → re-run `deploy.sh` →
      confirm Deck mode back (integrity spike re-check)
