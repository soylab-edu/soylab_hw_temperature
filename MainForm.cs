using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace SoyTemperature;

internal sealed class MainForm : Form
{
    private static readonly Color BackgroundColor = Theme.Background;
    private static readonly Color PanelColor = Theme.Surface;
    private static readonly Color TextColor = Color.White;
    private static readonly Color MutedColor = Theme.Muted;
    private static readonly Color AccentColor = Theme.Lavender;
    private readonly CancellationTokenSource _stop = new();
    private readonly TemperatureMonitor _monitor = new();
    private readonly DataGridView _grid = new();
    private readonly Label _status = new();
    private readonly Label _hint = new();
    private readonly Dictionary<string, (Label Value, Label Device, Label Range, Label Detail)> _cards = new();
    private readonly CheckBox _details = new();
    private readonly ContextMenuStrip _moreMenu = new();
    private readonly ToolTip _toolTip = new();
    private TableLayoutPanel _root = null!;
    private readonly Dictionary<Control, (float Size, FontStyle Style)> _fontSpecs = new();
    private readonly List<(TelemetryPanel Container, TableLayoutPanel Content)> _cardPanels = new();
    private readonly Dictionary<string, SignalTrace> _traces = new();
    private FlowLayoutPanel _toolbar = null!;
    private float _sizeScale = 1f;
    private bool _updatingLayout;
    private bool _responsiveVerified;
    private readonly string? _verifyDirectory;
    private readonly int _verifySamples;
    private readonly bool _verifyExit;
    private readonly List<MonitorSnapshot> _samples = new();
    private readonly ContextMenuStrip _trayMenu = new();
    private readonly TrayTemperature _cpuTray;
    private readonly TrayTemperature _gpuTray;
    private readonly ToolStripMenuItem _cpuTrayText = new("CPU: 측정 대기") { Enabled = false };
    private readonly ToolStripMenuItem _gpuTrayText = new("GPU: 측정 대기") { Enabled = false };
    private bool _exitRequested;
    private bool _trayMode;
    private bool _trayVerified;
    private bool _restoreVerified;
    private bool _resetVerified;
    private bool _detailsVerified;
    private DateTimeOffset? _historyBeforeReset;
    private Task? _worker;
    private MonitorSnapshot? _latest;
    private bool _closing;
    private bool _canClose;
    private bool _verificationFinished;

