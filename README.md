# SPECTRA

SPECTRA is a Windows desktop app for mirroring the display of a Meta Quest headset to your PC. It is based on Electron and scrcpy.

## Download

Open [GitHub Releases](https://github.com/femboy-organization/QuestMirror/releases) and download the `SPECTRA-v0.1.0-pre.1-win-x64.zip` asset from the pre-release. Extract the whole ZIP to a folder before launching `SPECTRA.exe`. Do not copy the EXE out of the extracted directory: Electron needs its adjacent runtime DLLs and resource files.

This build targets Windows x64. The first launch downloads scrcpy and ADB into the current user's application data directory and may take longer than subsequent launches.

## Connect a headset

1. Enable Developer Mode on the Meta Quest headset.
2. Connect the headset to the PC with a USB data cable and put on the headset.
3. Accept the USB debugging prompt on the headset.
4. Start mirroring in SPECTRA after the device status changes to **Device Ready**.
5. For wireless mirroring, establish the initial USB connection first, then select Wi-Fi in the app and follow the connection status before unplugging the cable.

The app's configuration tab contains crop, maximum size, FPS, fullscreen, window title, border, and recording settings. Close an active stream before changing settings that are applied on the next start.

## Build from source

Requires Node.js 24 and npm. From the repository root:

```powershell
npm install
npm run build
npm start
```

To package the Windows x64 application locally:

```powershell
npx --yes @electron/packager@20.3.0 . SPECTRA --platform=win32 --arch=x64 --no-asar --out=artifacts --overwrite
```

The [Windows pre-release workflow](https://github.com/femboy-organization/QuestMirror/actions/workflows/windows-prerelease.yml) builds and packages the application on a Windows GitHub Actions runner and publishes the pre-release ZIP with a SHA-256 checksum.

## License

Apache-2.0. See [LICENSE](LICENSE). The application uses Electron and scrcpy; refer to their upstream licenses for bundled runtime components.
