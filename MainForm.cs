using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace SoyTemperature;

internal sealed class MainForm : Form
{
    private readonly CancellationTokenSource _stop = new();
    private readonly TemperatureMonitor _monitor = new();
    private readonly AppFonts _fonts = new();
    private readonly Dictionary<string, TemperatureTile> _tiles = new();
    private readonly ContextMenuStrip _menu = new();
    private readonly ToolTip _toolTip = new();
    private readonly Icon _applicationIcon;
    private readonly TrayTemperature _cpuTray;
    private readonly TrayTemperature _gpuTray;
    private readonly ToolStripMenuItem _cpuText = new("CPU: —") { Enabled = false };
    private readonly ToolStripMenuItem _gpuText = new("GPU: —") { Enabled = false };
    private readonly TableLayoutPanel _root = new();
    private readonly WindowMinimizeButton _trayButton = new();
    private readonly string? _verifyDirectory;
    private readonly bool _verifyExit;
    private readonly int _verifySamples;
    private readonly List<MonitorSnapshot> _samples = new();
    private Task? _worker;
    private MonitorSnapshot? _latest;
    private DateTimeOffset? _historyBeforeReset;
    private bool _exitRequested, _closing, _canClose, _trayMode;
    private bool _trayVerified, _restoreVerified, _resetVerified, _responsiveVerified, _resizeVerified, _verificationFinished;

