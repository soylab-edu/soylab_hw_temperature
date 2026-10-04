Windows x64 standalone temperature monitor. Download **SoyTemperature.exe** directly or extract the ZIP and open the **1.0.0** folder. No separate .NET installation or font installation is required.

- Minimal screen: only CPU and GPU temperatures; SSD monitoring removed.
- Rounded borderless window and cards, generous spacing, embedded Ubuntu Bold typography and SOYLAB purple/accent colors.
- Subtle 60-second graphs above each temperature. CPU and GPU cards use different warm/cool purple backgrounds with coral/lavender traces; history continues in tray mode.
- Bold light-purple rounded edge around the outer window.
- Apple-style top-left controls: red exits the app, yellow minimizes to the temperature tray, green opens https://soylab.ai/ in the default browser. The previous bottom tray button is removed.
- Drag cards/background to move the window; drag window edges to resize. Compact layout and resize hit testing verified.
- Improved borderless resizing: rounded corners and edges show resize cursors and start resize gestures instead of window dragging. A small bottom-right resize grip makes the control visible. Minimum size is now 260×180 logical px, with compact/wide/tall layout checks.
- Fixed missing CPU temperatures after double-clicking: Windows now requests administrator access at startup. Accept the UAC prompt.
- CPU/GPU temperature numbers continue updating in the system tray every 2 seconds.
- History, CSV export, driver installation and exit are available in the context menu.
- SOYLAB icon with a large centered thermometer remains embedded.

Verified on this PC with administrator access: CPU and GPU temperatures are readable; tray hide/restore, compact layout and resize edge checks pass.
