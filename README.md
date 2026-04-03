# Dune Playnite Addon 🎮

The **Dune Save Sync** Playnite extension is a generic plugin designed to automatically manage and sync game saves directly from within the Playnite game launcher.

## Overview
This add-on connects your Playnite library directly to the overarching Dune ecosystem. It silently monitors your game paths, detects save files, connects to the Dune Server via API, and seamlessly syncs backup files with minimal user intervention.

## Features
- **Seamless Integration:** Runs natively inside Playnite as a Generic Plugin.
- **Save Scanning:** Auto-detects supported save directories for your configured Playnite games.
- **Direct Sync:** Offloads the backup process directly to your Dune Server instance via `DuneApiClient`.
- **Configurable:** Ships with local settings (`DuneSettings`) to control paths, API endpoints, and backup intervals.

## Building and Installation
1. Open the `DunePlayniteAddon.csproj` project using Visual Studio or VS Code.
2. Build the project using MSBuild or the `.NET` CLI.
3. Package the output into a `.pext` Playnite extension file, or manually drag the compiled `DunePlayniteAddon.dll` and `extension.yaml` into your Playnite extensions repository.
4. Restart Playnite and configure your server settings via the Add-on Configuration menu.
