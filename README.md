# SPECTRA

SPECTRA is a minimal Windows desktop application for mirroring a Meta Quest headset into the main application window.

## Windows x64 pre-release

Download the latest package from [GitHub Releases](https://github.com/femboy-organization/QuestMirror/releases):

- **Installer:** install SPECTRA and launch it from the Start menu or desktop.
- **Portable ZIP:** extract the archive and run **SPECTRA.exe**.

Both packages are self-contained and include the official scrcpy / ADB tools. A separate .NET installation is not required.

## Requirements and first launch

1. Enable Developer Mode for the headset in the Meta Quest mobile app and accept the USB debugging prompt inside the headset when using USB.
2. Launch SPECTRA and press **Refresh devices**.
3. For wireless use, enable **Developer options → Wireless debugging** in the headset. Enter the pairing address and pairing code shown by Quest, press **Pair headset**, then enter the separate device address and port shown by Wireless debugging and press **Connect over Wi-Fi**.
4. Select the device and press **Start stream**. The scrcpy video window is hosted inside SPECTRA. Press **Fullscreen** to expand the application; press **Esc** to leave fullscreen.

The pairing endpoint and the ADB connection endpoint use different ports. ADB must report the state **device** before SPECTRA enables streaming. If the device is **unauthorized**, unlock the headset and accept the debugging prompt.

## Streaming

- Automatic quality is enabled by default (1920 px max size, 60 FPS, 8 Mbps video).
- Manual quality provides supported presets for maximum size, FPS, and video bitrate. Stop streaming to edit quality; changes apply to the next stream.
- Audio, recording, screenshots, and headset control are not included.
- SPECTRA rechecks the ADB device state and stops the stream if the device disconnects.

## Build and test

Pinned SDK: .NET 10.0.403 (global.json).

PowerShell:

    dotnet restore SPECTRA.sln
    dotnet test SPECTRA.sln --configuration Release
    dotnet publish src/SPECTRA.App/SPECTRA.App.csproj -c Release -r win-x64 --self-contained true

The GitHub Actions workflow downloads the pinned upstream scrcpy v5.0.1 Windows bundle, verifies its SHA-256, builds the app, runs unit tests, creates the installer and portable ZIP, and publishes a GitHub Pre-release from main.

## Validation status

Automated tests cover ADB output parsing, wireless endpoint validation, quality configuration, and settings persistence. Actual USB/Wi-Fi streaming, device authorization, fullscreen behavior with video active, and cross-process window embedding must still be validated with a physical Meta Quest on Windows.
