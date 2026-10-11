# Contributing to ShuffleDrop

Contributions are welcome under the repository's MIT license; preserve third-party dependency licenses.

## Development

Run `restore-dependencies.ps1`, `build.ps1`, and `tests/run.ps1` on Windows. Default tests use temporary synthetic devices, not connected hardware. Keep file-format changes compatible with legacy and modern `iTunesSD` layouts, and add a regression test for meaningful changes to transfers or recovery.

Never use a connected iPod as a disposable test device. Device writes should remain explicit actions, and failures must preserve recovery information. Do not turn an unavailable battery reading into an estimated percentage.

## Issues and pull requests

- Explain the problem, reproducible steps, and expected behavior.
- Include Windows version, Shuffle generation, app version, and relevant error text.
- State which checks you ran. Distinguish simulated tests from physical-device playback.
- Do not upload songs, serial numbers, device database dumps, or personal backups.
- Keep changes focused. For substantial UI or format changes, open an issue to discuss the approach.

Accessibility improvements and real-hardware compatibility reports are especially welcome.
