#!/usr/bin/env bash
#
# Overlays a Deck-mode build of 7th Heaven onto a MateriaForge-managed install.
#
# Run this ON THE STEAM DECK (desktop mode), after:
#   1. MateriaForge has been run normally and stock 7th Heaven launches, and
#   2. the fork's Windows build output has been copied onto the Deck.
#
# What it does:
#   - finds (or takes) the MateriaForge install folder and sanity-checks it
#   - backs up every stock file it is about to overwrite (first time only)
#   - copies the build output over the install, leaving MateriaForge.toml,
#     the Proton prefix, and the 7thWorkshop user-data folder untouched
#   - installs the two-action-set Steam Input template if a Steam root is found
#
# It does NOT edit MateriaForge.toml or any Steam shortcut. Add --deck to the
# non-Steam entry's launch options yourself (see ../deck-deploy/README.md).
#
# Re-run this script after every MateriaForge run/update: the MateriaForge
# installer overwrites files without checking them (see README, "Integrity
# check spike"), which reverts the overlay.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEMPLATE_VDF="$SCRIPT_DIR/steam-input/controller_neptune_7th_heaven_deck_mode.vdf"
BACKUP_NAME="deck-overlay-backup"

usage() {
    cat <<EOF
Usage: $(basename "$0") <build-dir> [install-dir]

  build-dir    The fork's built output (the folder containing "7th Heaven.exe",
               e.g. AppUI/bin/Debug/net10.0-windows7.0 copied from Windows).
  install-dir  The MateriaForge-managed 7th Heaven install folder (contains
               MateriaForge.toml). Omit to search under \$HOME for it.
EOF
    exit 1
}

die() {
    echo "error: $*" >&2
    exit 1
}

BUILD_DIR="${1:-}"
INSTALL_DIR="${2:-}"

[ -n "$BUILD_DIR" ] || usage
[ -d "$BUILD_DIR" ] || die "build dir not found: $BUILD_DIR"
[ -f "$BUILD_DIR/7th Heaven.exe" ] || die "no '7th Heaven.exe' in $BUILD_DIR - is this the AppUI build output?"

# the launcher copies these three into the game dir at launch; a build without
# them boots the UI but cannot inject mods (see repo build notes)
for f in "AppLoader.dll" "nethost.dll"; do
    if [ ! -f "$BUILD_DIR/$f" ]; then
        echo "warning: $f missing from the build output - mod injection will not work" >&2
    fi
done

# ---- locate the MateriaForge install ---------------------------------------

if [ -z "$INSTALL_DIR" ]; then
    echo "No install dir given - searching \$HOME for MateriaForge.toml…"

    mapfile -t candidates < <(
        find "$HOME" -maxdepth 6 -name 'MateriaForge.toml' \
             -not -path '*/steamapps/compatdata/*' 2>/dev/null \
        | while read -r toml; do dirname "$toml"; done \
        | sort -u
    )

    case "${#candidates[@]}" in
        0) die "no MateriaForge.toml found under \$HOME - pass the install dir explicitly" ;;
        1) INSTALL_DIR="${candidates[0]}" ;;
        *)
            echo "Multiple MateriaForge installs found - pass one explicitly:" >&2
            printf '  %s\n' "${candidates[@]}" >&2
            exit 1
            ;;
    esac
fi

[ -d "$INSTALL_DIR" ] || die "install dir not found: $INSTALL_DIR"
[ -f "$INSTALL_DIR/MateriaForge.toml" ] || die "no MateriaForge.toml in $INSTALL_DIR - not a MateriaForge-managed install"
[ -f "$INSTALL_DIR/7th Heaven.exe" ] || die "no '7th Heaven.exe' in $INSTALL_DIR - stock install incomplete?"

echo "Build dir:   $BUILD_DIR"
echo "Install dir: $INSTALL_DIR"

# ---- back up + overlay ------------------------------------------------------

BACKUP_DIR="$INSTALL_DIR/$BACKUP_NAME"
copied=0
backed_up=0

# never copy from the build output: local user data, logs, and the install's
# own control files (fresh CI output has none of these, but a build folder the
# app has been run from contains a 7thWorkshop with the DEV machine's settings)
is_excluded() {
    case "$1" in
        ./7thWorkshop/*|./MateriaForge.toml|./"$BACKUP_NAME"/*) return 0 ;;
        *.log|*.LOG) return 0 ;;
        *) return 1 ;;
    esac
}

while IFS= read -r -d '' src; do
    rel="${src#"$BUILD_DIR"/}"
    rel="./$rel"

    if is_excluded "$rel"; then
        continue
    fi

    dest="$INSTALL_DIR/${rel#./}"
    backup="$BACKUP_DIR/${rel#./}"

    # stock file about to be replaced: keep the original, once
    if [ -f "$dest" ] && [ ! -f "$backup" ]; then
        mkdir -p "$(dirname "$backup")"
        cp -p "$dest" "$backup"
        backed_up=$((backed_up + 1))
    fi

    mkdir -p "$(dirname "$dest")"
    cp -f "$src" "$dest"
    copied=$((copied + 1))
done < <(find "$BUILD_DIR" -type f -print0)

echo "Overlaid $copied files ($backed_up stock originals saved to $BACKUP_DIR)."

# ---- Steam Input template ---------------------------------------------------

# same mechanism MateriaForge uses for its own config: template VDFs in
# controller_base/templates appear under "Templates" in the layout picker
installed_template=0
for steam_root in "$HOME/.steam/steam" "$HOME/.local/share/Steam"; do
    if [ -d "$steam_root/controller_base/templates" ] && [ -f "$TEMPLATE_VDF" ]; then
        cp -f "$TEMPLATE_VDF" "$steam_root/controller_base/templates/"
        echo "Installed Steam Input template to $steam_root/controller_base/templates/"
        installed_template=1
        break
    fi
done

if [ "$installed_template" -eq 0 ]; then
    echo "warning: Steam controller_base/templates dir not found - install the layout manually (see steam-input/LAYOUT.md)" >&2
fi

# ---- reminders ----------------------------------------------------------------

cat <<EOF

Done. Remaining manual steps (details in deck-deploy/README.md):

  1. In Steam, open the 7th Heaven entry's Properties -> Launch Options and
     set them to:  --deck
  2. Restart Steam (or switch to game mode) so the controller template is
     picked up, then select "7th Heaven Deck mode" in the controller layout
     picker and verify BOTH action sets (see steam-input/LAYOUT.md).
  3. Re-run this script after every MateriaForge run or update.
EOF
