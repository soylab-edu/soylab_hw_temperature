# Dependencies

- LibreHardwareMonitorLib 0.9.6: MPL-2.0. Unmodified library from NuGet. Source: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/3d331e3370efb858411f19511373eff65a218701. License and upstream third-party notices are included in `licenses/`.
- .NET 10 Windows Desktop runtime: MIT and third-party licenses. Source and notices: https://github.com/dotnet/runtime and https://github.com/dotnet/winforms. Runtime license files are bundled in the self-contained executable and extracted with the runtime.
- NuGet dependencies and exact versions are recorded in `packages.lock.json`. Upstream LibreHardwareMonitor notices describe additional components.
- PawnIO 2.2.0 is downloaded directly from https://github.com/namazso/PawnIO.Setup/releases/tag/2.2.0 only when the user selects driver installation. The installer is not redistributed by this repository or executable. Driver source and GPL-2.0 license: https://github.com/namazso/PawnIO. Installer source: https://github.com/namazso/PawnIO.Setup.

The palette is taken from the user's SOYLAB Comfy Router project: https://github.com/soylab-edu/ComfyUI-soylab-router/blob/ffda499/web/router.js. SF Pro is selected only when already installed; no Apple font files are redistributed.
