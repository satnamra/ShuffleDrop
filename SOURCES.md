# Format references

Implementation is original C#, written from public file-format descriptions. TagLibSharp 2.3.0 is bundled separately to read music metadata.

- Modern 3G / 4G iTunesSD format, 64-byte `bdhs` header, `hths`, 372-byte `rths`, `hphs`, `lphs`: https://github.com/nims11/IPod-Shuffle-4g/blob/master/docs/iTunesSD3gen.md
- Reference implementation consulted for field interpretation: https://github.com/nims11/IPod-Shuffle-4g/blob/master/ipod-shuffle-4g.py (GPL-3.0 repository; no source copied into the distributed application).
- Legacy database builder and record layout reference (18-byte header, big-endian 24-bit count, 558-byte records, UTF-16LE paths): https://github.com/kemenril/3build_db (GPL-2.0 repository; no source copied into the distributed application).
- libgpod `itdb_rename_files` invalidates `iTunesShuffle` and `iTunesStats` after database changes: https://github.com/gtkpod/libgpod/blob/master/src/itdb_itunesdb.c
- Apple model identification: https://support.apple.com/en-us/103823
- USB identifiers: https://github.com/vcrhonek/hwdata/blob/master/usb.ids (`05ac:1301` = iPod Shuffle 2.Gen; `05ac:1303` = iPod Shuffle 4.Gen).

Read-only hardware validation used a second-generation Shuffle with a legacy 0x010800 iTunesSD database. The private device fixture is excluded from the repository and distributable archives. This confirms database compatibility, not playback of newly transferred audio on hardware.


## Metadata library

Battery guidance: https://support.apple.com/en-us/102158 (2nd and 4th generation LED ranges and 4th generation VoiceOver instructions). Local read-only checks found no battery percentage in Win32_Battery, matching iPod PnP battery properties, or the device drive's Shell battery properties. This does not prove no proprietary protocol exists; the application explicitly reports unknown and does not estimate a percentage or charging state.

TagLibSharp 2.3.0, LGPL-2.1-only: https://www.nuget.org/packages/TagLibSharp/2.3.0
Source tag: https://github.com/mono/taglib-sharp/tree/TaglibSharp-2.3.0.0
The bundled net462 DLL is unmodified. third-party includes the license and corresponding sources/build files. Large test media are omitted from the source archive. The DLL may be replaced with a compatible custom build. Reverse engineering to debug library modifications is permitted.

## UI and audio, version 0.3

- Krypton.Toolkit 90.25.2.55, net472, BSD-3-Clause: https://www.nuget.org/packages/Krypton.Toolkit/90.25.2.55 and https://github.com/Krypton-Suite/Standard-Toolkit
- NAudio.Core and NAudio.Wasapi 2.2.1, MIT: https://github.com/naudio/NAudio/tree/v2.2.1
- Playback patterns: https://github.com/naudio/NAudio/blob/v2.2.1/Docs/PlayAudioFileWinForms.md
- Fluent 2 color, theme and contrast guidance: https://fluent2.microsoft.design/color
- Further battery investigation: https://github.com/dstaley/ipod-sysinfo/blob/main/models/iPod%20shuffle/2nd%20generation/SysInfoExtended.plist contains power capabilities, not a current battery percentage. No verified live battery reader has been implemented. The UI explicitly says unsupported.

All UI/audio libraries are unmodified official NuGet DLLs. Krypton 90.25.2.55 was selected for its net472 build without additional package dependencies. Only NAudio.Core and NAudio.Wasapi are used; the NAudio umbrella and WinMM packages are not shipped. The app icon is original vector-style artwork rendered locally by the program. No music files, private device snapshots, or screenshots of the user's song library are included in the distributable.
