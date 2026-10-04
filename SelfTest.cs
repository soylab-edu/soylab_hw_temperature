using System.Text.Json;

namespace SoyTemperature;

internal static class SelfTest
{
    public static int Run()
    {
        var results = new List<string>();
        try
        {
            var history = new SensorHistory();
            Check(history.Observe("cpu", 52) == (52f, 52f, 52f), "첫 값으로 최저/최고 초기화", results);
            Check(history.Observe("cpu", 40) == (40f, 40f, 52f), "최저 기록", results);
            Check(history.Observe("cpu", 71) == (71f, 40f, 71f), "최고 기록", results);
            Check(history.Observe("cpu", null) == (null, 40f, 71f), "값 없음: 현재는 null, 기록 유지", results);
            Check(history.Observe("cpu", float.NaN) == (null, 40f, 71f), "NaN 제외", results);
            Check(history.Observe("cpu", float.PositiveInfinity) == (null, 40f, 71f), "무한대 제외", results);
            Check(history.Observe("gpu", 35) == (35f, 35f, 35f), "센서별 기록 분리", results);
            history.Reset();
            Check(history.Observe("cpu", null) == (null, null, null), "초기화 후 기록 제거", results);
            Check(history.Observe("cpu", 49) == (49f, 49f, 49f), "초기화 후 새 측정부터 기록", results);
            // Use working directory because a single-file app's base may be its extraction directory.
            Directory.CreateDirectory("artifacts");
            File.WriteAllText("artifacts/self-test.json", JsonSerializer.Serialize(new { Passed = true, Checks = results }, Program.JsonOptions));
            return 0;
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory("artifacts");
            File.WriteAllText("artifacts/self-test.json", JsonSerializer.Serialize(new { Passed = false, Checks = results, Error = ex.ToString() }, Program.JsonOptions));
            return 1;
        }
    }

    private static void Check(bool result, string name, List<string> results)
    {
        if (!result) throw new InvalidOperationException(name);
        results.Add(name);
    }
}
