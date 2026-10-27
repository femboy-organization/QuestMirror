# SPECTRA clean rewrite

The new implementation is being built with C#/.NET and Avalonia UI. It is separated from the legacy Electron implementation while the replacement is developed and validated.

## Projects

- `SPECTRA.App`: Avalonia desktop UI and UI composition.
- `SPECTRA.Core`: platform-independent application logic and models.
- Platform-specific integrations will be isolated behind interfaces as Windows, Linux, and macOS support is implemented.

## Principles

1. Keep UI code separate from device and streaming logic.
2. Use asynchronous process I/O, cancellation, timeouts, and explicit process lifetime management.
3. Do not shell-concatenate arguments; use `ProcessStartInfo.ArgumentList`.
4. Treat ADB discovery and scrcpy streaming as separate services.
5. Never claim streaming is working until the video output is actually rendered inside the application window.
6. Build and test on Windows, Linux, and macOS in CI.

## Current status

The first scaffold contains the Avalonia window, a minimal asynchronous ADB device scan, and a cross-platform build workflow. It does not yet implement scrcpy session management or embedded video rendering. Those are separate milestones and must be tested before calling the rewrite feature-complete.

## Local development

Install the .NET 8 SDK, then run:

```sh
dotnet restore SPECTRA.sln
dotnet build SPECTRA.sln
dotnet run --project src/SPECTRA.App/SPECTRA.App.csproj
```
