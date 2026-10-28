# Polytopia Single Player Library

A standalone PolyMod client mod for multiple single-player saves and portable local replays.

[中文说明](README.md) · [Development](docs/DEVELOPMENT.md) · [Verification](VERIFICATION.md)

## Compatibility

Version **0.1.7** targets **Polytopia 2.17.3.16375**, **PolyMod 1.2.18**, and Windows Steam. Supported rules: Perfection, Domination, and Creative Perfection / Domination / Infinity. Agent Bridge is optional. Pass & Play, weekly challenges, other game versions, and arbitrary rule-mod combinations are outside the verified scope.

## Features

- A SINGLE PLAYER menu with Ongoing and Replays tabs.
- Independent saves, details, duplication, and confirmed deletion.
- Local self-contained `.polyreplay` files using the native replay client, timeline and perspective controls.
- Actual-rule labels for Creative games.
- An Infinity-only TERMINATE action inside the in-game Settings menu. Confirmation ends the game with YOU WIN and an optional replay save prompt, without score statistics on the result page.

## Installation

Install the supported game and PolyMod versions, quit the game, and place `single_player_library-0.1.7.polymod` from Releases in the actual game's `Mods` directory. Keep only one enabled copy. Icons are embedded; do not install the DLL separately.

Data is stored under the active game user's `SinglePlayerLibrary` directory, usually `%USERPROFILE%/AppData/LocalLow/Midjiwan/Polytopia/<user-id>/SinglePlayerLibrary/`. Use OPEN FOLDER to locate saves or replays.

To share a replay, copy its `.polyreplay` file into the recipient's `Replays` folder and refresh. The same game version and this mod are required. Incomplete-history saves cannot become complete replays. The mod does not use the official replay upload/query service.

Disabling the mod restores the original menu and preserves mod data. See the Chinese guide for guarded install/disable and vanilla-save restoration commands.

## Build and license

Use PowerShell 7 and a local supported game/PolyMod installation; see [development instructions](docs/DEVELOPMENT.md). Python tools use the standard library. No proprietary binaries or user data are included.

Project code and supplied custom icons are provided under the [MIT License](LICENSE). Existing notices are preserved. Game assets, fonts and trademarks remain with their respective owners and are not redistributed; see [notices](THIRD_PARTY_NOTICES.md).
