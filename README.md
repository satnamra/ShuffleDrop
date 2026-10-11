<img src="assets/ShuffleDrop.png" width="80" alt="ShuffleDrop icon">

# ShuffleDrop

**A free, open-source Windows music manager for iPod Shuffle — without iTunes.**

Browse music already on your player, add MP3 files, listen on your computer, export songs, and arrange playback order. Includes light and dark themes. The current interface is in Lithuanian.

**[Download for Windows](https://github.com/satnamra/ShuffleDrop/releases/tag/v0.3.0)** · **[Lietuviška instrukcija](docs/README.lt.md)** · **[Report a problem](https://github.com/satnamra/ShuffleDrop/issues)**

## Install

1. Open the release page above and download **ShuffleDrop-Setup-0.3.0.exe**.
2. Run the installer, then open ShuffleDrop from the Start menu.
3. Connect an already initialized iPod Shuffle. Existing songs appear in **Biblioteka**.
4. Open **Pridėti muziką**, choose MP3 files, then select **Perkelti į iPod**.
5. Use Windows to safely eject the player when finished.

The installer includes all app libraries and installs for the current user without administrator rights. Requires **.NET Framework 4.7.2 or later** on Windows. Windows N editions may need Microsoft's Media Feature Pack for audio preview. The release is not digitally signed.

A portable ZIP is also available on the release page: extract the entire folder before running `ShuffleDrop.exe`.

## Features

- Existing library with title, artist, album, duration, size, and search.
- MP3 import using a file picker, folders, or drag and drop; identical file contents are skipped.
- Computer playback with play/pause, seek, volume, and embedded album artwork.
- Export selected songs to the computer with readable filenames.
- Remove songs with verified local recovery copies; undo the most recent operation.
- Reorder songs for sequential playback and explicitly save the order.
- Light/dark theme and an original app icon.

## Compatibility and current limits

**Experimental release: keep a separate copy of important music before changing the player.** Simulated transfers, removals, reordering, and recovery are tested in temporary folders. Existing music and the legacy database have been read from a real second-generation Shuffle. **Playback of newly transferred music on physical hardware has not yet been verified.** Third/fourth-generation compatibility is based on published formats and synthetic tests, not hardware validation.

- Supports legacy first/second-generation and modern third/fourth-generation `iTunesSD` layouts on an already initialized FAT/FAT32 player.
- Does not initialize, format, restore firmware, or repair an unsupported device.
- **Battery percentage is not supported.** The app shows this explicitly rather than estimating a value.
- Imports MP3 only. No FLAC/M4A conversion, tag editing, or creation of new playlists.
- Does not generate new VoiceOver announcements.
- Removal from mixed third/fourth-generation podcast/audiobook libraries is not supported.
- Updates `iTunesSD`, not `iTunesDB`; a later sync through iTunes or another manager can replace the library.
- Playback order applies in sequential mode, not random shuffle mode.
- Recovery covers the latest operation and depends on backups on the same computer and Windows account. Later operations replace the recovery point. Interrupted imports can leave unlisted audio files; automatic cleanup is not implemented.

Backups are stored in `%LOCALAPPDATA%\ShuffleDrop\Backups`; transaction records are stored under `iPod_Control\ShuffleDrop\transactions` on the player. Backups are not automatically deleted. Uninstalling preserves local backups and preferences.

## Build from source

Use Windows PowerShell with .NET Framework 4.7.2 or newer installed:

```powershell
.\restore-dependencies.ps1
.\build.ps1
.\tests\run.ps1
```

The restore script downloads pinned official NuGet packages and verifies each runtime DLL against `dependency-hashes.json`. The build uses the Windows .NET Framework C# compiler and produces `dist\ShuffleDrop.exe` with required libraries and notices. The default tests use generated local fixtures and do not require an iPod.

For the installer, obtain the **NSIS 3.13 portable ZIP** from [the official NSIS download page](https://nsis.sourceforge.io/Download), extract it so `tools\nsis-3.13\Bin\makensis.exe` exists, and run:

```powershell
.\build-installer.ps1
```

The installer and SHA-256 file appear in `releases`. Optional `UITests.cs` and `AudioTests.cs` are local interactive/device smoke tests, separate from the default suite. Do not submit music, device databases, serial numbers, or backups with bug reports.

## Contributing

Bug reports, hardware compatibility reports, translations, and pull requests are welcome. Include the Shuffle generation, Windows version, app version, and whether importing, on-device playback, and recovery were tested. Remove personal information from logs or screenshots.

English localization and broader physical-device testing would be useful contributions. See [CONTRIBUTING.md](CONTRIBUTING.md).

## License and acknowledgments

ShuffleDrop is **free to use, modify, and share under the [MIT license](LICENSE)**. No subscription or paid activation is required. Third-party components retain their own licenses:

| Component | Version | License |
| --- | --- | --- |
| TagLibSharp | 2.3.0 | LGPL-2.1-only |
| Krypton.Toolkit | 90.25.2.55 | BSD-3-Clause |
| NAudio.Core / NAudio.Wasapi | 2.2.1 | MIT |
| NSIS installer runtime | 3.13 | See bundled NSIS notices |

Notices and TagLibSharp corresponding source/build files are in [third-party](third-party). The TagLibSharp DLL is separate and replaceable with a compatible custom build; debugging modifications to that library is permitted. Original format references and attribution are in [SOURCES.md](SOURCES.md).

ShuffleDrop is an independent community project, not affiliated with or endorsed by Apple. iPod and iTunes are Apple trademarks.
