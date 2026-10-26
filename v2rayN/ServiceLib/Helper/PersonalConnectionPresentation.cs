namespace ServiceLib.Helper;

public record PersonalConnectionPresentation(string Status, string Action, bool IsOn, bool IsReady)
{
    public static PersonalConnectionPresentation From(bool tunEnabled, bool tunUp, bool proxyEnabled, bool listenerUp, bool busy, bool? enabled = null)
    {
        var on = enabled ?? (tunEnabled || proxyEnabled);
        var ready = listenerUp && (tunEnabled ? tunUp : proxyEnabled);
        if (busy) return new("正在切换", "请稍候", on, false);
        if (enabled == false)
            return tunUp || listenerUp ? new("断开未完成", "完成断开", true, false) : new("未连接", "开启连接", false, false);
        if (ready) return new("已开启", "断开连接", true, true);
        if (on) return new("连接未就绪", "断开连接", true, false);
        return new("未连接", "开启连接", false, false);
    }

    public static string FriendlyNode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "尚未选择节点";
        var name = Regex.Replace(raw, @"^\[[^\]]+\]\s*", "");
        name = Regex.Replace(name, @"\([^()]*\)\s*$", "").Trim();
        if (name.StartsWith("氯离子-")) name = name[4..];
        if (name.EndsWith("-personal-windows", StringComparison.OrdinalIgnoreCase)) name = name[..^17];
        return string.IsNullOrWhiteSpace(name) ? "个人节点" : name;
    }
}