    public MainForm(string[] args)
    {
        _verifyDirectory = ArgumentValue(args, "--verify");
        _verifyExit = args.Contains("--verify-exit");
        _verifySamples = int.TryParse(ArgumentValue(args, "--verify-samples"), out var count) ? Math.Max(6, count) : 6;
        if (_verifyDirectory is not null) Directory.CreateDirectory(_verifyDirectory);
        Text = "";
        AccessibleName = "SOY Temperature";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        _ = Handle;
        BackColor = Theme.Background;
        ForeColor = Color.White;
        Font = new Font("맑은 고딕", 10, FontStyle.Bold);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(S(520), S(300));
        MinimumSize = new Size(S(380), S(230));
        DoubleBuffered = true;
        using (var stream = typeof(Program).Assembly.GetManifestResourceStream("SoyTemperature.AppIcon")!)
            _applicationIcon = new Icon(stream, new Size(S(32), S(32)));
        Icon = _applicationIcon;
        _menu.Items.Add("창 열기", null, (_, _) => RestoreWindow());
        _menu.Items.AddRange([_cpuText, _gpuText, new ToolStripSeparator()]);
        _menu.Items.Add("최저·최고 초기화", null, (_, _) => _monitor.RequestReset());
        _menu.Items.Add("CSV 저장", null, (_, _) => ExportCsv());
        var driver = _menu.Items.Add("CPU 드라이버 설치", null, async (_, _) => await InstallDriverAsync());
        driver.Enabled = !LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled;
        _menu.Items.Add("글꼴 라이선스", null, (_, _) => ShowFontLicense());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("종료", null, (_, _) => { _exitRequested = true; Close(); });
        ContextMenuStrip = _menu;
        _cpuTray = new TrayTemperature("CPU", Theme.Coral, _menu, RestoreWindow);
        _gpuTray = new TrayTemperature("GPU", Theme.Lavender, _menu, RestoreWindow);
        BuildLayout();
        Shown += (_, _) =>
        {
            var area = Screen.FromControl(this).WorkingArea;
            Size = new Size(Math.Min(Width, (int)(area.Width * .9)), Math.Min(Height, (int)(area.Height * .9)));
            UpdateResponsiveLayout();
            _worker = Task.Run(MonitorLoopAsync);
        };
        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized && !_closing) MinimizeToTray();
            else UpdateResponsiveLayout();
        };
        FormClosing += OnClosing;
    }

    private void BuildLayout()
    {
        _root.Dock = DockStyle.Fill;
        _root.ColumnCount = 1;
        _root.RowCount = 2;
        _root.RowStyles.Add(new(SizeType.Percent, 100));
        _root.RowStyles.Add(new(SizeType.Absolute, S(58)));
        Controls.Add(_root);
        _root.MouseDown += DragWindow;
        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        cards.MouseDown += DragWindow;
        string[] categories = ["CPU", "GPU"];
        Color[] colors = [Theme.Coral, Theme.Lavender];
        for (var i = 0; i < 2; i++)
        {
            cards.ColumnStyles.Add(new(SizeType.Percent, 50));
            var tile = new TemperatureTile(categories[i], _fonts.Family, colors[i])
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(i == 0 ? 0 : S(7), 0, i == 1 ? 0 : S(7), 0),
                ContextMenuStrip = _menu
            };
            _tiles[categories[i]] = tile;
            tile.MouseDown += DragWindow;
            cards.Controls.Add(tile, i, 0);
        }
        _root.Controls.Add(cards, 0, 0);
        var toolbar = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        toolbar.MouseDown += DragWindow;
        _toolTip.SetToolTip(_trayButton, "트레이로 내리기");
        _trayButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _trayButton.ContextMenuStrip = _menu;
        _trayButton.Click += (_, _) => MinimizeToTray();
        toolbar.Controls.Add(_trayButton);
        toolbar.Resize += (_, _) => _trayButton.Location = new Point(toolbar.Width - _trayButton.Width, toolbar.Height - _trayButton.Height);
        _root.Controls.Add(toolbar, 0, 1);
        UpdateResponsiveLayout();
    }

    private int S(int pixels) => (int)Math.Round(pixels * DeviceDpi / 96f);

    private void UpdateResponsiveLayout()
    {
        if (_tiles.Count != 2 || WindowState == FormWindowState.Minimized) return;
        var scale = Math.Clamp(Math.Min(ClientSize.Width / (520f * DeviceDpi / 96f),
            ClientSize.Height / (300f * DeviceDpi / 96f)), .7f, 1.4f);
        _root.Padding = new Padding((int)(S(22) * scale));
        _root.RowStyles[1].Height = S(60) * scale;
        _trayButton.Size = new Size((int)(S(42) * scale), (int)(S(42) * scale));
        using var outline = RoundedShape.Create(new RectangleF(0, 0, Width, Height), S(26));
        var previousRegion = Region;
        Region = new Region(outline);
        previousRegion?.Dispose();
        if (_trayButton.Parent is Control toolbar)
            _trayButton.Location = new Point(toolbar.Width - _trayButton.Width, toolbar.Height - _trayButton.Height);
    }

    private async Task MonitorLoopAsync()
    {
        try
        {
            _monitor.Open();
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            do
            {
                _stop.Token.ThrowIfCancellationRequested();
                var snapshot = _monitor.Read();
                await InvokeAsync(() => ApplySnapshot(snapshot), _stop.Token);
            } while (await timer.WaitForNextTickAsync(_stop.Token));
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Program.LogError(ex.ToString());
            if (_verifyDirectory is not null) File.WriteAllText(Path.Combine(_verifyDirectory, "failure.txt"), ex.ToString());
            if (!_stop.IsCancellationRequested)
                await InvokeAsync(() =>
                {
                    foreach (var tile in _tiles.Values) _toolTip.SetToolTip(tile, ex.Message);
                    if (_verifyExit) { Environment.ExitCode = 1; _exitRequested = true; Close(); }
                });
        }
        finally
        {
            if (_verifyDirectory is not null)
                try { File.WriteAllText(Path.Combine(_verifyDirectory, "hardware-report.txt"), _monitor.GetReport()); }
                catch (Exception ex) { Program.LogError(ex.ToString()); }
            try { _monitor.Dispose(); }
            catch (Exception ex) { Program.LogError(ex.ToString()); }
        }
    }

    private void ApplySnapshot(MonitorSnapshot snapshot)
    {
        if (_closing) return;
        _latest = snapshot;
        _cpuTray.Update(SummaryRow(snapshot, "CPU")?.Current);
        _gpuTray.Update(SummaryRow(snapshot, "GPU")?.Current);
        _cpuText.Text = MenuTemperature(snapshot, "CPU", "CPU");
        _gpuText.Text = MenuTemperature(snapshot, "GPU", "GPU");
        foreach (var (category, tile) in _tiles)
            tile.UpdateTemperature(SummaryRow(snapshot, category)?.Current, !_trayMode);
        if (!_trayMode) UpdateWindow(snapshot);
        RecordVerification(snapshot);
    }

    private static string MenuTemperature(MonitorSnapshot snapshot, string category, string label)
    {
        var row = SummaryRow(snapshot, category);
        return $"{label}: {Format(row?.Current)}° · 최저 {Format(row?.Minimum)}° · 최고 {Format(row?.Maximum)}°";
    }

    private void UpdateWindow(MonitorSnapshot snapshot)
    {
        foreach (var (category, tile) in _tiles)
        {
            var row = SummaryRow(snapshot, category);
            tile.Invalidate();
            _toolTip.SetToolTip(tile, row?.Current is not null ? row.Device
                : category == "CPU" && !LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled ? "우클릭 → CPU 드라이버 설치"
                : category == "CPU" && !Program.IsAdministrator ? "관리자 권한으로 다시 실행하세요."
                : "이 장치의 온도 센서에 접근할 수 없습니다.");
        }
    }

    private void RecordVerification(MonitorSnapshot snapshot)
    {
        if (_verifyDirectory is null || _verificationFinished) return;
        _samples.Add(snapshot);
        File.AppendAllText(Path.Combine(_verifyDirectory, "samples.jsonl"), JsonSerializer.Serialize(snapshot) + Environment.NewLine);
        if (_samples.Count == 2)
        {
            MinimizeToTray();
            _trayVerified = !Visible && !ShowInTaskbar && _cpuTray.Visible && _gpuTray.Visible;
            _cpuTray.SavePreview(Path.Combine(_verifyDirectory, "cpu-tray.png"));
            _gpuTray.SavePreview(Path.Combine(_verifyDirectory, "gpu-tray.png"));
        }
        if (_samples.Count == 3)
        {
            RestoreWindow();
            _restoreVerified = Visible && ShowInTaskbar && !_cpuTray.Visible && !_gpuTray.Visible;
            _historyBeforeReset = snapshot.HistoryStart;
            _monitor.RequestReset();
        }
        if (_samples.Count == 4)
            _resetVerified = snapshot.HistoryStart > _historyBeforeReset
                && snapshot.Rows.All(r => !r.Current.HasValue || (r.Current == r.Minimum && r.Current == r.Maximum));
        if (_samples.Count == 5)
        {
            var original = ClientSize;
            ClientSize = new Size(S(380), S(230));
            UpdateResponsiveLayout();
            SaveWindow(Path.Combine(_verifyDirectory, "compact-window.png"));
            _responsiveVerified = _tiles.Values.All(tile => tile.TextFits) && ButtonFits();
            _resizeVerified = ResizeHit(new Point(2, Height / 2)) == 10
                && ResizeHit(new Point(Width - 2, Height / 2)) == 11
                && ResizeHit(new Point(Width / 2, 2)) == 12
                && ResizeHit(new Point(Width / 2, Height - 2)) == 15;
            ClientSize = original;
            UpdateResponsiveLayout();
        }
        if (_samples.Count >= _verifySamples) FinishVerification();
    }

    private void FinishVerification()
    {
        _verificationFinished = true;
        SaveWindow(Path.Combine(_verifyDirectory!, "window.png"));
        using (var bitmap = _applicationIcon.ToBitmap()) bitmap.Save(Path.Combine(_verifyDirectory!, "app-icon.png"), ImageFormat.Png);
        var cpu = SummaryRow(_latest!, "CPU")?.Current;
        var report = new
        {
            IsAdministrator = Program.IsAdministrator, ProcessId = Environment.ProcessId,
            Framework = RuntimeInformation.FrameworkDescription,
            RuntimeDirectory = RuntimeEnvironment.GetRuntimeDirectory(),
            SampleCount = _samples.Count, TargetIntervalMilliseconds = 2000,
            ActualIntervalsMilliseconds = _samples.Zip(_samples.Skip(1), (a, b) => (b.Timestamp - a.Timestamp).TotalMilliseconds).ToArray(),
            Dpi = DeviceDpi, WindowSize = new { Width, Height },
            AppIconResource = "SoyTemperature.AppIcon",
            TrayMinimizePassed = _trayVerified, TrayRestorePassed = _restoreVerified,
            HistoryResetPassed = _resetVerified, ResponsiveLayoutPassed = _responsiveVerified,
            LabelsFit = _tiles.Values.All(tile => tile.TextFits) && ButtonFits(),
            MinimalUiPassed = _tiles.Count == 2 && _trayButton.Text == "" && _root.Controls.Count == 2,
            BorderlessWindow = FormBorderStyle == FormBorderStyle.None,
            RoundedWindowPassed = Region is not null && !Region.IsVisible(1, 1)
                && !Region.IsVisible(Width - 2, 1) && !Region.IsVisible(1, Height - 2)
                && !Region.IsVisible(Width - 2, Height - 2) && Region.IsVisible(Width / 2, Height / 2),
            IconOnlyTrayButton = _trayButton.AccessibleName == "트레이로 내리기" && _trayButton.Text.Length == 0,
            ResizeEdgesPassed = _resizeVerified,
            StorageMonitoringDisabled = !_latest!.Rows.Any(row => row.Category == "SSD/HDD"),
            TemperatureGraphSamples = _tiles.ToDictionary(pair => pair.Key, pair => pair.Value.GraphSamples),
            FontFamily = _fonts.Family.Name, FontWeight = "Bold (700)", EmbeddedFont = true,
            CpuTemperature = cpu, CpuTemperatureReadable = cpu.HasValue,
            CpuTrayText = _cpuTray.Text, GpuTrayText = _gpuTray.Text,
            PawnIoInstalled = LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled,
            WorkingSetBytes = Process.GetCurrentProcess().WorkingSet64,
            FinalSnapshot = _latest
        };
        File.WriteAllText(Path.Combine(_verifyDirectory!, "verification.json"), JsonSerializer.Serialize(report, Program.JsonOptions));
        if (!_trayVerified || !_restoreVerified || !_resetVerified || !_responsiveVerified || !_resizeVerified || !report.LabelsFit || !report.RoundedWindowPassed
            || (Program.IsAdministrator && LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled && !cpu.HasValue)) Environment.ExitCode = 1;
        if (_verifyExit) { _exitRequested = true; Close(); }
    }

    private bool ButtonFits()
    {
        return _trayButton.Width >= S(28) && _trayButton.Height >= S(28)
            && _trayButton.Right <= _trayButton.Parent!.ClientSize.Width;
    }

    private void SaveWindow(string path)
    {
        using var bitmap = new Bitmap(Width, Height);
        DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
        // DrawToBitmap ignores a window region; preserve the actual window's rounded outline in the capture.
        if (Region is not null)
        {
            using var outside = new Region(new Rectangle(0, 0, Width, Height));
            outside.Exclude(Region);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.SetClip(outside, System.Drawing.Drawing2D.CombineMode.Replace);
            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            using var transparent = new SolidBrush(Color.Transparent);
            graphics.FillRectangle(transparent, 0, 0, Width, Height);
        }
        bitmap.Save(path, ImageFormat.Png);
    }

    private async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (_canClose) return;
        e.Cancel = true;
        if (!_exitRequested && e.CloseReason == CloseReason.UserClosing) { MinimizeToTray(); return; }
        if (_closing) return;
        _closing = true;
        _stop.Cancel();
        if (_worker is not null) await _worker;
        _cpuTray.Dispose();
        _gpuTray.Dispose();
        _menu.Dispose();
        _toolTip.Dispose();
        _applicationIcon.Dispose();
        _stop.Dispose();
        _canClose = true;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _fonts.Dispose();
    }

    private void MinimizeToTray()
    {
        _trayMode = true;
        _cpuTray.Visible = true;
        _gpuTray.Visible = true;
        ShowInTaskbar = false;
        Hide();
    }

    private void RestoreWindow()
    {
        _trayMode = false;
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        if (_latest is not null) UpdateWindow(_latest);
        _cpuTray.Visible = false;
        _gpuTray.Visible = false;
        Activate();
    }

    private static TemperatureRow? SummaryRow(MonitorSnapshot snapshot, string category) => snapshot.Rows
        .Where(r => r.Category == category).GroupBy(r => r.Device)
        .Select(group => group.OrderBy(r => !r.Current.HasValue).ThenBy(r => r.Sensor switch
        {
            "CPU Package" or "GPU Core" or "Temperature" or "Composite" => 0,
            "Core Max" => 1, _ => 2
        }).First()).OrderByDescending(r => r.Current).FirstOrDefault();

    private void ExportCsv()
    {
        if (_latest is null) return;
        using var dialog = new SaveFileDialog { Filter = "CSV 파일 (*.csv)|*.csv", FileName = $"temperature-{DateTime.Now:yyyyMMdd-HHmmss}.csv" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var text = new StringBuilder("분류,장치,센서,현재 °C,최저 °C,최고 °C,상태\r\n");
            foreach (var r in _latest.Rows)
                text.AppendLine(string.Join(",", new[] { r.Category, r.Device, r.Sensor, Format(r.Current), Format(r.Minimum), Format(r.Maximum), r.Status }
                    .Select(value => "\"" + value.Replace("\"", "\"\"") + "\"")));
            File.WriteAllText(dialog.FileName, text.ToString(), new UTF8Encoding(true));
        }
        catch (Exception ex) { Program.LogError(ex.ToString()); MessageBox.Show(this, ex.Message, "CSV 저장 실패"); }
    }

    private async Task InstallDriverAsync()
    {
        try
        {
            await DriverInstaller.InstallAsync(_stop.Token);
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true });
            _exitRequested = true;
            Close();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Program.LogError(ex.ToString()); MessageBox.Show(this, ex.Message, "드라이버 설치 실패"); }
    }

    private void ShowFontLicense()
    {
        using var licenseStream = typeof(Program).Assembly.GetManifestResourceStream("SoyTemperature.UbuntuLicense")!;
        using var copyrightStream = typeof(Program).Assembly.GetManifestResourceStream("SoyTemperature.UbuntuCopyright")!;
        using var licenseReader = new StreamReader(licenseStream);
        using var copyrightReader = new StreamReader(copyrightStream);
        using var dialog = new Form { Text = "Ubuntu font license", Size = new Size(S(620), S(420)), StartPosition = FormStartPosition.CenterParent };
        dialog.Controls.Add(new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical, Text = copyrightReader.ReadToEnd() + Environment.NewLine + licenseReader.ReadToEnd() });
        dialog.ShowDialog(this);
    }

    private static string Format(float? value) => value?.ToString("F1") ?? "—";

    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1, 2, 0);
    }

    private int ResizeHit(Point point)
    {
        var screen = PointToScreen(point);
        return SendMessage(Handle, 0x84, 0, (screen.Y << 16) | (screen.X & 0xffff)).ToInt32();
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg != 0x84 || WindowState != FormWindowState.Normal) return;
        var packed = message.LParam.ToInt64();
        var point = PointToClient(new Point((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff)));
        var edge = S(7);
        var left = point.X < edge;
        var right = point.X >= Width - edge;
        var top = point.Y < edge;
        var bottom = point.Y >= Height - edge;
        message.Result = (IntPtr)(top && left ? 13 : top && right ? 14 : bottom && left ? 16 : bottom && right ? 17
            : left ? 10 : right ? 11 : top ? 12 : bottom ? 15 : 1);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, int wParam, int lParam);
    private static string? ArgumentValue(string[] args, string key)
    {
        var index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
