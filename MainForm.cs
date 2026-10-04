using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace SoyTemperature;

internal sealed class MainForm : Form
{
    private static readonly Color BackgroundColor = Color.FromArgb(16, 23, 35);
    private static readonly Color PanelColor = Color.FromArgb(25, 35, 50);
    private static readonly Color TextColor = Color.FromArgb(232, 239, 249);
    private static readonly Color MutedColor = Color.FromArgb(158, 177, 199);
    private static readonly Color AccentColor = Color.FromArgb(83, 217, 192);
    private readonly CancellationTokenSource _stop = new();
    private readonly TemperatureMonitor _monitor = new();
    private readonly DataGridView _grid = new();
    private readonly Label _status = new();
    private readonly Label _hint = new();
    private readonly Label _history = new();
    private readonly Dictionary<string, (Label Value, Label Detail)> _cards = new();
    private readonly string? _verifyDirectory;
    private readonly int _verifySamples;
    private readonly bool _verifyExit;
    private readonly List<MonitorSnapshot> _samples = new();
    private Task? _worker;
    private MonitorSnapshot? _latest;
    private bool _closing;
    private bool _canClose;
    private bool _verificationFinished;

    public MainForm(string[] args)
    {
        _verifyDirectory = ArgumentValue(args, "--verify");
        _verifySamples = int.TryParse(ArgumentValue(args, "--verify-samples"), out var count) ? Math.Max(2, count) : 6;
        _verifyExit = args.Contains("--verify-exit");
        if (_verifyDirectory is not null) Directory.CreateDirectory(_verifyDirectory);
        Text = "SOY Temperature · CPU / GPU / SSD·HDD";
        Font = new Font("맑은 고딕", 10);
        BackColor = BackgroundColor;
        ForeColor = TextColor;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1120, 760);
        MinimumSize = new Size(900, 640);
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;
        BuildLayout();
        Shown += (_, _) => _worker = Task.Run(MonitorLoopAsync);
        FormClosing += OnClosing;
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26), ColumnCount = 1, RowCount = 7 };
        foreach (var height in new[] { 62f, 46f, 135f, 53f }) root.RowStyles.Add(new(SizeType.Absolute, height));
        root.RowStyles.Add(new(SizeType.Percent, 100));
        root.RowStyles.Add(new(SizeType.Absolute, 48));
        root.RowStyles.Add(new(SizeType.Absolute, 30));
        Controls.Add(root);

        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        heading.ColumnStyles.Add(new(SizeType.Percent, 65));
        heading.ColumnStyles.Add(new(SizeType.Percent, 35));
        heading.Controls.Add(MakeLabel("SOY  /  TEMPERATURE", 23, TextColor, FontStyle.Bold), 0, 0);
        heading.Controls.Add(new Label { Text = "LIVE  ·  2초 간격", ForeColor = AccentColor, Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight, Font = new Font(Font.FontFamily, 11, FontStyle.Bold) }, 1, 0);
        root.Controls.Add(heading, 0, 0);

        _hint.Dock = DockStyle.Fill;
        _hint.TextAlign = ContentAlignment.MiddleLeft;
        _hint.ForeColor = Program.IsAdministrator ? AccentColor : Color.FromArgb(241, 196, 109);
        _hint.Text = Program.IsAdministrator
            ? "관리자 모드  ·  CPU / GPU / 저장장치의 온도 센서를 읽습니다."
            : "일반 권한  ·  일부 센서가 보이지 않으면 ‘관리자 실행’을 누르세요.";
        root.Controls.Add(_hint, 0, 1);

        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = Padding.Empty };
        var categories = new[] { "CPU", "GPU", "SSD/HDD" };
        for (var i = 0; i < 3; i++)
        {
            cards.ColumnStyles.Add(new(SizeType.Percent, 100f / 3));
            var card = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = PanelColor,
                Margin = new Padding(i == 0 ? 0 : 8, 5, i == 2 ? 0 : 8, 5), Padding = new Padding(18, 8, 12, 8), RowCount = 3 };
            card.RowStyles.Add(new(SizeType.Absolute, 25));
            card.RowStyles.Add(new(SizeType.Percent, 100));
            card.RowStyles.Add(new(SizeType.Absolute, 25));
            card.Controls.Add(MakeLabel(categories[i], 10, MutedColor, FontStyle.Bold), 0, 0);
            var value = MakeLabel("— °C", 29, AccentColor, FontStyle.Bold);
            var detail = MakeLabel("센서 검색 중…", 9, MutedColor);
            card.Controls.Add(value, 0, 1);
            card.Controls.Add(detail, 0, 2);
            _cards[categories[i]] = (value, detail);
            cards.Controls.Add(card, i, 0);
        }
        root.Controls.Add(cards, 0, 2);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 9, 0, 0) };
        var reset = MakeButton("최저·최고 초기화");
        reset.Click += (_, _) => { _monitor.RequestReset(); _status.Text = "기록 초기화 요청됨 · 다음 측정부터 새로 기록합니다."; };
        var export = MakeButton("CSV 저장");
        export.Click += (_, _) => ExportCsv();
        var admin = MakeButton(Program.IsAdministrator ? "관리자 모드 사용 중" : "관리자 실행");
        admin.Enabled = !Program.IsAdministrator;
        admin.Click += (_, _) => RestartAsAdministrator();
        toolbar.Controls.AddRange([reset, export, admin]);
        root.Controls.Add(toolbar, 0, 3);

        _grid.Dock = DockStyle.Fill;
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
        _grid.ColumnHeadersHeight = 40;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.ColumnHeadersDefaultCellStyle = new() { BackColor = Color.FromArgb(35, 48, 67), ForeColor = TextColor,
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold), SelectionBackColor = Color.FromArgb(35, 48, 67) };
        _grid.DefaultCellStyle = new() { BackColor = PanelColor, ForeColor = TextColor,
            SelectionBackColor = Color.FromArgb(43, 65, 87), SelectionForeColor = TextColor, Padding = new Padding(7, 0, 7, 0) };
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(29, 40, 56);
        _grid.GridColor = Color.FromArgb(43, 55, 73);
        _grid.RowTemplate.Height = 34;
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
        _status.Text = "LibreHardwareMonitor로 센서를 검색하고 있습니다…";
        root.Controls.Add(_status, 0, 5);
        _history.Dock = DockStyle.Fill;
        _history.TextAlign = ContentAlignment.MiddleLeft;
        _history.ForeColor = MutedColor;
        _history.Font = new Font(Font.FontFamily, 9);
        _history.Text = "최저·최고: 실행 후 관측값  ·  상단: 분류별 가장 높은 현재 온도  ·  단위: 섭씨";
        root.Controls.Add(_history, 0, 6);
    }

    private Label MakeLabel(string text, float size, Color color, FontStyle style = FontStyle.Regular) => new()
    {
        Text = text, Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = color,
        Font = new Font(Font.FontFamily, size, style), TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty
    };

    private Button MakeButton(string text) => new()
    {
        Text = text, AutoSize = true, Height = 34, Padding = new Padding(12, 3, 12, 3), Margin = new Padding(0, 0, 10, 0),
        FlatStyle = FlatStyle.Flat, BackColor = PanelColor, ForeColor = TextColor, Cursor = Cursors.Hand,
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
                        if (_verifyExit) Close();
                    }
                });
        }
        finally
        {
            try { _monitor.Dispose(); }
            catch (Exception ex) { Program.LogError(ex.ToString()); }
        }
    }

    private void ApplySnapshot(MonitorSnapshot snapshot)
    {
        if (_closing) return;
        _latest = snapshot;
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
        foreach (var (category, labels) in _cards)
        {
            var live = snapshot.Rows.Where(r => r.Category == category && r.Current.HasValue).ToList();
            labels.Value.Text = live.Count == 0 ? "— °C" : $"{live.Max(r => r.Current):F1} °C";
            labels.Detail.Text = live.Count == 0 ? "측정값 없음 · 권한 / 지원 확인" : $"{live.Count}개 센서 · 현재 온도 중 최고";
        }
        var valid = snapshot.Rows.Count(r => r.Current.HasValue);
        _status.Text = $"●  {snapshot.Timestamp:HH:mm:ss} 갱신  ·  #{snapshot.Sequence}  ·  온도 센서 {valid}개  ·  2초 간격"
            + (snapshot.Errors.Count > 0 ? $"  ·  읽기 오류 {snapshot.Errors.Count}개" : "");
        _history.Text = $"기록 시작 {snapshot.HistoryStart:HH:mm:ss}  ·  최저·최고: 관측값  ·  상단: 분류별 가장 높은 현재 온도";
        if (_verifyDirectory is not null && !_verificationFinished)
        {
            _samples.Add(snapshot);
            File.AppendAllText(Path.Combine(_verifyDirectory, "samples.jsonl"), JsonSerializer.Serialize(snapshot) + Environment.NewLine);
            if (_samples.Count >= _verifySamples) FinishVerification();
        }
    }

    private void FinishVerification()
    {
        _verificationFinished = true;
        var intervals = _samples.Zip(_samples.Skip(1), (a, b) => (b.Timestamp - a.Timestamp).TotalMilliseconds).ToArray();
        using (var bitmap = new Bitmap(Width, Height))
        {
            DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
            bitmap.Save(Path.Combine(_verifyDirectory!, "window.png"), ImageFormat.Png);
        }
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
            FinalSnapshot = _latest,
            ValidTemperatureCount = _latest!.Rows.Count(r => r.Current.HasValue),
            Notes = "window.png is a DrawToBitmap capture of the running WinForms window. Missing sensors are reported without simulated values."
        };
        File.WriteAllText(Path.Combine(_verifyDirectory!, "verification.json"), JsonSerializer.Serialize(report, Program.JsonOptions));
        // Hardware report is produced on its owning worker after the UI callback returns.
        if (_verifyExit) Close();
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
            Close();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { _status.Text = "관리자 실행이 취소되었습니다. 현재 모드로 계속 측정합니다."; }
        catch (Exception ex) { Program.LogError(ex.ToString()); MessageBox.Show(this, ex.Message, "관리자 실행 실패"); }
    }

    private async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (_canClose) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        _status.Text = "모니터와 센서 연결을 종료하고 있습니다…";
        _stop.Cancel();
        if (_worker is not null) await _worker;
        _stop.Dispose();
        _canClose = true;
        Close();
    }

    private static string Format(float? value) => value?.ToString("F1") ?? "—";
    private static string Invariant(float? value) => value?.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) ?? "";
    private static string CsvQuote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static string? ArgumentValue(string[] args, string key)
    {
        var index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
