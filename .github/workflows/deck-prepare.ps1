# Environment prep for the Deck-mode release build.
#
# Mirrors ONLY the vcpkg setup from .github/workflows/prepare.ps1, deliberately
# dropping the parts that belong to the Tsunamods release pipeline:
#   - Inno Setup install (the installer build we don't do)
#   - canary/release env-var juggling and the tag-version rewrite
#   - the GitHub Packages NuGet source (all our packages are on nuget.org; the
#     only reason the stock script adds it is vcpkg binary caching, which we
#     skip so this workflow needs no tokens/permissions beyond the defaults)
#
# AppLoader (native C++) builds via a vcpkg manifest (AppLoader/vcpkg.json), so
# vcpkg must be pinned to the manifest baseline, bootstrapped, and integrated
# with MSBuild before the solution build.

$ErrorActionPreference = "Stop"

# GitHub-hosted Windows runners ship vcpkg and expose its path here; fall back to
# the well-known location the stock script hard-codes.
$vcpkgRoot = if ($env:VCPKG_INSTALLATION_ROOT) { $env:VCPKG_INSTALLATION_ROOT } else { "C:\vcpkg" }

# Read the baseline commit the manifest pins (jq is preinstalled on the runner).
$vcpkgBaseline = [string](jq --arg baseline "builtin-baseline" -r '.[$baseline]' AppLoader/vcpkg.json)

Write-Output "--------------------------------------------------"
Write-Output "VCPKG ROOT:     $vcpkgRoot"
Write-Output "VCPKG BASELINE: $vcpkgBaseline"
Write-Output "--------------------------------------------------"

# Pin vcpkg to the manifest's baseline commit, exactly like the stock pipeline.
git -C $vcpkgRoot fetch --all --tags
git -C $vcpkgRoot checkout $vcpkgBaseline
git -C $vcpkgRoot clean -fxd

cmd.exe /c "call `"$vcpkgRoot\bootstrap-vcpkg.bat`""

& "$vcpkgRoot\vcpkg.exe" integrate install
