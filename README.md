# 7th Heaven — Deck controller UI fork

A fork of [tsunamods-codes/7th-Heaven](https://github.com/tsunamods-codes/7th-Heaven),
the mod manager for Final Fantasy VII PC, adding an experimental **controller-first
fullscreen mode ("Deck mode") for the Steam Deck**, launched with the `--deck` flag.

This is a personal project and is not affiliated with or endorsed by the Tsunamods team.
The stock desktop UI is unchanged; Deck mode is an additive layer over the same
ViewModels and engine, and the app remains Windows-only WPF, running on the Deck via
Proton (set up by [MateriaForge](https://github.com/dotaxis/MateriaForge-rs), which this
fork overlays but never modifies).

- [SPEC.md](SPEC.md) — the full Deck mode design
- [CLAUDE.md](CLAUDE.md) — codebase guide and verified ViewModel shapes
- [deck-deploy/](deck-deploy/README.md) — deploying the fork onto a Steam Deck

## Building

Same as upstream: Visual Studio with the workloads in [.vsconfig](.vsconfig) (plus
`vcpkg integrate install`), then build [`7thHeaven.sln`](7thHeaven.sln); or
`dotnet build 7thHeaven.sln -c Debug`. Run the built `AppUI` exe for the stock UI, or
with `--deck` for Deck mode.

## License

See [LICENSE.txt](LICENSE.txt). Upstream credits and history: the
[original 7th Heaven 2.x](https://github.com/unab0mb/7h) and the
[Tsunamods 7th Heaven repository](https://github.com/tsunamods-codes/7th-Heaven).
