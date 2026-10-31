# SPECTRA

[![Windows build](https://github.com/femboy-organization/SPECTRA/actions/workflows/dotnet-build.yml/badge.svg?branch=main)](https://github.com/femboy-organization/SPECTRA/actions/workflows/dotnet-build.yml)
[![Latest pre-release](https://img.shields.io/github/v/release/femboy-organization/SPECTRA?include_prereleases&label=latest%20pre-release)](https://github.com/femboy-organization/SPECTRA/releases)
[![MIT License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

SPECTRA is a Windows x64 desktop application for mirroring a Meta Quest headset inside the main application window. It uses ADB for device connectivity and scrcpy for video streaming.

> **Pre-release software:** SPECTRA is still being validated. USB/Wi-Fi streaming and native video-window embedding need testing with a physical Meta Quest headset on Windows.

## Download

Get the latest pre-release from [GitHub Releases](https://github.com/femboy-organization/SPECTRA/releases).

- **Windows installer:** run the `SPECTRA-*-Setup-win-x64.exe` installer and launch SPECTRA from the Start menu or desktop.
- **Portable version:** extract `SPECTRA-*-win-x64.zip` and run the root `SPECTRA.exe`. It launches the application from `bin\SPECTRA.exe`; application DLLs and runtime files live under `bin`, while scrcpy/ADB are in `bin\tools`.
- **Integrity checks:** each package has a matching `.sha256` file attached to the release.

Both packages are self-contained and include the official scrcpy/ADB tools. A separate .NET installation is not required to run the packaged application.

## Requirements and first launch

- Windows x64.
- A Meta Quest headset with Developer Mode enabled.
- USB connection and debugging authorization, or a Wi-Fi network that allows the headset and PC to communicate.

1. Enable **Developer Mode** for the headset in the Meta Quest mobile app. For USB, connect the headset and accept the USB debugging prompt inside the headset.
2. Launch SPECTRA and select **Refresh devices**.
3. For wireless use, open **Developer options → Wireless debugging** in the headset. Enter the pairing address and pairing code shown by Quest, then select **Pair headset**.
4. Enter the separate device address and port shown on the Wireless debugging screen and select **Connect over Wi-Fi**.
5. Select the connected device and press **Start stream**. The scrcpy video window is hosted inside SPECTRA.
6. Press **Fullscreen** to expand the application; press **Esc** to leave fullscreen.

The wireless pairing endpoint and ADB connection endpoint use different ports. SPECTRA enables streaming only when ADB reports the device state as `device`. If the state is `unauthorized`, unlock the headset and accept the debugging prompt.

## Diagnostics and logs

SPECTRA writes a daily diagnostic log to `%LOCALAPPDATA%\SPECTRA\Logs\spectra-YYYY-MM-DD.log`. Launcher startup failures are recorded in `launcher-YYYY-MM-DD.log` in the same directory. If LocalAppData is unavailable, launcher logging falls back to `%TEMP%\SPECTRA\Logs`, then a `logs` folder next to the launcher. Logs include application lifecycle events, ADB/scrcpy process failures, window embedding errors, unhandled exceptions and timing measurements used to diagnose startup or streaming performance. Wireless pairing codes are not logged. Attach the latest log file when reporting a bug.

## Streaming

- **2D display mirroring:** scrcpy explicitly captures the Android display as a normal flat 2D video stream; SPECTRA does not add stereoscopic or volumetric rendering.\n- **Automatic quality** is enabled by default: 1920 px maximum size, 60 FPS, and 8 Mbps video bitrate.
- **Manual quality** offers presets for maximum size, FPS, and video bitrate. Stop streaming before editing quality; changes apply to the next stream.
- **Disconnect handling:** SPECTRA rechecks the ADB device state and stops the stream if the device disconnects.
- **Not included:** audio, recording, screenshots, and headset control.

## Build and test

The repository pins the .NET SDK to **10.0.403** in `global.json`. Use PowerShell or a terminal with that SDK installed:

```powershell
dotnet restore SPECTRA.sln
dotnet build SPECTRA.sln --configuration Release --no-restore
dotnet test SPECTRA.sln --configuration Release --no-build
```

The GitHub Actions workflow builds and tests the application and launcher on pull requests. For pushes to `main`, it downloads the official scrcpy v5.0.1 Windows bundle, verifies its SHA-256, publishes the main self-contained application into `bin`, publishes the root `SPECTRA.exe` launcher as a self-contained single-file .NET app, validates that there are no root-level DLL files, starts the exact packaged launcher in a smoke test and confirms the application writes its startup log, then creates the installer and portable ZIP and publishes a GitHub pre-release. The release workflow is the supported path for producing complete distribution packages; a local `dotnet publish` without the bundled scrcpy tools and launcher packaging step is not a complete package.

## Project status and limitations

Automated tests cover ADB output parsing, wireless endpoint validation, quality configuration, settings persistence, and process handling. Physical-device validation is still required for:

- USB and Wi-Fi connection and streaming on real Meta Quest hardware.
- Device authorization and disconnect recovery.
- Fullscreen transitions while video is active.
- Cross-process native window embedding across supported Windows systems and graphics drivers.

SPECTRA currently targets Windows x64. Linux and macOS are not supported at runtime.

## Contributing and security

Read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request. Report suspected vulnerabilities using the process in [SECURITY.md](SECURITY.md), not through a public issue. Bug reports and feature requests can be opened with the repository's [issue templates](https://github.com/femboy-organization/SPECTRA/issues/new/choose).

## License

SPECTRA source code is released under the [MIT License](LICENSE). Bundled third-party components remain under their respective licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Video framing

SPECTRA mirrors one eye at a time from a side-by-side headset display. Choose the
left or right eye in **Video framing** and adjust rotation from -180° to +180°
(clockwise). The crop is calculated from the active Android display dimensions
reported by ADB. Framing changes apply when the next stream starts.

## 16:9 video preview

The selected eye is cropped to a centered 16:9 rectangle that remains inside the
circular lens viewport. The desktop preview host uses the same 16:9 aspect ratio,
so the image is neither square nor stretched and the circular lens boundary is
not included in the video frame.
