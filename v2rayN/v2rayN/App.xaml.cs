using v2rayN.Manager;
using v2rayN.Views;
using ServiceLib.Helper;
using System.Security.Principal;

namespace v2rayN;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    public static EventWaitHandle ProgramStarted;
    private PersonalInstanceCoordinator? _personalInstance;
    public static bool ShowOnStartup { get; private set; } = true;

    public App()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    /// <summary>
    /// Open only one process
    /// </summary>
    /// <param name="e"></param>
    protected override void OnStartup(StartupEventArgs e)
    {
        // The manifest requests normal UAC elevation; never load a second,
        // unelevated configuration that could clear the user's TUN preference.
        if (!Utils.IsAdministrator())
        {
            UI.Show("氯离子需要管理员权限来统一控制连接。未启动第二个客户端，也未更改连接设置。");
            Environment.Exit(1);
            return;
        }
        ShowOnStartup = !e.Args.Contains("--background");
        try
        {
            var sid = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException();
            // A restart may wait for the old owner to exit, but never bypasses
            // ownership. Path casing and launch method share one identity.
            _personalInstance = PersonalInstanceCoordinator.Acquire(Utils.GetExePath(), sid,
                e.Args.Contains(Global.RebootAs) ? TimeSpan.FromSeconds(10) : TimeSpan.Zero);
            if (!_personalInstance.IsOwner)
            {
                _personalInstance.Dispose();
                Environment.Exit(0);
                return;
            }
            ProgramStarted = _personalInstance.ShowRequested!;
        }
        catch
        {
            UI.Show("无法取得氯离子的单实例控制权。为避免重复连接，本次启动已停止。");
            Environment.Exit(1);
            return;
        }

        if (!AppManager.Instance.InitApp())
        {
            UI.Show($"Loading GUI configuration file is abnormal,please restart the application{Environment.NewLine}加载GUI配置文件异常,请重启应用");
            Environment.Exit(0);
            return;
        }

        var config = AppManager.Instance.Config;
        config.PersonalConnectionEnabled ??= config.TunModeItem.EnableTun || config.SystemProxyItem.SysProxyType is ESysProxyType.ForcedChange or ESysProxyType.Pac;
        if (config.PersonalConnectionEnabled == false)
        {
            config.TunModeItem.EnableTun = false;
            // No network takeover on first launch or while disconnected.
            config.SystemProxyItem.SysProxyType = ESysProxyType.Unchanged;
        }

        // The personal overview needs local proxy counters. Enable them before
        // creating the view model/core so startup cannot race the window setup.
        AppManager.Instance.Config.GuiItem.DisplayRealTimeSpeed = true;
        AppManager.Instance.WindowDialog = new WindowDialog();

        AppManager.Instance.InitComponents();

        RxAppBuilder.CreateReactiveUIBuilder()
            .WithWpf()
            .BuildApp();

        base.OnStartup(e);

        var mainWindowViewModel = new MainWindowViewModel();
        var viewFor = SimpleViewLocator.Instance.ResolveView(mainWindowViewModel);
        viewFor!.ViewModel = mainWindowViewModel;

        var mainWindow = (MainWindow)viewFor;
        mainWindow.Show();
        MainWindow = mainWindow;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logging.SaveLog("App_DispatcherUnhandledException", e.Exception);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject != null)
        {
            Logging.SaveLog("CurrentDomain_UnhandledException", (Exception)e.ExceptionObject);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logging.SaveLog("TaskScheduler_UnobservedTaskException", e.Exception);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logging.SaveLog("OnExit");
        _personalInstance?.Dispose();
        base.OnExit(e);
        Process.GetCurrentProcess().Kill();
    }
}
