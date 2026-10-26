using System.Net;
using System.Text.Json;

namespace ServiceLib.Services;

// All diagnostic traffic uses the explicit node SOCKS listener. There is no
// system-proxy or direct fallback, even when the desktop app is excluded by TUN.
public static class PersonalNodeDiagnostics
{
    public const long DownloadLimit = 20_000_000;
    public static readonly Uri DownloadEndpoint = new("https://speed.cloudflare.com/__down?bytes=20000000");
    public static readonly Uri FallbackEndpoint = new("https://cachefly.cachefly.net/10mb.test");
    public static readonly Uri ReputationEndpoint = new("https://api.ipquery.io/");

    public static HttpClient CreateClient(int localSocksPort)
    {
        if (localSocksPort is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(localSocksPort));
        return new(new SocketsHttpHandler
        {
            Proxy = new WebProxy($"socks5://127.0.0.1:{localSocksPort}"), UseProxy = true,
            ConnectTimeout = TimeSpan.FromSeconds(6), AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
        }) { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = 65536 };
    }

    public static async Task<NodeDownloadResult> MeasureDownload(HttpClient client, CancellationToken ct, IProgress<double>? progress = null)
    {
        foreach (var endpoint in new[] { DownloadEndpoint, FallbackEndpoint })
        {
            try { return await MeasureOne(client, endpoint, ct, progress).ConfigureAwait(false); }
            catch (HttpRequestException) when (endpoint == DownloadEndpoint) { }
            catch (InvalidDataException) when (endpoint == DownloadEndpoint) { }
        }
        throw new InvalidDataException("No speed endpoint returned test data.");
    }

    private static async Task<NodeDownloadResult> MeasureOne(HttpClient client, Uri endpoint, CancellationToken ct, IProgress<double>? progress)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        long bytes = 0;
        var timer = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.CacheControl = new() { NoCache = true, NoStore = true };
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var type = response.Content.Headers.ContentType?.MediaType;
            if (type?.Contains("html", StringComparison.OrdinalIgnoreCase) == true || type?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
                throw new InvalidDataException("Endpoint returned a page rather than test data.");
            using var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            var buffer = new byte[65536];
            while (bytes < DownloadLimit)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, DownloadLimit - bytes)), deadline.Token).ConfigureAwait(false);
                if (read == 0) break;
                bytes += read;
                if (timer.Elapsed.TotalMilliseconds > 100) progress?.Report(BytesPerSecond(bytes, timer.Elapsed.TotalSeconds));
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && bytes >= 262144) { }
        ct.ThrowIfCancellationRequested();
        if (bytes < 262144) throw new InvalidDataException("Not enough test data.");
        return new(bytes, timer.Elapsed.TotalSeconds, BytesPerSecond(bytes, timer.Elapsed.TotalSeconds), endpoint.Host, DateTimeOffset.Now);
    }

    public static double BytesPerSecond(long bytes, double seconds) => bytes >= 0 && double.IsFinite(seconds) && seconds > 0 ? bytes / seconds : 0;

    public static async Task<NodeIpReputation> QueryReputation(HttpClient client, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(16));
        var own = (await client.GetStringAsync(ReputationEndpoint, deadline.Token).ConfigureAwait(false)).Trim().Trim('"');
        if (!IPAddress.TryParse(own, out var address) || IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            throw new InvalidDataException("Provider did not identify a public exit address.");
        var uri = new Uri(ReputationEndpoint, Uri.EscapeDataString(address.ToString()) + "?format=json");
        var json = await client.GetStringAsync(uri, deadline.Token).ConfigureAwait(false);
        return ParseReputation(json, address.ToString());
    }

    public static NodeIpReputation ParseReputation(string json, string expectedIp)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        string? Text(JsonElement obj, string key) => obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        JsonElement Child(string key) => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(key, out var child) ? child : default;
        var ip = Text(root, "ip");
        if (!IPAddress.TryParse(ip, out var actual) || !IPAddress.TryParse(expectedIp, out var expected) || !actual.Equals(expected))
            throw new InvalidDataException("Exit address and report do not match.");
        var risk = Child("risk");
        bool? Flag(string key) => risk.ValueKind == JsonValueKind.Object && risk.TryGetProperty(key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
        int? score = risk.ValueKind == JsonValueKind.Object && risk.TryGetProperty("risk_score", out var number) && number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var n) && n is >= 0 and <= 100 ? n : null;
        var isp = Child("isp");
        var location = Child("location");
        return new(actual.ToString(), Text(isp, "asn"), Text(isp, "org"), Text(location, "country"), Text(location, "city"),
            Flag("is_datacenter"), Flag("is_vpn"), Flag("is_proxy"), Flag("is_tor"), score, "IPQuery", DateTimeOffset.Now);
    }
}

public record NodeDownloadResult(long Bytes, double Seconds, double BytesPerSecond, string Source, DateTimeOffset CheckedAt);
public record NodeIpReputation(string Ip, string? Asn, string? Organization, string? Country, string? City,
    bool? Datacenter, bool? Vpn, bool? Proxy, bool? Tor, int? RiskScore, string Source, DateTimeOffset CheckedAt)
{
    public static string FlagText(bool? value) => value switch { true => "已标记", false => "未标记", _ => "未知" };
    public string NetworkType => Datacenter switch { true => "机房网络", false => "未标记为机房", _ => "网络类型未知" };
    public string ScoreText => RiskScore is int score ? $"风险 {score}/100" : "风险评分未知";
    public string Details => $"出口 IP：{Ip}\n位置：{Country ?? "未知"} · {City ?? "未知"}\n网络：{Asn ?? "未知"} · {Organization ?? "未知"}\n类型：{NetworkType}\nVPN：{FlagText(Vpn)}　公开代理：{FlagText(Proxy)}　Tor：{FlagText(Tor)}\n{ScoreText}（数值越高，来源认为风险越高）\n来源：{Source} · {CheckedAt:MM-dd HH:mm}\n单一来源参考；未标记不代表住宅 IP、没有滥用记录或平台一定接受。";
}
