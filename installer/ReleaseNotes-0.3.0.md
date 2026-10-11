# ShuffleDrop 0.3.0 — first public preview

A free, open-source Windows music manager for iPod Shuffle. No iTunes installation, subscription, or paid activation is required. The current app interface is Lithuanian.

## Download

- **ShuffleDrop-Setup-0.3.0.exe** — recommended installer; includes libraries and installs for the current user without administrator rights.
- **ShuffleDrop-Portable-0.3.0.zip** — extract the entire archive, then run ShuffleDrop.exe.
- **SHA256SUMS.txt** — checksums for both downloads.

Requires .NET Framework 4.7.2 or newer on Windows and an already initialized compatible FAT/FAT32 iPod Shuffle. Windows N editions may need the Media Feature Pack for computer audio playback. The executables are not digitally signed.

## Included

Existing song library and search; MP3 import with duplicate detection; computer playback with pause, seek, volume, and album art; export; removal with local recovery copies; playback-order editing; light/dark themes.

## Preview limitations

Keep a separate copy of important music before changing a player. Legacy first/second-generation and modern third/fourth-generation database layouts are implemented, but **newly transferred music has not yet been verified playing on physical hardware**. Real-hardware checks so far cover reading an existing second-generation library; third/fourth-generation format tests use synthetic fixtures.

Battery percentage is unsupported. No new VoiceOver announcements, format conversion, tag editing, or new playlist creation. Mixed podcast/audiobook library removal is restricted. iTunes can overwrite changes during a later sync. Recovery depends on the latest operation's local backups.

## Validation

- Built from a clean export of the published source with pinned, SHA-256-verified NuGet runtime libraries.
- 116 synthetic data, transfer, removal, and recovery checks passed without device writes.
- Installer, installed-app launch, and uninstallation smoke-tested locally.

Free to use, modify, and redistribute under the repository's MIT license; bundled libraries retain their own licenses. See the README and third-party notices. Bug reports, translations, and hardware testing contributions are welcome.
