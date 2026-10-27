# Contributing to SPECTRA

Contributions that improve reliability, usability, documentation, and test coverage are welcome. SPECTRA is pre-release software, and hardware-dependent behavior should be described accurately.

## Before opening an issue

- Read the [README](README.md), including setup steps and known limitations.
- Check [existing issues](https://github.com/femboy-organization/QuestMirror/issues) to avoid duplicates.
- Include the SPECTRA version, Windows version, connection mode (USB or Wi-Fi), and reproducible steps when reporting a bug.
- Remove personal information, device identifiers, pairing codes, IP addresses, and other sensitive details from logs before sharing them.

## Development setup

1. Use Windows x64 for UI and native-window integration work.
2. Install the .NET SDK pinned in [global.json](global.json): **10.0.403**.
3. Clone the repository and run these commands from its root:

   ```powershell
   dotnet restore SPECTRA.sln
   dotnet build SPECTRA.sln --configuration Release --no-restore
   dotnet test SPECTRA.sln --configuration Release --no-build
   ```

The CI workflow downloads the upstream scrcpy bundle and verifies its checksum before creating distributable packages. Do not commit downloaded executables or generated build output.

## Pull requests

- Keep changes focused and describe the behavior changed and why.
- Add or update unit tests for logic changes where practical.
- Run the build and test commands above, and report any checks that could not be run.
- Update the README or other documentation when user-visible behavior, requirements, or limitations change.
- Do not claim physical-device validation unless you actually tested with a headset. State the headset/Windows setup and results when hardware testing is relevant.
- Preserve third-party licensing and notices. Do not replace upstream binaries or omit their license files without reviewing the relevant license requirements.
- Use a descriptive pull request title and explain known risks or follow-up work.

By submitting a contribution, you agree that the contribution may be distributed under the repository's MIT License. You retain copyright to your own contributions.
