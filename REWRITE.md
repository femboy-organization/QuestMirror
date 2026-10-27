# SPECTRA v1 rewrite

The desktop app is implemented in C#/.NET 10 and Avalonia UI. The old Electron implementation is not used by the new project.

## Architecture

- **SPECTRA.App:** Avalonia UI, MVVM-style MainViewModel, fullscreen handling and the Windows native video host.
- **SPECTRA.Core:** ADB command execution, device-state parsing, settings persistence, tool resolution, quality options, and scrcpy process lifecycle.
- **SPECTRA.Core.Tests:** unit tests for ADB parsing and outcomes, input validation, quality presets, and settings.
- **build/installer/SPECTRA.iss:** Inno Setup installer definition.
- **.github/workflows/dotnet-build.yml:** pinned .NET SDK build, tests, upstream dependency hash validation, packaging, and pre-release publication.

## Runtime behavior

ADB is invoked with ProcessStartInfo.ArgumentList rather than shell-built command strings. Process I/O is redirected, operations have timeouts, and the process runner terminates timed-out process trees. ADB results distinguish authorized (device), unauthorized, offline, and unavailable states.

scrcpy is started without audio or control support. Its window title is uniquely generated, found by process ID, and re-parented to a hidden native child window inside the Avalonia preview area. The child is resized with the host control, allowing the desktop window and fullscreen state to change without restarting the stream. This Windows-native embedding strategy requires headset / GPU validation on physical Windows hardware; it has not been declared hardware-verified.

Settings are saved atomically under the current user's Local Application Data folder. Automatic mode uses a fixed preset; manual mode validates resolution, FPS, and bitrate before building scrcpy arguments. The UI disables quality editing while a stream is running so changes take effect on the next stream.

## Build and release

The root global.json pins SDK 10.0.403. The release workflow downloads the official scrcpy v5.0.1 Windows x64 archive and validates its published SHA-256 before packaging it. It creates a self-contained Windows x64 publish directory, installer, portable ZIP, and SHA-256 sidecar files. Publishing occurs only after test, build, package structure, and size checks pass.

## Known limitations

- The video integration uses Windows SetParent / child-window hosting with the official scrcpy SDL window. This implementation needs physical device and GPU testing, including repeated start/stop and fullscreen transitions.
- Audio capture and playback are intentionally excluded.
- Wireless pairing needs the pair endpoint/code and the separate Wireless debugging connect endpoint shown by Quest.
- Linux and macOS are not currently supported at runtime; their future support requires native window hosting and packaging backends.
