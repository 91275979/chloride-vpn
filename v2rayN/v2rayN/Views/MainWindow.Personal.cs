using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System.Windows.Controls;
using ServiceLib.Helper;

namespace v2rayN.Views;

public partial class MainWindow
{
    private bool _loadingPersonalControls = true;
    private bool _syncingPersonalNavigation;
    private bool _personalOperationBusy;
    private PersonalConnectionPresentation _personalConnectionState = PersonalConnectionPresentation.From(false, false, false, false, false);
    private DispatcherTimer? _personalStatusTimer;
    private readonly ObservableCollection<PersonalAppEntry> _personalApps = [];

    public record PersonalAppEntry(string Title, string ProcessName, string Initials);

    private void StartPersonalStatusUpdates()
    {
        _personalStatusTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(3) };
        _personalStatusTimer.Tick += (_, _) => RefreshPersonalConnectionState();
        _personalStatusTimer.Start();
        Closed += (_, _) => _personalStatusTimer.Stop();
    }

    private void PersonalNavigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (personalPages == null || _syncingPersonalNavigation || e.Source != personalNavigation) return;
        if (personalNavigation.SelectedItem is ListBoxItem item && int.TryParse(item.Tag?.ToString(), out var page))
            personalPages.SelectedIndex = page;
    }

    private void PersonalPages_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != personalPages || personalNavigation == null || personalSectionTitle == null) return;
        _syncingPersonalNavigation = true;
        foreach (ListBoxItem item in personalNavigation.Items)
            if (item.Tag?.ToString() == personalPages.SelectedIndex.ToString()) personalNavigation.SelectedItem = item;
        _syncingPersonalNavigation = false;
        (personalSectionTitle.Text, personalSectionDescription.Text) = personalPages.SelectedIndex switch
        {
            2 => ("应用分流", "需要走节点的应用，在这里管理。"),
            3 => ("设置", "启动方式、节点与高级网络选项。"),
            1 => ("高级工具", "原生面板保留完整的节点、日志与网络配置。"),
            _ => ("概览", "连接状态与常用分流，一眼看清。"),
        };
    }

    private void PersonalOpenApps_Click(object sender, RoutedEventArgs e) => personalPages.SelectedIndex = 2;

    private void LoadPersonalAppRouting()
    {
        _loadingPersonalControls = true;
        try
        {
            var policy = _config.PersonalAppRouting ??= new();
            personalAppSplit.IsChecked = policy.Enabled;
            personalEdgeMode.SelectedIndex = policy.EdgeMode switch { "direct" => 0, "proxy" => 2, _ => 1 };
            personalProxyApps.Text = string.Join(Environment.NewLine, policy.ProxyApps ?? []);
            personalAutoRun.IsChecked = _config.GuiItem.AutoRun;
            personalAutoHide.IsChecked = _config.UiItem.AutoHideStartup;
            _personalApps.Clear();
            foreach (var process in policy.ProxyApps ?? [])
            {
                var title = Path.GetFileNameWithoutExtension(process);
                _personalApps.Add(new(title, process, title.Length > 1 ? title[..2] : title));
            }
            personalAppList.ItemsSource = _personalApps;
            personalAppsEmpty.Visibility = _personalApps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            personalOverviewApps.Text = _personalApps.Count == 0 ? "尚未添加应用" : string.Join("、", _personalApps.Select(a => a.Title));
            personalAppRoutingStatus.Text = !policy.Enabled ? "应用专属规则已关闭，连接开启后按网站规则处理流量。"
                : !_config.TunModeItem.EnableTun ? "规则已保存，开启连接后生效。"
                : !PersonalAppRoutingPolicy.UsesTunFrontend(_config) ? "应用分流需要保护模式，并关闭系统代理；可在高级工具中检查。"
                : "名单已生效。Edge 的方式在概览中单独切换。";
            UpdatePersonalEdgeDescription();
            RefreshPersonalConnectionState();
        }
        finally { _loadingPersonalControls = false; }
    }

    private void UpdatePersonalEdgeDescription()
    {
        personalEdgeDescription.Text = personalEdgeMode.SelectedIndex switch
        {
            0 => "Edge 使用本地网络直连，不影响其他应用的连接方式。",
            2 => "Edge 的公网连接都走当前节点，局域网保持直连。",
            _ => "Google、YouTube 等已列入规则的网站走节点，其他网站直连。",
        };
        personalEdgeStatus.Text = !_config.PersonalAppRouting.Enabled ? "应用分流关闭中，浏览器专属设置尚未生效"
            : !_config.TunModeItem.EnableTun ? "已保存，开启连接后生效" : "当前方式已保存";
    }

    private void RefreshPersonalConnectionState()
    {
        if (_config == null || personalProxyStatus == null) return;
        InvalidatePersonalDiagnosticsIfNeeded();
        var tunUp = false;
        var listenerUp = false;
        try
        {
            tunUp = NetworkInterface.GetAllNetworkInterfaces().Any(n => n.Name == "singbox_tun" && n.OperationalStatus == OperationalStatus.Up);
            var port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
            listenerUp = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(p => p.Port == port && System.Net.IPAddress.IsLoopback(p.Address));
        }
        catch { }
        var proxyOn = _config.SystemProxyItem.SysProxyType is ESysProxyType.ForcedChange or ESysProxyType.Pac;
        var state = PersonalConnectionPresentation.From(_config.TunModeItem.EnableTun, tunUp, proxyOn, listenerUp, _personalOperationBusy, _config.PersonalConnectionEnabled);
        if (!_personalOperationBusy) _personalConnectionState = state;
        personalProxyStatus.Text = state.Status;
        personalConnectLabel.Text = state.Action;
        personalSidebarStatus.Text = state.IsReady ? "连接已开启" : state.Status;
        personalConnectButton.ToolTip = state.Action;
        personalConnectButton.IsEnabled = !_personalOperationBusy && (_personalDiagnosticCts == null || state.IsOn) && (ViewModel?.BlReloadEnabled ?? true);
        personalPowerSurface.Opacity = state.IsReady ? 1 : 0.65;
        personalConnectionDescription.Text = state.IsReady
            ? (_config.PersonalAppRouting.Enabled ? "按应用分流，名单外应用直接联网。" : "按网站分流规则处理连接。")
            : state.IsOn ? "本机连接尚未就绪，可以重新连接或检查高级工具中的日志。"
            : "开启后，按你选择的浏览器和应用规则联网。";
        if (!state.IsReady)
        {
            personalLiveSpeed.Text = state.IsOn ? "等待连接就绪" : "连接未开启";
            _personalDiagnosticCts?.Cancel();
        }
        else if ((DateTimeOffset.Now - _lastPersonalTraffic).TotalSeconds > 6) personalLiveSpeed.Text = "等待流量统计";
    }

    private async void PersonalConnect_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null || _personalOperationBusy) return;
        RefreshPersonalConnectionState();
        var previousState = _personalConnectionState;
        _personalOperationBusy = true;
        RefreshPersonalConnectionState();
        try
        {
            var wasOn = _config.PersonalConnectionEnabled ?? (_config.TunModeItem.EnableTun || _config.SystemProxyItem.SysProxyType is ESysProxyType.ForcedChange or ESysProxyType.Pac);
            var desired = !wasOn;
            if (!wasOn && previousState.IsOn) desired = false; // leftover transport: finish stopping it
            if (!desired) _personalDiagnosticCts?.Cancel();
            if (desired && await ConfigHandler.GetDefaultServer(_config) == null)
            {
                personalSettingsStatus.Text = "请先导入一个节点，再开启连接。";
                personalPages.SelectedIndex = 3;
                return;
            }
            if (desired && !Utils.IsAdministrator())
            {
                _config.TunModeItem.EnableTun = true;
                _config.PersonalConnectionEnabled = true;
                _config.SystemProxyItem.SysProxyType = ESysProxyType.ForcedClear;
                await ConfigHandler.SaveConfig(_config);
                if (ProcUtils.RebootAsAdmin()) await AppManager.Instance.AppExitAsync(true);
                else
                {
                    _config.TunModeItem.EnableTun = false;
                    _config.PersonalConnectionEnabled = false;
                    await ConfigHandler.SaveConfig(_config);
                    personalLatency.Text = "未获得管理员权限，连接仍未开启。可通过启动器重新打开。";
                }
                return;
            }
            _config.SystemProxyItem.SysProxyType = ESysProxyType.ForcedClear;
            ViewModel.StatusBarViewModel.SystemProxySelected = (int)ESysProxyType.ForcedClear;
            await ViewModel.StatusBarViewModel.ChangeSystemProxyAsync(ESysProxyType.ForcedClear, true);
            _config.TunModeItem.EnableLegacyProtect = true;
            _config.PersonalConnectionEnabled = desired;
            _config.TunModeItem.EnableTun = desired;
            ViewModel.StatusBarViewModel.EnableTun = desired;
            await ConfigHandler.SaveConfig(_config);
            if (desired) await ViewModel.Reload();
            else await ViewModel.DisconnectPersonalConnection();
            personalLatency.Text = desired ? "连接已启动，可检测节点可用性。" : "氯离子已停止接管应用连接。";
            LoadPersonalAppRouting();
        }
        catch { personalLatency.Text = "连接操作未完成，请在高级工具中查看日志。"; }
        finally { _personalOperationBusy = false; RefreshPersonalConnectionState(); }
    }

    private async Task<bool> SavePersonalPolicy(PersonalAppRoutingItem policy)
    {
        if (ViewModel == null || _personalOperationBusy) return false;
        _personalOperationBusy = true;
        personalEdgeMode.IsEnabled = false;
        var previous = _config.PersonalAppRouting;
        try
        {
            policy.ProxyApps = PersonalAppRoutingPolicy.NormalizeApps(policy.ProxyApps ?? []);
            _config.PersonalAppRouting = policy;
            if (await ConfigHandler.SaveConfig(_config) != 0) throw new IOException();
            if (_config.TunModeItem.EnableTun) await ViewModel.Reload();
            LoadPersonalAppRouting();
            return true;
        }
        catch (Exception ex)
        {
            _config.PersonalAppRouting = previous;
            await ConfigHandler.SaveConfig(_config);
            LoadPersonalAppRouting();
            var message = ex is ArgumentException ? ex.Message : "更改未完成，已保留原来的分流规则。";
            personalAppRoutingStatus.Text = personalEdgeStatus.Text = message;
            return false;
        }
        finally { _personalOperationBusy = false; personalEdgeMode.IsEnabled = true; RefreshPersonalConnectionState(); }
    }

    private async void PersonalEdgeMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingPersonalControls || _personalOperationBusy || _config == null || e.Source != personalEdgeMode) return;
        var policy = JsonUtils.DeepCopy(_config.PersonalAppRouting);
        policy.EdgeMode = personalEdgeMode.SelectedIndex switch { 0 => "direct", 2 => "proxy", _ => "rules" };
        personalEdgeStatus.Text = "正在应用…";
        await SavePersonalPolicy(policy);
    }

    private async void PersonalAppSplit_Click(object sender, RoutedEventArgs e)
    {
        if (_loadingPersonalControls) return;
        var policy = JsonUtils.DeepCopy(_config.PersonalAppRouting);
        policy.Enabled = personalAppSplit.IsChecked == true;
        await SavePersonalPolicy(policy);
    }

    private async void PersonalAddApp_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "选择需要走节点的应用", Filter = "应用程序 (*.exe)|*.exe", Multiselect = true };
        if (dialog.ShowDialog(this) != true) return;
        var policy = JsonUtils.DeepCopy(_config.PersonalAppRouting);
        policy.ProxyApps = [.. policy.ProxyApps ?? [], .. dialog.FileNames.Select(Path.GetFileName)];
        await SavePersonalPolicy(policy);
    }

    private async void PersonalRemoveApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string process }) return;
        var policy = JsonUtils.DeepCopy(_config.PersonalAppRouting);
        policy.ProxyApps = policy.ProxyApps.Where(p => !string.Equals(p, process, StringComparison.OrdinalIgnoreCase)).ToList();
        await SavePersonalPolicy(policy);
    }

    private async void PersonalSaveAppRouting_Click(object sender, RoutedEventArgs e)
    {
        var policy = JsonUtils.DeepCopy(_config.PersonalAppRouting);
        policy.ProxyApps = personalProxyApps.Text.Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries).ToList();
        await SavePersonalPolicy(policy);
    }

    private async void PersonalAutoRun_Click(object sender, RoutedEventArgs e)
    {
        var previous = _config.GuiItem.AutoRun;
        try
        {
            _config.GuiItem.AutoRun = personalAutoRun.IsChecked == true;
            if (await ConfigHandler.SaveConfig(_config) != 0) throw new IOException();
            await AutoStartupHandler.UpdateTask(_config);
            personalSettingsStatus.Text = _config.GuiItem.AutoRun ? "已保存开机自启动设置。" : "已关闭开机自启动。";
        }
        catch
        {
            _config.GuiItem.AutoRun = previous;
            personalAutoRun.IsChecked = previous;
            personalSettingsStatus.Text = "自启动设置未完成，请在高级工具中检查日志。";
        }
    }

    private async void PersonalAutoHide_Click(object sender, RoutedEventArgs e)
    {
        _config.UiItem.AutoHideStartup = personalAutoHide.IsChecked == true;
        await ConfigHandler.SaveConfig(_config);
        personalSettingsStatus.Text = "窗口偏好已保存，下次启动生效。";
    }

    private async void PersonalTest_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        if (!_config.TunModeItem.EnableTun && _config.SystemProxyItem.SysProxyType is not (ESysProxyType.ForcedChange or ESysProxyType.Pac))
        {
            personalLatency.Text = "先开启连接，再检测节点。";
            return;
        }
        personalTestButton.IsEnabled = false;
        personalLatency.Text = "正在通过节点检测连接…";
        try
        {
            var result = await ViewModel.StatusBarViewModel.TestServerAvailability();
            personalLatency.Text = result is { Time: > 0 } ? $"节点可用，检测延迟 {result.Time} ms。" : "检测未通过，请重新连接或查看日志。";
        }
        catch { personalLatency.Text = "本次检测未完成，请稍后重试。"; }
        finally { personalTestButton.IsEnabled = true; RefreshPersonalConnectionState(); }
    }
}
