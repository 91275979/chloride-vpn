namespace ServiceLib.Handler;

// This policy is applied only to the Windows sing-box TUN frontend. The existing
// routing preset remains intact and supplies Edge's website rules.
public static class PersonalAppRoutingPolicy
{
    public static List<string> NormalizeApps(IEnumerable<string> apps)
    {
        var result = new List<string>();
        foreach (var raw in apps)
        {
            var name = raw.Trim();
            if (name.Length == 0) continue;
            if (!Regex.IsMatch(name, @"^[\p{L}\p{N}_. -]+\.exe$", RegexOptions.IgnoreCase))
                throw new ArgumentException("请填写进程文件名，例如 ChatGPT.exe；每行一个，不填路径。");
            if (new[] { "msedge.exe", "xray.exe", "sing-box.exe", "mihomo.exe", "v2rayN.exe" }
                .Contains(name, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException("Edge 请使用专用开关；代理核心程序不能加入代理名单。");
            if (!result.Contains(name, StringComparer.OrdinalIgnoreCase)) result.Add(name);
        }
        return result;
    }

    public static bool UsesTunFrontend(Config config) =>
        config.PersonalAppRouting?.Enabled == true
        && config.TunModeItem.EnableTun
        && config.TunModeItem.EnableLegacyProtect
        && config.SystemProxyItem.SysProxyType == ESysProxyType.ForcedClear;

    public static RoutingItem CreateBackendRouting(RoutingItem original)
    {
        var routing = JsonUtils.DeepCopy(original);
        routing.RuleSet = JsonUtils.Serialize(new List<RulesItem>
        {
            new() { Id = "personal-tun-proxy", Network = "tcp,udp", OutboundTag = Global.ProxyTag },
        });
        routing.RuleNum = 1;
        return routing;
    }

    public static List<Rule4Sbox> CreateFrontendRules(PersonalAppRoutingItem policy)
    {
        if (policy.EdgeMode is not ("direct" or "rules" or "proxy"))
            throw new ArgumentException("无效的 Edge 分流模式。");
        var apps = NormalizeApps(policy.ProxyApps ?? []);
        var rules = new List<Rule4Sbox>
        {
            // Keep routers, local services and private networks reachable in every mode.
            new() { ip_is_private = true, outbound = Global.DirectTag },
        };
        if (policy.EdgeMode != "rules")
            rules.Add(new()
            {
                process_name = ["msedge.exe"],
                outbound = policy.EdgeMode == "direct" ? Global.DirectTag : Global.ProxyTag,
            });
        if (apps.Count > 0)
            rules.Add(new() { process_name = apps, outbound = Global.ProxyTag });
        // Only Edge reaches the existing website rules. Unknown/missing processes,
        // and every app outside the explicit proxy list, go direct.
        rules.Add(new()
        {
            process_name = ["msedge.exe"], invert = true, outbound = Global.DirectTag,
        });
        return rules;
    }
}
