using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telemetry;

namespace Telemetry.Harness;

internal static class Program
{
    private const string CopiesMode = "copies";
    private const string EnvelopeMode = "envelope";
    private const string SendMode = "send";
    private const string RateLimitMode = "ratelimit";
    private const int BurstCount = 70;
    private const string AbsentProjectEnvelopeUri = "https://ingest.routersys.com/api/999/envelope/";
    private const string ProbeTypeName = "Telemetry.Harness.Plugin.Probe";
    private const string SessionIdMember = "SessionId";
    private const string ReserveBudgetMember = "ReserveBudget";
    private const string BuildEnvelopeMember = "BuildEnvelope";

    private static int Main(string[] arguments)
    {
        var mode = arguments.Length > 0 ? arguments[0] : CopiesMode;

        switch (mode)
        {
            case CopiesMode:
                VerifyCopies();
                return 0;

            case EnvelopeMode:
                ShowEnvelope();
                return 0;

            case SendMode:
                return SendOnce().GetAwaiter().GetResult();

            case RateLimitMode:
                return TripRateLimit().GetAwaiter().GetResult();

            default:
                Console.WriteLine($"使い方: dotnet run -- [{CopiesMode}|{EnvelopeMode}|{SendMode}|{RateLimitMode}]");
                return 2;
        }
    }

    private static void VerifyCopies()
    {
        var first = Load("Telemetry.Harness.PluginA");
        var second = Load("Telemetry.Harness.PluginB");

        Console.WriteLine("=== 取り込み単位が別アセンブリになっているか ===");
        var firstType = first.GetType("Telemetry.TelemetryReporter")!;
        var secondType = second.GetType("Telemetry.TelemetryReporter")!;
        Report("完全名が一致する", firstType.FullName == secondType.FullName);
        Report("型としては別物である", firstType != secondType);

        Console.WriteLine();
        Console.WriteLine("=== セッションが一致するか ===");
        Report("両者のセッションが一致する", (string)Invoke(first, SessionIdMember)! == (string)Invoke(second, SessionIdMember)!);

        Console.WriteLine();
        Console.WriteLine("=== 送信枠をコピー間で共有するか ===");
        ProcessState.Remove("SentCount");
        ProcessState.Remove("FingerprintCounts");
        var reserved = new List<string>();
        for (var index = 0; index < TelemetryBudget.MaxEventsPerFingerprint; index++)
        {
            reserved.Add((string)Call(first, ReserveBudgetMember, "shared")!);
        }

        reserved.Add((string)Call(second, ReserveBudgetMember, "shared")!);
        Report("同じ指紋の上限をコピー間で共有する", reserved[^1] == "DuplicateLimitReached");

        Console.WriteLine();
        Console.WriteLine("=== 報告がプラグインごとに帰属するか ===");
        ProcessState.Remove("SentCount");
        ProcessState.Remove("FingerprintCounts");
        var firstEnvelope = (string)Call(first, BuildEnvelopeMember)!;
        var secondEnvelope = (string)Call(second, BuildEnvelopeMember)!;
        Report("片方の名前を名乗る", firstEnvelope.Contains("\"plugin\":\"Telemetry.Harness.PluginA\"", StringComparison.Ordinal));
        Report("他方の名前を名乗る", secondEnvelope.Contains("\"plugin\":\"Telemetry.Harness.PluginB\"", StringComparison.Ordinal));
        Report("指紋にプラグイン名が入る", firstEnvelope.Contains("\"Telemetry.Harness.PluginA\"]", StringComparison.Ordinal));

        TelemetrySpool.Default.Clear();
    }

    private static void ShowEnvelope()
    {
        var plugin = Load("Telemetry.Harness.PluginA");
        Console.WriteLine(Call(plugin, BuildEnvelopeMember));
    }

    private static async Task<int> SendOnce()
    {
        var plugin = Load("Telemetry.Harness.PluginA");
        var envelope = Encoding.UTF8.GetBytes((string)Call(plugin, BuildEnvelopeMember)!);

        Console.WriteLine($"送信先: {TelemetryEndpoint.EnvelopeUri}");
        var result = await HttpTelemetryChannel.Default.SendAsync(envelope, CancellationToken.None);
        Console.WriteLine($"結果: {result.Outcome}");

        return result.Outcome == DeliveryOutcome.Delivered ? 0 : 1;
    }

    private static async Task<int> TripRateLimit()
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(TelemetryEndpoint.UserAgent);

        var payload = Encoding.UTF8.GetBytes("{\"event_id\":\"00000000000000000000000000000001\"}\n");
        var counts = new Dictionary<int, int>();

        Console.WriteLine($"送信先: {AbsentProjectEnvelopeUri}");

        for (var index = 0; index < BurstCount; index++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, AbsentProjectEnvelopeUri);
            request.Headers.TryAddWithoutValidation(TelemetryEndpoint.AuthorizationHeaderName, TelemetryEndpoint.AuthorizationHeaderValue);
            request.Content = new ByteArrayContent(payload);

            using var response = await client.SendAsync(request);
            var status = (int)response.StatusCode;
            counts[status] = counts.TryGetValue(status, out var seen) ? seen + 1 : 1;
        }

        foreach (var (status, count) in counts.OrderBy(static entry => entry.Key))
        {
            Console.WriteLine($"{status}: {count}");
        }

        return counts.ContainsKey(429) ? 0 : 1;
    }

    private static Assembly Load(string name)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var path = Directory.EnumerateFiles(root, name + ".dll", SearchOption.AllDirectories)
            .FirstOrDefault(candidate => candidate.Contains(Path.Combine(name, "bin"), StringComparison.OrdinalIgnoreCase));

        return path is null ? throw new FileNotFoundException(name) : Assembly.LoadFrom(path);
    }

    private static object? Invoke(Assembly assembly, string member) =>
        assembly.GetType(ProbeTypeName)!
            .GetProperty(member, BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null);

    private static object? Call(Assembly assembly, string member, params object[] arguments) =>
        assembly.GetType(ProbeTypeName)!
            .GetMethod(member, BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, arguments);

    private static void Report(string description, bool satisfied) =>
        Console.WriteLine($"  {(satisfied ? "OK  " : "NG  ")}{description}");
}
