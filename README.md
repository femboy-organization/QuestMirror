# SPECTRA

SPECTRA is a desktop companion for mirroring a Meta Quest headset to a computer.

## Clean rewrite

The new implementation is being developed in C#/.NET 10 with Avalonia UI, with Windows, Linux, and macOS as the intended desktop targets. The previous Electron implementation is retained during the rewrite so the replacement can be built and verified before the old app is retired.

See [REWRITE.md](REWRITE.md) for architecture and implementation status.

## Requirements

- .NET 10 SDK
- Android Debug Bridge (ADB) / Android platform-tools for device discovery
- A compatible Meta Quest headset

## Build and run

```sh
dotnet restore SPECTRA.sln
dotnet build SPECTRA.sln --configuration Release
dotnet run --project src/SPECTRA.App/SPECTRA.App.csproj
```

The current scaffold includes the Avalonia UI and an asynchronous ADB device scan. Embedded scrcpy streaming is not implemented yet.

## Cross-platform scope

The UI and core library are designed to build on Windows, Linux, and macOS. Device tools, process lifecycle, packaging, permissions, and video rendering still require platform-specific validation before those platforms can be considered supported.
