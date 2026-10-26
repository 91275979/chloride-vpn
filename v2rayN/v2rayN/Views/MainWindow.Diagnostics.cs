using ServiceLib.Services;
using System.Net.Http;
using System.Windows.Controls;
using System.Windows.Media;

namespace v2rayN.Views;

public partial class MainWindow
{
    private CancellationTokenSource? _personalDiagnosticCts;
    private string? _diagnosticNodeId;
    private NodeIpReputation? _personalReputation;
    private DateTimeOffset _lastPersonalTraffic;

    private void InitializePersonalDiagnostics()
    {
        // Native counters are enabled before core initialization in App startup.
        // Active probes remain manual.
        _diagnosticNodeId = _config.IndexId;
        Closed += (_, _) => _personalDiagnosticCts?.Cancel();
    }

    private void UpdatePersonalLiveSpeed(ServerSpeedItem item)
    {
        if (!_personalConnectionState.IsReady || item.IndexId != _config.IndexId) return;
        _lastPersonalTraffic = DateTimeOffset.Now;
        personalLiveSpeed.Text = $"↑ {Utils.HumanFy(item.ProxyUp)}/s   ↓ {Utils.HumanFy(item.ProxyDown)}/s";
        personalLiveSpeed.ToolTip = "当前节点实际收发速率；空闲时为 0，不代表节点的最高速度。";
    }

    private void InvalidatePersonalDiagnosticsIfNeeded()
    {
        if (_diagnosticNodeId == _config.IndexId) return;
        _personalDiagnosticCts?.Cancel();
        _diagnosticNodeId = _config.IndexId;
        _personalReputation = null;
        personalDownloadSpeed.Text = "尚未测速";
        personalDownloadNote.Text = "手动检测，最多约 20 MB";
        personalDownloadNote.ToolTip = null;
        personalIpQuality.Text = "尚未检测";
        personalIpQualityNote.Text = "查看机房、代理标记和风险";
        personalIpQualityNote.ToolTip = null;
        personalQualityDetails.Visibility = Visibility.Collapsed;
        personalLatency.Text = "尚未检测延迟";
        _lastPersonalTraffic = default;
    }

    private bool StartPersonalDiagnostic(bool speed)
    {
        RefreshPersonalConnectionState();
        if (!_personalConnectionState.IsReady || _personalOperationBusy || ViewModel?.BlReloadEnabled != true)
        {
            if (speed) personalDownloadNote.Text = "请先开启连接，等待节点就绪。";
            else personalIpQualityNote.Text = "请先开启连接，等待节点就绪。";
            return false;
        }
        _personalDiagnosticCts = new();
        personalSpeedButton.IsEnabled = speed;
        personalQualityButton.IsEnabled = !speed;
        personalTestButton.IsEnabled = false;
        personalEdgeMode.IsEnabled = false;
        personalConnectButton.IsEnabled = false;
        if (speed) personalSpeedButton.Content = "取消测速";
        else personalQualityButton.Content = "取消检测";
        return true;
    }

    private void FinishPersonalDiagnostic()
    {
        _personalDiagnosticCts?.Dispose();
        _personalDiagnosticCts = null;
        personalSpeedButton.IsEnabled = personalQualityButton.IsEnabled = personalTestButton.IsEnabled = true;
        personalEdgeMode.IsEnabled = true;
        personalSpeedButton.Content = "测速";
        personalQualityButton.Content = "检测 IP";
        RefreshPersonalConnectionState();
    }

    private async void PersonalMeasureSpeed_Click(object sender, RoutedEventArgs e)
    {
        if (_personalDiagnosticCts != null) { _personalDiagnosticCts.Cancel(); return; }
        if (!StartPersonalDiagnostic(true)) return;
        var node = _config.IndexId;
        personalDownloadSpeed.Text = "测速中…";
        personalDownloadNote.Text = "通过当前节点下载，最多约 20 MB";
        try
        {
            using var client = PersonalNodeDiagnostics.CreateClient(AppManager.Instance.GetLocalPort(EInboundProtocol.socks));
            var token = _personalDiagnosticCts!.Token;
            var progress = new Progress<double>(bytes =>
            {
                if (_personalDiagnosticCts != null && node == _config.IndexId) personalDownloadSpeed.Text = $"{bytes / 1_000_000:F2} MB/s";
            });
            var result = await PersonalNodeDiagnostics.MeasureDownload(client, token, progress);
            if (node != _config.IndexId) return;
            personalDownloadSpeed.Text = $"{result.BytesPerSecond / 1_000_000:F2} MB/s";
            personalDownloadNote.Text = $"{result.BytesPerSecond * 8 / 1_000_000:F1} Mbps · {result.CheckedAt:HH:mm} 实测";
            personalDownloadNote.ToolTip = $"来源：{result.Source}；接收 {result.Bytes / 1_000_000:F1} MB，耗时 {result.Seconds:F1} 秒。一次下载样本，不代表所有网站或最高带宽。";
        }
        catch (OperationCanceledException) { if (node == _config.IndexId) { personalDownloadSpeed.Text = "未完成"; personalDownloadNote.Text = "已取消或超时，可重试。"; } }
        catch { if (node == _config.IndexId) { personalDownloadSpeed.Text = "测速未完成"; personalDownloadNote.Text = "测速服务不可用；不等于节点断开。"; } }
        finally { FinishPersonalDiagnostic(); }
    }

    private async void PersonalCheckQuality_Click(object sender, RoutedEventArgs e)
    {
        if (_personalDiagnosticCts != null) { _personalDiagnosticCts.Cancel(); return; }
        if (!StartPersonalDiagnostic(false)) return;
        var node = _config.IndexId;
        _personalReputation = null;
        personalQualityDetails.Visibility = Visibility.Collapsed;
        personalIpQualityNote.ToolTip = null;
        personalIpQuality.Text = "检测中…";
        personalIpQualityNote.Text = "正在查询节点出口 · IPQuery";
        try
        {
            using var client = PersonalNodeDiagnostics.CreateClient(AppManager.Instance.GetLocalPort(EInboundProtocol.socks));
            var report = await PersonalNodeDiagnostics.QueryReputation(client, _personalDiagnosticCts!.Token);
            if (node != _config.IndexId) return;
            _personalReputation = report;
            personalIpQuality.Text = report.ScoreText;
            personalIpQualityNote.Text = $"{report.NetworkType} · IPQuery {report.CheckedAt:HH:mm}\n单一来源参考，非通用纯净分";
            personalIpQualityNote.ToolTip = report.Details;
            personalQualityDetails.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException) { if (node == _config.IndexId) { personalIpQuality.Text = "未完成"; personalIpQualityNote.Text = "已取消或超时，可重试。"; } }
        catch { if (node == _config.IndexId) { personalIpQuality.Text = "暂时无法检测"; personalIpQualityNote.Text = "未取得有效数据，不作纯净判断。"; } }
        finally { FinishPersonalDiagnostic(); }
    }

    private void PersonalQualityDetails_Click(object sender, RoutedEventArgs e)
    {
        if (_personalReputation == null) return;
        var text = new TextBox { Text = _personalReputation.Details, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0),
            Background = Brushes.Transparent, Foreground = new SolidColorBrush(Color.FromRgb(25, 52, 66)), FontSize = 14, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var window = new Window { Title = "节点 IP 质量", Owner = this, Width = 600, Height = 400, MinWidth = 480, MinHeight = 340,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new SolidColorBrush(Color.FromRgb(243, 248, 250)), Content = new Border { Padding = new Thickness(28), Child = text } };
        window.ShowDialog();
    }
}
