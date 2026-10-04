using LibreHardwareMonitor.Hardware;

namespace SoyTemperature;

internal sealed record TemperatureRow(string Id, string Category, string Device, string Sensor,
    float? Current, float? Minimum, float? Maximum, string Status);
internal sealed record MonitorSnapshot(DateTimeOffset Timestamp, long Sequence, DateTimeOffset HistoryStart,
    IReadOnlyList<TemperatureRow> Rows, IReadOnlyList<string> Errors);

internal sealed class SensorHistory
{
    private readonly Dictionary<string, (float Min, float Max)> _values = new();
    public (float? Current, float? Min, float? Max) Observe(string id, float? value)
    {
        // A missing/nonfinite sample must not become zero or erase a valid range.
        if (value is float number && float.IsFinite(number))
        {
            if (_values.TryGetValue(id, out var previous))
                _values[id] = (Math.Min(previous.Min, number), Math.Max(previous.Max, number));
            else _values[id] = (number, number);
        }
        else value = null;
        return _values.TryGetValue(id, out var range) ? (value, range.Min, range.Max) : (value, null, null);
    }
    public void Reset() => _values.Clear();
}

internal sealed class TemperatureMonitor : IDisposable
{
    private readonly Computer _computer = new() { IsCpuEnabled = true, IsGpuEnabled = true, IsStorageEnabled = true };
    private readonly SensorHistory _history = new();
    private DateTimeOffset _historyStart = DateTimeOffset.Now;
    private long _sequence;
    private int _resetRequested;
    public void Open() => _computer.Open();
    public void RequestReset() => Interlocked.Exchange(ref _resetRequested, 1);
    public string GetReport() => _computer.GetReport();

    // Called exclusively by the monitor worker; the UI never touches hardware objects.
    public MonitorSnapshot Read()
    {
        if (Interlocked.Exchange(ref _resetRequested, 0) == 1)
        {
            _history.Reset();
            _historyStart = DateTimeOffset.Now;
        }
        var rows = new List<TemperatureRow>();
        var errors = new List<string>();
        foreach (var hardware in _computer.Hardware)
        {
            var category = hardware.HardwareType switch
            {
                HardwareType.Cpu => "CPU",
                HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => "GPU",
                HardwareType.Storage => "SSD/HDD",
                _ => null
            };
            if (category is null) continue;
            var before = rows.Count;
            ReadHardware(hardware, category, hardware.Name, rows, errors);
            if (rows.Count == before)
                rows.Add(new(hardware.Identifier.ToString(), category, hardware.Name, "온도 센서 없음",
                    null, null, null, "미지원 / 권한 확인"));
        }
        foreach (var category in new[] { "CPU", "GPU", "SSD/HDD" })
            if (!rows.Any(r => r.Category == category))
                rows.Add(new($"missing/{category}", category, "장치가 감지되지 않았습니다", "—",
                    null, null, null, "장치 없음 / 권한 확인"));
        return new(DateTimeOffset.Now, ++_sequence, _historyStart, rows, errors);
    }

    private void ReadHardware(IHardware hardware, string category, string device,
        List<TemperatureRow> rows, List<string> errors)
    {
        string? updateError = null;
        try { hardware.Update(); }
        catch (Exception ex) { updateError = ex.Message; errors.Add($"{device}: {ex.Message}"); }
        foreach (var sensor in hardware.Sensors.Where(s => s.SensorType == SensorType.Temperature))
        {
            var id = sensor.Identifier.ToString();
            var values = _history.Observe(id, updateError is null ? sensor.Value : null);
            rows.Add(new(id, category, device, sensor.Name, values.Current, values.Min, values.Max,
                updateError is not null ? "읽기 오류" : values.Current.HasValue ? "정상" : "값 없음 / 권한 확인"));
        }
        foreach (var subHardware in hardware.SubHardware)
            ReadHardware(subHardware, category, device, rows, errors);
    }

    public void Dispose() => _computer.Close();
}
