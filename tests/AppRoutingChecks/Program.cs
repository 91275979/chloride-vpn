global using System.Reflection;
global using ServiceLib;
global using ServiceLib.Common;
global using ServiceLib.Enums;
global using ServiceLib.Handler;
global using ServiceLib.Manager;
global using ServiceLib.Models.Configs;
global using ServiceLib.Models.CoreConfigs;
global using ServiceLib.Models.Dto;
global using ServiceLib.Models.Entities;
global using ServiceLib.Services.CoreConfig;
global using ServiceLib.Tests.CoreConfig;

var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
var count = 0;
void Check(bool ok, string message)
{
    if (!ok) throw new Exception(message);
    count++;
}
bool Matches(Rule4Sbox r, string process, bool privateIp = false)
{
    if (r.ip_is_private == true) return privateIp;
    if (r.process_name == null) return false;
    var found = r.process_name.Contains(process, StringComparer.OrdinalIgnoreCase);
    return r.invert == true ? !found : found;
}
foreach (var mode in new[] { "direct", "rules", "proxy" })
{
    var policy = new PersonalAppRoutingItem { Enabled = true, EdgeMode = mode, ProxyApps = ["ChatGPT.exe"] };
    var prefix = PersonalAppRoutingPolicy.CreateFrontendRules(policy);
    string Route(string process, bool local = false) => prefix.FirstOrDefault(r => Matches(r, process, local))?.outbound;
    Check(Route("msedge.exe") == (mode == "rules" ? null : mode), $"Edge mode {mode}");
    Check(Route("ChatGPT.exe") == "proxy", "Selected app must proxy regardless of Edge mode");
    Check(Route("Weixin.exe") == "direct", "Unselected app must stay direct");
    Check(Route("") == "direct", "Unknown process must stay direct");
    Check(Route("msedge.exe", true) == "direct", "LAN must stay direct");

    var cfg = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
    cfg.PersonalAppRouting = policy;
    cfg.TunModeItem.EnableTun = true;
    cfg.TunModeItem.EnableLegacyProtect = true;
    cfg.SystemProxyItem.SysProxyType = ESysProxyType.ForcedClear;
    cfg.TunModeItem.Stack = "system";
    cfg.TunModeItem.Mtu = 1500;
    CoreConfigTestFactory.BindAppManagerConfig(cfg);
    var node = CoreConfigTestFactory.CreateSocksNode(ECoreType.sing_box);
    var context = CoreConfigTestFactory.CreateContext(cfg, node, ECoreType.sing_box, false) with { IsWindows = true };
    context.RoutingItem.RuleSet = JsonUtils.Serialize(new List<RulesItem>
    {
        new() { Id = "web", Domain = ["domain:example.org"], OutboundTag = "proxy" },
        new() { Id = "fallback", Network = "tcp,udp", OutboundTag = "direct" },
    });
    var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();
    Check(result.Success, "Frontend config generation: " + result.Msg);
    var text = result.Data.ToString();
    var generated = JsonUtils.Deserialize<SingboxConfig>(text);
    var rules = generated.route.rules;
    var appIndex = rules.FindIndex(r => r.process_name?.Contains("ChatGPT.exe") == true);
    var websiteIndex = rules.FindIndex(r => r.domain_suffix?.Contains("example.org") == true);
    Check(appIndex >= 0 && websiteIndex > appIndex, "App routing must precede website rules");
    Check(rules.Any(r => r.process_name?.Contains("msedge.exe") == true && r.invert == true && r.outbound == "direct"), "Missing unselected-app guard");
    Check(!rules.Any(r => r.clash_mode != null), "Global/direct UI mode must not override isolated app policy");
    File.WriteAllText(Path.Combine(output, mode + ".json"), text);

    var original = context.RoutingItem.RuleSet;
    var backend = PersonalAppRoutingPolicy.CreateBackendRouting(context.RoutingItem);
    Check(context.RoutingItem.RuleSet == original, "Website preset must not be mutated");
    var backendRules = JsonUtils.Deserialize<List<RulesItem>>(backend.RuleSet);
    Check(backendRules.Count == 1 && backendRules[0].OutboundTag == "proxy", "Backend must preserve the frontend's proxy decision");
    cfg.TunModeItem.EnableTun = false;
    Check(!PersonalAppRoutingPolicy.UsesTunFrontend(cfg), "Policy must not pretend to work without TUN");
    var noTun = new CoreConfigSingboxService(context with { IsTunEnabled = false }).GenerateClientConfigContent();
    var noTunCfg = JsonUtils.Deserialize<SingboxConfig>(noTun.Data.ToString());
    Check(!noTunCfg.route.rules.Any(r => r.process_name?.Contains("ChatGPT.exe") == true), "Non-TUN traffic must retain existing behavior");
    cfg.TunModeItem.EnableTun = true;
    cfg.SystemProxyItem.SysProxyType = ESysProxyType.ForcedChange;
    Check(!PersonalAppRoutingPolicy.UsesTunFrontend(cfg), "System proxy must be cleared before app isolation is activated");
}
Check(PersonalAppRoutingPolicy.NormalizeApps([" ChatGPT.exe ", "chatgpt.exe"]).Count == 1, "Deduplication");
foreach (var invalid in new[] { "xray.exe", "msedge.exe", "C:\\Apps\\a.exe", "*" })
{
    try { PersonalAppRoutingPolicy.NormalizeApps([invalid]); throw new Exception("Accepted invalid app: " + invalid); }
    catch (ArgumentException) { count++; }
}
var tunReady = ServiceLib.Helper.PersonalConnectionPresentation.From(true, true, false, true, false);
Check(tunReady.IsReady && tunReady.Status == "已开启", "A working TUN with system proxy off must show connected");
Check(!ServiceLib.Helper.PersonalConnectionPresentation.From(true, false, false, true, false).IsReady, "Missing TUN adapter must not show connected");
Check(!ServiceLib.Helper.PersonalConnectionPresentation.From(true, true, false, false, false).IsReady, "Missing local core listener must not show connected");
Check(ServiceLib.Helper.PersonalConnectionPresentation.From(false, false, false, true, false).Action == "开启连接", "Background core alone must not show connected");
Check(ServiceLib.Helper.PersonalConnectionPresentation.From(true, false, false, false, false).Action == "断开连接", "Failed configured tunnel must remain disconnectable");
Check(ServiceLib.Helper.PersonalConnectionPresentation.From(false, true, false, true, false, false).Status == "断开未完成", "A leftover tunnel/listener must never be reported off");
Check(ServiceLib.Helper.PersonalConnectionPresentation.From(false, false, false, false, false, false).Status == "未连接", "Off requires no live tunnel or listener");
Check(ServiceLib.Helper.PersonalConnectionPresentation.From(false, false, false, false, false, true).Action == "断开连接", "An enabled but failed connection must remain cancelable");
Check(ServiceLib.Helper.PersonalConnectionPresentation.From(true, true, false, true, true).Status == "正在切换", "Busy operation must show transition");
Check(ServiceLib.Helper.PersonalConnectionPresentation.From(false, false, true, true, false).IsReady, "Legacy system proxy readiness");
Check(ServiceLib.Helper.PersonalConnectionPresentation.FriendlyNode("[VLESS] 氯离子-洛杉矶-personal-windows(192.0.2.1:443)") == "洛杉矶", "Friendly node must remove protocol, client suffix and address");
Check(ServiceLib.Helper.PersonalConnectionPresentation.FriendlyNode(null) == "尚未选择节点", "Missing node placeholder");
var qualityJson = """{"ip":"192.0.2.10","isp":{"asn":"AS64500","org":"Example"},"risk":{"is_datacenter":true,"is_vpn":false,"risk_score":0}}""";
var quality = ServiceLib.Services.PersonalNodeDiagnostics.ParseReputation(qualityJson, "192.0.2.10");
Check(quality.Datacenter == true && quality.RiskScore == 0, "Provider risk and datacenter classification are independent");
Check(quality.Proxy == null && quality.Tor == null, "Missing provider flags must stay unknown");
Check(quality.NetworkType == "机房网络" && !quality.ScoreText.Contains("纯净"), "Do not manufacture a universal purity score");
Check(ServiceLib.Services.NodeIpReputation.FlagText(null) == "未知", "Unknown is not a negative finding");
foreach (var badScore in new[] { "-1", "101", "null", "\"0\"" })
{
    var minimal = ServiceLib.Services.PersonalNodeDiagnostics.ParseReputation("{\"ip\":\"192.0.2.10\",\"risk\":{\"risk_score\":" + badScore + "}}", "192.0.2.10");
    Check(minimal.RiskScore == null, "Invalid provider score must stay unknown");
}
try { ServiceLib.Services.PersonalNodeDiagnostics.ParseReputation(qualityJson, "192.0.2.11"); throw new Exception("Accepted mismatched exit IP"); }
catch (InvalidDataException) { count++; }
Check(ServiceLib.Services.PersonalNodeDiagnostics.BytesPerSecond(1_000_000, 2) == 500_000, "Speed units are actual bytes per second");
Check(ServiceLib.Services.PersonalNodeDiagnostics.BytesPerSecond(10, 0) == 0, "Zero elapsed time must not become infinity");
var requests = 0;
using (var testClient = new System.Net.Http.HttpClient(new DiagnosticHandler(request =>
{
    requests++;
    if (request.RequestUri.Host == "speed.cloudflare.com") return new(System.Net.HttpStatusCode.Forbidden);
    return new(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.ByteArrayContent(new byte[300_000]) };
})))
{
    var speed = await ServiceLib.Services.PersonalNodeDiagnostics.MeasureDownload(testClient, CancellationToken.None);
    Check(requests == 2 && speed.Source == "cachefly.cachefly.net", "Blocked speed service must use an explicit fallback");
    Check(speed.Bytes == 300_000 && speed.BytesPerSecond > 0, "Measured payload only");
}
using (var testClient = new System.Net.Http.HttpClient(new DiagnosticHandler(request => new(System.Net.HttpStatusCode.OK)
    { Content = new System.Net.Http.ByteArrayContent(new byte[21_000_000]) })))
{
    var speed = await ServiceLib.Services.PersonalNodeDiagnostics.MeasureDownload(testClient, CancellationToken.None);
    Check(speed.Bytes == ServiceLib.Services.PersonalNodeDiagnostics.DownloadLimit, "Active speed test must cap payload at 20 MB");
    using var canceled = new CancellationTokenSource(); canceled.Cancel();
    try { await ServiceLib.Services.PersonalNodeDiagnostics.MeasureDownload(testClient, canceled.Token); throw new Exception("Ignored cancellation"); }
    catch (OperationCanceledException) { count++; }
}
using (var testClient = new System.Net.Http.HttpClient(new DiagnosticHandler(request => new(System.Net.HttpStatusCode.OK)
    { Content = new System.Net.Http.StringContent(request.RequestUri.AbsolutePath == "/" ? "192.0.2.10" : qualityJson) })))
{
    var report = await ServiceLib.Services.PersonalNodeDiagnostics.QueryReputation(testClient, CancellationToken.None);
    Check(report.Ip == "192.0.2.10" && report.Source == "IPQuery", "Own-exit lookup and matching reputation report");
}
var stoppedConfig = CoreConfigTestFactory.CreateConfig(ECoreType.Xray);
stoppedConfig.PersonalConnectionEnabled = false;
var coreCallbacks = 0;
await CoreManager.Instance.Init(stoppedConfig, (success, message) => { coreCallbacks++; return Task.CompletedTask; });
await CoreManager.Instance.LoadCore(null, null);
Check(coreCallbacks == 0, "Master off must gate core loading before any node generation/startup callbacks");
stoppedConfig.PersonalConnectionEnabled = true;
await CoreManager.Instance.LoadCore(null, null);
Check(coreCallbacks == 1, "Enabled core ingress still validates a missing node normally");
if (args.Contains("--probe"))
{
    using var nodeClient = ServiceLib.Services.PersonalNodeDiagnostics.CreateClient(10808);
    var live = new Dictionary<string, object>();
    try
    {
        var speed = await ServiceLib.Services.PersonalNodeDiagnostics.MeasureDownload(nodeClient, CancellationToken.None);
        live["speed"] = new { speed.Bytes, speed.Seconds, speed.BytesPerSecond, speed.Source, speed.CheckedAt };
    }
    catch (Exception ex) { live["speedError"] = ex.GetType().Name; }
    try
    {
        var report = await ServiceLib.Services.PersonalNodeDiagnostics.QueryReputation(nodeClient, CancellationToken.None);
        live["quality"] = new { report.Source, report.NetworkType, report.Asn, report.RiskScore, report.Vpn, report.Proxy, report.Tor, report.CheckedAt };
    }
    catch (Exception ex) { live["qualityError"] = ex.GetType().Name; }
    var liveJson = System.Text.Json.JsonSerializer.Serialize(live, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(Path.Combine(output, "live-probe.json"), liveJson);
    Console.WriteLine(liveJson);
}
Check(new PersonalAppRoutingItem().ProxyApps.Count == 0, "Public defaults must not preselect personal apps");
Check(new PersonalAppRoutingItem().Enabled, "Browser/application routing must be ready for explicit connection");
Console.WriteLine($"PASS: {count} routing, presentation and diagnostic checks; generated configurations in {output}");

sealed class DiagnosticHandler(Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage> respond) : System.Net.Http.HttpMessageHandler
{
    protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(respond(request));
    }
}