    public MainForm(string[] args)
    {
        _verifyDirectory = ArgumentValue(args, "--verify");
        _verifySamples = int.TryParse(ArgumentValue(args, "--verify-samples"), out var count) ? Math.Max(6, count) : 6;
        _verifyExit = args.Contains("--verify-exit");
        if (_verifyDirectory is not null) Directory.CreateDirectory(_verifyDirectory);
        Text = "SOY Temperature · CPU / GPU / SSD·HDD";
        // This hand-built layout scales pixel dimensions explicitly, including table row styles.
        AutoScaleMode = AutoScaleMode.None;
        _ = Handle;
        Font = new Font(SelectFont(), 10);
        BackColor = BackgroundColor;
        ForeColor = TextColor;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(S(900), S(585));
        MinimumSize = new Size(S(640), S(425));
        DoubleBuffered = true;
        BuildLayout();
        _trayMenu.Items.Add("창 열기", null, (_, _) => RestoreWindow());
        _trayMenu.Items.AddRange([_cpuTrayText, _gpuTrayText, new ToolStripSeparator()]);
        _trayMenu.Items.Add("최저·최고 초기화", null, (_, _) => _monitor.RequestReset());
        _trayMenu.Items.Add("종료", null, (_, _) => { _exitRequested = true; Close(); });
        _cpuTray = new TrayTemperature("CPU", Theme.Coral, _trayMenu, RestoreWindow);
        _gpuTray = new TrayTemperature("GPU", Theme.Lavender, _trayMenu, RestoreWindow);
        Icon = SystemIcons.Application;
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
            else if (_root is not null) UpdateResponsiveLayout();
        };
        FormClosing += OnClosing;
    }

    private void BuildLayout()
    {
        var root = _root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(S(28)), ColumnCount = 1, RowCount = 6 };
        foreach (var height in new[] { 64, 38, 280, 68 }) root.RowStyles.Add(new(SizeType.Absolute, S(height)));
        root.RowStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.Absolute, S(40)));
        Controls.Add(root);

        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        heading.RowStyles.Add(new(SizeType.Percent, 85));
        heading.RowStyles.Add(new(SizeType.Percent, 15));
        heading.ColumnStyles.Add(new(SizeType.Percent, 70));
        heading.ColumnStyles.Add(new(SizeType.Percent, 30));
        heading.Controls.Add(MakeLabel("SOY / TEMPERATURE", 22, TextColor, FontStyle.Regular), 0, 0);
        heading.Controls.Add(new Label { Text = "●  LIVE / 2 SEC", ForeColor = Theme.Coral, Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight, Font = new Font(Font.FontFamily, 10) }, 1, 0);
        var ruler = new HeaderRuler { Dock = DockStyle.Fill, Margin = new Padding(0, S(3), 0, 0) };
        heading.Controls.Add(ruler, 0, 1);
        heading.SetColumnSpan(ruler, 2);
        root.Controls.Add(heading, 0, 0);

        _hint.Dock = DockStyle.Fill;
        _hint.TextAlign = ContentAlignment.MiddleLeft;
        _hint.ForeColor = MutedColor;
        _hint.Text = Program.IsAdministrator
            ? "하드웨어 온도 / 실시간 모니터링"
            : "CPU 온도를 보려면 ‘더 보기 → 관리자 실행’을 선택하세요.";
        root.Controls.Add(_hint, 0, 1);

        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = Padding.Empty };
        var categories = new[] { "CPU", "GPU", "SSD/HDD" };
        for (var i = 0; i < 3; i++)
        {
            cards.ColumnStyles.Add(new(SizeType.Percent, 100f / 3));
            var color = i == 0 ? Theme.Coral : i == 1 ? Theme.Lavender : Theme.Green;
            var container = new TelemetryPanel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Accent = color,
                Margin = new Padding(i == 0 ? 0 : S(7), S(4), i == 2 ? 0 : S(7), S(4)), Padding = new Padding(S(20), S(16), S(16), S(16)) };
            var card = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, RowCount = 6, ColumnCount = 1, Margin = Padding.Empty };
            foreach (var height in new[] { 28, 39, 80, 29 }) card.RowStyles.Add(new(SizeType.Absolute, S(height)));
            card.RowStyles.Add(new(SizeType.Absolute, S(25)));
            card.RowStyles.Add(new(SizeType.Percent, 100));
            card.Controls.Add(MakeLabel($"0{i + 1} / {categories[i]}", 11, color, FontStyle.Bold), 0, 0);
            var device = MakeLabel("검색 중…", 9.5f, MutedColor);
            var value = MakeLabel("—°", 40, TextColor, FontStyle.Regular);
            var range = MakeLabel("최저 —   최고 —", 10, MutedColor);
            var detail = MakeLabel("섭씨 °C", 9, MutedColor);
            card.Controls.Add(device, 0, 1);
            card.Controls.Add(value, 0, 2);
            card.Controls.Add(range, 0, 3);
            card.Controls.Add(detail, 0, 4);
            var trace = new SignalTrace { Dock = DockStyle.Fill, Accent = color, Margin = new Padding(0, S(8), 0, 0) };
            _toolTip.SetToolTip(trace, "최근 60초 온도 추이 · 실제 측정값");
            card.Controls.Add(trace, 0, 5);
            _traces[categories[i]] = trace;
            container.Controls.Add(card);
            _cardPanels.Add((container, card));
            _cards[categories[i]] = (value, device, range, detail);
            cards.Controls.Add(container, i, 0);
        }
        root.Controls.Add(cards, 0, 2);

        var toolbar = _toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, S(9), 0, 0) };
        var reset = MakeButton("기록 초기화");
        reset.Click += (_, _) => { _monitor.RequestReset(); _status.Text = "기록 초기화 요청됨 · 다음 측정부터 새로 기록합니다."; };
        _details.Text = "센서 상세";
        _details.AutoSize = true;
        _details.Margin = new Padding(S(14), S(9), S(20), 0);
        _details.CheckedChanged += (_, _) =>
        {
            _grid.Visible = _details.Checked;
            var area = Screen.FromControl(this).WorkingArea;
            var ratio = _details.Checked ? 865f / 585 : 585f / 865;
            ClientSize = new Size(ClientSize.Width, Math.Min((int)(ClientSize.Height * ratio), area.Height - S(45)));
            UpdateResponsiveLayout();
            if (_latest is not null && _details.Checked) UpdateGrid(_latest);
        };
        var tray = MakeButton("트레이로 내리기");
        tray.Click += (_, _) => MinimizeToTray();
        var more = MakeButton("더 보기  ···");
        more.Click += (_, _) => _moreMenu.Show(more, new Point(0, more.Height));
        _moreMenu.Items.Add("CSV 저장", null, (_, _) => ExportCsv());
        var admin = _moreMenu.Items.Add("관리자 실행", null, (_, _) => RestartAsAdministrator());
        admin.Enabled = !Program.IsAdministrator;
        var driver = _moreMenu.Items.Add("CPU 드라이버 설치", null, async (_, _) => await InstallDriverAsync());
        driver.Enabled = !LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled;
        _moreMenu.Items.Add(new ToolStripSeparator());
        _moreMenu.Items.Add("종료", null, (_, _) => { _exitRequested = true; Close(); });
        toolbar.Controls.AddRange([reset, _details, tray, more]);
        root.Controls.Add(toolbar, 0, 3);

        _grid.Dock = DockStyle.Fill;
        _grid.Visible = false;
        _grid.BackgroundColor = PanelColor;
        _grid.BorderStyle = BorderStyle.None;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersHeight = S(40);
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.ColumnHeadersDefaultCellStyle = new() { BackColor = BackgroundColor, ForeColor = MutedColor,
            Font = new Font(Font.FontFamily, 9, FontStyle.Bold), SelectionBackColor = BackgroundColor };
        _grid.DefaultCellStyle = new() { BackColor = PanelColor, ForeColor = TextColor,
            SelectionBackColor = Theme.Button, SelectionForeColor = TextColor, Padding = new Padding(S(5), 0, S(5), 0) };
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Theme.Background;
        _grid.GridColor = Theme.Border;
        _grid.RowTemplate.Height = S(34);
        string[] names = ["분류", "장치", "센서", "현재 °C", "최저 °C", "최고 °C", "상태"];
        int[] widths = [65, 260, 165, 85, 85, 85, 155];
        for (var i = 0; i < names.Length; i++)
        {
            var col = new DataGridViewTextBoxColumn { Name = names[i], HeaderText = names[i], FillWeight = widths[i], SortMode = DataGridViewColumnSortMode.NotSortable };
            if (i is >= 3 and <= 5) col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns.Add(col);
        }
        root.Controls.Add(_grid, 0, 4);

        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.ForeColor = MutedColor;
        _status.Font = new Font(Font.FontFamily, 9);
        _status.Text = "센서를 찾고 있습니다…";
        root.Controls.Add(_status, 0, 5);
    }

    private Label MakeLabel(string text, float size, Color color, FontStyle style = FontStyle.Regular)
    {
        var label = new Label
        {
            Text = text, Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = color,
            Font = new Font(Font.FontFamily, size, style), TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty
        };
        _fontSpecs[label] = (size, style);
        return label;
    }

    private Button MakeButton(string text) => new()
    {
        Text = text, AutoSize = true, Height = S(34), Padding = new Padding(S(12), S(3), S(12), S(3)), Margin = new Padding(0, 0, S(10), 0),
        FlatStyle = FlatStyle.Flat, BackColor = Theme.Background, ForeColor = TextColor, Cursor = Cursors.Hand,
        UseVisualStyleBackColor = false
    };

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
                // Awaiting the UI dispatch prevents queued stale updates during dialogs/shutdown.
                await InvokeAsync(() => ApplySnapshot(snapshot), _stop.Token);
            } while (await timer.WaitForNextTickAsync(_stop.Token));
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Program.LogError(ex.ToString());
            if (!_stop.IsCancellationRequested)
                await InvokeAsync(() =>
                {
                    _status.Text = $"센서 초기화 실패: {ex.Message}";
                    _hint.Text = "관리자 실행 후 다시 확인하세요. 오류 로그는 %LOCALAPPDATA%\\SoyTemperature에 저장됩니다.";
                    if (_verifyDirectory is not null)
                    {
                        File.WriteAllText(Path.Combine(_verifyDirectory, "failure.txt"), ex.ToString());
                        Environment.ExitCode = 1;
                        if (_verifyExit) { _exitRequested = true; Close(); }
                    }
                });
        }
        finally
        {
            if (_verifyDirectory is not null)
            {
                try { File.WriteAllText(Path.Combine(_verifyDirectory, "hardware-report.txt"), _monitor.GetReport()); }
                catch (Exception ex) { Program.LogError(ex.ToString()); }
            }
            try { _monitor.Dispose(); }
            catch (Exception ex) { Program.LogError(ex.ToString()); }
        }
    }

    private void ApplySnapshot(MonitorSnapshot snapshot)
    {
        if (_closing) return;
        _latest = snapshot;
        foreach (var (category, trace) in _traces) trace.Add(SummaryTemperature(snapshot, category));
        var cpu = SummaryTemperature(snapshot, "CPU");
        var gpu = SummaryTemperature(snapshot, "GPU");
        _cpuTray.Update(cpu);
        _gpuTray.Update(gpu);
        _cpuTrayText.Text = $"CPU: {Format(cpu)} °C";
        _gpuTrayText.Text = $"GPU: {Format(gpu)} °C";
        // Sensor sampling continues while hidden. Avoid rebuilding the grid in tray mode.
        if (!_trayMode) UpdateWindow(snapshot);
        RecordVerification(snapshot);
    }

    private void UpdateWindow(MonitorSnapshot snapshot)
    {
        if (_details.Checked) UpdateGrid(snapshot);
        foreach (var (category, labels) in _cards)
        {
            var row = SummaryRow(snapshot, category);
            labels.Value.Text = row?.Current is float current ? $"{current:F1}°" : "—°";
            labels.Device.Text = row?.Device ?? "장치 없음";
            _toolTip.SetToolTip(labels.Device, row?.Device);
            labels.Range.Text = $"최저 {Format(row?.Minimum)}°   최고 {Format(row?.Maximum)}°";
            labels.Detail.Text = row?.Current is not null ? $"{row.Sensor} · °C" : category == "SSD/HDD" ? "온도 정보 미제공" : "관리자 권한 / 드라이버 확인";
            FitLabel(labels.Value);
            FitLabel(labels.Range);
            FitLabel(labels.Detail);
        }
        _status.Text = $"{snapshot.Timestamp:HH:mm:ss} 업데이트  ·  기록 시작 {snapshot.HistoryStart:HH:mm:ss}"
            + (snapshot.Errors.Count > 0 ? $"  ·  읽기 오류 {snapshot.Errors.Count}개" : "");
    }

    private void UpdateGrid(MonitorSnapshot snapshot)
    {
        var selectedId = _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0].Tag as string : null;
        var firstRow = _grid.FirstDisplayedScrollingRowIndex;
        _grid.Rows.Clear();
        foreach (var row in snapshot.Rows)
        {
            var index = _grid.Rows.Add(row.Category, row.Device, row.Sensor, Format(row.Current), Format(row.Minimum), Format(row.Maximum), row.Status);
            _grid.Rows[index].Tag = row.Id;
            _grid.Rows[index].Cells[3].Style.ForeColor = row.Current.HasValue ? AccentColor : MutedColor;
            if (selectedId == row.Id) _grid.Rows[index].Selected = true;
        }
        if (firstRow >= 0 && firstRow < _grid.Rows.Count) _grid.FirstDisplayedScrollingRowIndex = firstRow;
    }

    private void RecordVerification(MonitorSnapshot snapshot)
    {
        if (_verifyDirectory is not null && !_verificationFinished)
        {
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
                _details.Checked = true;
                _detailsVerified = _grid.Visible && _grid.Rows.Count == snapshot.Rows.Count;
                _details.Checked = false;
                var original = ClientSize;
                ClientSize = new Size((int)(640 * DeviceDpi / 96f), (int)(425 * DeviceDpi / 96f));
                UpdateResponsiveLayout();
                _responsiveVerified = _cards.Values.All(card => LabelFits(card.Value) && LabelFits(card.Range))
                    && _toolbar.Controls.Cast<Control>().All(c => c.Bottom <= _toolbar.ClientSize.Height);
                SaveWindow(Path.Combine(_verifyDirectory, "compact-window.png"));
                ClientSize = original;
                UpdateResponsiveLayout();
            }
            if (_samples.Count >= _verifySamples) FinishVerification();
        }
    }

    private void FinishVerification()
    {
        _verificationFinished = true;
        var intervals = _samples.Zip(_samples.Skip(1), (a, b) => (b.Timestamp - a.Timestamp).TotalMilliseconds).ToArray();
        SaveWindow(Path.Combine(_verifyDirectory!, "window.png"));
        var report = new
        {
            Started = _samples[0].Timestamp,
            Finished = _samples[^1].Timestamp,
            IsAdministrator = Program.IsAdministrator,
            ProcessId = Environment.ProcessId,
            Framework = RuntimeInformation.FrameworkDescription,
            RuntimeDirectory = RuntimeEnvironment.GetRuntimeDirectory(),
            SampleCount = _samples.Count,
            TargetIntervalMilliseconds = 2000,
            ActualIntervalsMilliseconds = intervals,
            UiRowCount = _grid.Rows.Count,
            UiVisible = Visible,
            Dpi = DeviceDpi,
            WindowSize = new { Width, Height },
            AutoScale = new { AutoScaleDimensions.Width, AutoScaleDimensions.Height },
            TrayMinimizePassed = _trayVerified,
            TrayRestorePassed = _restoreVerified,
            HistoryResetPassed = _resetVerified,
            SensorDetailsPassed = _detailsVerified,
            ResponsiveLayoutPassed = _responsiveVerified,
            LabelsFit = _cards.Values.All(card => LabelFits(card.Value) && LabelFits(card.Range)),
            FontFamily = Font.FontFamily.Name,
            CpuTrayText = _cpuTray.Text,
            GpuTrayText = _gpuTray.Text,
            PawnIoInstalled = LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled,
            PawnIoVersion = LibreHardwareMonitor.PawnIo.PawnIo.Version.ToString(),
            WorkingSetBytes = Process.GetCurrentProcess().WorkingSet64,
            FinalSnapshot = _latest,
            ValidTemperatureCount = _latest!.Rows.Count(r => r.Current.HasValue),
            Notes = "window.png is a DrawToBitmap capture of the running WinForms window. Missing sensors are reported without simulated values."
        };
        File.WriteAllText(Path.Combine(_verifyDirectory!, "verification.json"), JsonSerializer.Serialize(report, Program.JsonOptions));
        // Hardware report is produced on its owning worker after the UI callback returns.
        if (!_trayVerified || !_restoreVerified || !_resetVerified || !_detailsVerified || !_responsiveVerified) Environment.ExitCode = 1;
        if (_verifyExit) { _exitRequested = true; Close(); }
    }

    private void ExportCsv()
    {
        if (_latest is null) return;
        using var dialog = new SaveFileDialog { Filter = "CSV 파일 (*.csv)|*.csv", FileName = $"temperature-{DateTime.Now:yyyyMMdd-HHmmss}.csv" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var text = new StringBuilder("시간,분류,장치,센서,현재 °C,최저 °C,최고 °C,상태\r\n");
            foreach (var r in _latest.Rows)
                text.AppendLine(string.Join(",", new[] { _latest.Timestamp.ToString("O"), r.Category, r.Device, r.Sensor,
                    Invariant(r.Current), Invariant(r.Minimum), Invariant(r.Maximum), r.Status }.Select(CsvQuote)));
            File.WriteAllText(dialog.FileName, text.ToString(), new UTF8Encoding(true));
            _status.Text = $"CSV 저장 완료: {dialog.FileName}";
        }
        catch (Exception ex) { Program.LogError(ex.ToString()); MessageBox.Show(this, ex.Message, "CSV 저장 실패"); }
    }

    private void RestartAsAdministrator()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" });
            _exitRequested = true;
            Close();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { _status.Text = "관리자 실행이 취소되었습니다. 현재 모드로 계속 측정합니다."; }
        catch (Exception ex) { Program.LogError(ex.ToString()); MessageBox.Show(this, ex.Message, "관리자 실행 실패"); }
    }

    private async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (_canClose) return;
        e.Cancel = true;
        if (!_exitRequested && e.CloseReason == CloseReason.UserClosing)
        {
            MinimizeToTray();
            return;
        }
        if (_closing) return;
        _closing = true;
        _status.Text = "모니터와 센서 연결을 종료하고 있습니다…";
        _stop.Cancel();
        if (_worker is not null) await _worker;
        _cpuTray.Dispose();
        _gpuTray.Dispose();
        _trayMenu.Dispose();
        _moreMenu.Dispose();
        _toolTip.Dispose();
        _stop.Dispose();
        _canClose = true;
        Close();
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

    private static float? SummaryTemperature(MonitorSnapshot snapshot, string category)
        => SummaryRow(snapshot, category)?.Current;

    private static TemperatureRow? SummaryRow(MonitorSnapshot snapshot, string category) => snapshot.Rows
        .Where(r => r.Category == category).GroupBy(r => r.Device)
        .Select(group => group.OrderBy(r => !r.Current.HasValue).ThenBy(r => r.Sensor switch
        {
            "CPU Package" or "GPU Core" or "Temperature" or "Composite" => 0,
            "Core Max" => 1,
            _ => 2
        }).First()).OrderByDescending(r => r.Current).FirstOrDefault();

    private static string SelectFont()
    {
        var installed = FontFamily.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new[] { "SF Pro Display", "SF Pro Text", "SF Pro", "Segoe UI Variable", "Segoe UI" }
            .FirstOrDefault(installed.Contains) ?? "맑은 고딕";
    }

    private static string Format(float? value) => value?.ToString("F1") ?? "—";
    private int S(int value) => (int)Math.Round(value * DeviceDpi / 96f * _sizeScale);

    private void UpdateResponsiveLayout()
    {
        if (_updatingLayout || _root is null || _cardPanels.Count != 3 || WindowState == FormWindowState.Minimized) return;
        _updatingLayout = true;
        SuspendLayout();
        _root.SuspendLayout();
        try
        {
            var dpiScale = DeviceDpi / 96f;
            _sizeScale = Math.Clamp(Math.Min(ClientSize.Width / (900f * dpiScale),
                ClientSize.Height / ((_details.Checked ? 865f : 585f) * dpiScale)), .65f, 1.4f);
            var previous = Font;
            Font = new Font(previous.FontFamily, 10 * _sizeScale);
            previous.Dispose();
            _root.Padding = new Padding(S(28));
            int[] heights = [64, 38, 280, 68];
            for (var i = 0; i < heights.Length; i++) _root.RowStyles[i].Height = S(heights[i]);
            _root.RowStyles[5].Height = S(40);
            for (var i = 0; i < _cardPanels.Count; i++)
            {
                var (container, content) = _cardPanels[i];
                container.Margin = new Padding(i == 0 ? 0 : S(7), S(4), i == 2 ? 0 : S(7), S(4));
                container.Padding = new Padding(S(20), S(16), S(16), S(16));
                int[] cardHeights = [28, 39, 80, 29];
                for (var r = 0; r < cardHeights.Length; r++) content.RowStyles[r].Height = S(cardHeights[r]);
                content.RowStyles[4].Height = S(25);
            }
            _toolbar.Padding = new Padding(0, S(9), 0, 0);
            _details.Margin = new Padding(S(14), S(9), S(20), 0);
            foreach (Control control in _toolbar.Controls)
                if (control is Button button)
                {
                    button.Padding = new Padding(S(12), S(3), S(12), S(3));
                    button.Margin = new Padding(0, 0, S(10), 0);
                }
            foreach (var (control, spec) in _fontSpecs)
            {
                var old = control.Font;
                control.Font = new Font(Font.FontFamily, spec.Size * _sizeScale, spec.Style);
                old.Dispose();
            }
            _grid.RowTemplate.Height = S(34);
            _grid.ColumnHeadersHeight = S(40);
        }
        finally
        {
            _root.ResumeLayout(true);
            ResumeLayout(true);
            _updatingLayout = false;
        }
        foreach (var card in _cards.Values)
        {
            FitLabel(card.Value);
            FitLabel(card.Range);
            FitLabel(card.Detail);
        }
    }

    private void FitLabel(Label label)
    {
        if (label.Width < 1 || label.Height < 1 || !_fontSpecs.TryGetValue(label, out var spec)) return;
        var target = spec.Size * _sizeScale;
        using var graphics = label.CreateGraphics();
        Font? chosen = null;
        for (var size = target; size >= target * .65f; size -= .25f)
        {
            chosen?.Dispose();
            chosen = new Font(Font.FontFamily, size, spec.Style);
            var measured = TextRenderer.MeasureText(graphics, label.Text, chosen, Size.Empty, TextFormatFlags.SingleLine);
            if (measured.Width <= label.Width && measured.Height <= label.Height) break;
        }
        if (chosen is not null)
        {
            var old = label.Font;
            label.Font = chosen;
            old.Dispose();
        }
    }

    private static bool LabelFits(Label label)
    {
        using var graphics = label.CreateGraphics();
        var measured = TextRenderer.MeasureText(graphics, label.Text, label.Font, Size.Empty, TextFormatFlags.SingleLine);
        return measured.Width <= label.Width && measured.Height <= label.Height;
    }

    private void SaveWindow(string path)
    {
        using var bitmap = new Bitmap(Width, Height);
        DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
        bitmap.Save(path, ImageFormat.Png);
    }

    private async Task InstallDriverAsync()
    {
        _status.Text = "공식 PawnIO 드라이버를 다운로드하고 있습니다…";
        try
        {
            await DriverInstaller.InstallAsync(_stop.Token);
            _status.Text = "드라이버 설치 완료 · 관리자 모드로 다시 실행합니다.";
            RestartAsAdministrator();
        }
        catch (OperationCanceledException) { _status.Text = "설치를 취소했습니다."; }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { _status.Text = "드라이버 설치가 취소되었습니다."; }
        catch (Exception ex) { Program.LogError(ex.ToString()); _status.Text = $"드라이버 설치 실패: {ex.Message}"; }
    }
    private static string Invariant(float? value) => value?.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) ?? "";
    private static string CsvQuote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static string? ArgumentValue(string[] args, string key)
    {
        var index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
