
DivaModManager by Hanserm v1.4.0

## Credits

* Based on DivaModManager by Enomoto (https://github.com/enomoto-r02/DivaModManager-by-Enomoto)
* Originally created by TekkaGB (DivaModManager)
* Licensed under GPL-3.0

## Changes in this fork

* Fixed config.toml retry logic: a temporary file lock could leave the enabled mod list unsaved, so mods were not applied in game.
* Launch: warns if the game is already running instead of silently failing.
* Launch: uses Steam to start the game when Steam is not running, avoiding the first launch closing itself.
* Launch: warns when the mod configuration could not be saved.
* New Conflicts button: shows enabled mods that overwrite each other's files, song IDs, module IDs or item IDs, and which one is used.
* Module tab reads module tables inside .farc archives.
* Implemented the Console option (it was disabled and unfinished): turns the DivaModLoader console window on or off.
* Fixed read-only folders from archives blocking cleanup of temporary folders.
* Downloads: interrupted or stalled downloads now retry automatically and resume where they stopped (no more stuck progress bars).
* Downloads: server errors and incomplete files are reported instead of being installed as a broken mod.
* Downloads: added Pause/Resume button, download speed and time remaining.
* Fixed the update dialog showing the wrong file size (e.g. 5.9 KB for DivaModLoader).
* Extraction: fixed a deadlock where large archives froze the manager forever (7-Zip output was never read).
* Extraction: added a progress window with percentage; closing it cancels the extraction.
* Extraction: the window no longer freezes while extracting.
* Fixed temporary extraction folders (Downloads\temp_*) never being deleted, which wasted gigabytes of disk space.
* Extraction: the 260-character path check now measures the final path in the game's mods folder instead of the temporary folder, so mods are no longer rejected just because DMM is stored in a deep folder.

## Notice (Please Read)

* Use this software at your own risk.
* The author assumes no liability whatsoever for any issues arising from the use of this program.

## Tested Environment

* Windows 11 Home 25H2

## Changes

* Please refer to GitHub or GameBanana for previous versions.
* This section describes changes since the previous recommended release (excluding alpha and beta versions).

### About Internal Processing

* Major internal revisions have been made, too extensive to list in full.

### Implemented a Cache System to Reduce Access Frequency to GameBanana and DivaModArchive and Improve Loading Speed

* If your network connection is slow or when using the "GameBanana" tab, the initial loading may be slower than in previous versions. Cache files will also accumulate in the DivaModManager/cache folder.
* Once the cache has been generated, faster loading times and reduced excessive API access can be expected.
* It is always enabled in the Mods tab to reduce server load.
* The following cache retention periods are currently configured:
  GameBanana and DivaModArchive API cache: 3 hours
  GameBanana and DivaModArchive image cache: 72 hours

### Added an Experimental Module Tab

* Please refer to GitHub for usage instructions.
* This feature is intended for advanced Mod users, so please refrain from asking questions about it.

### Included an Error Report Template

* If you encounter a reproducible error, it would be helpful if you could report it using the included template.

### Updated the UI

* Considered the screen size of the Steam Deck (I wish I had a Steam Deck or a Steam Machine.)

### Added GitHub Sponsors Messages and Links

* Your support would be greatly appreciated.

