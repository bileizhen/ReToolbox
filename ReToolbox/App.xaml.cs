using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using ReToolbox.Services;
using ReToolbox.ViewModels;

namespace ReToolbox
{
    public partial class App : Application
    {
        private static IServiceProvider? _services;
        private readonly DiagnosticLogService _diagnosticLog;
        public static IServiceProvider Services => _services!;
        public static MainWindow? MainWindow { get; private set; }

        public App()
        {
            InitializeComponent();

            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<DiagnosticLogService>();
                    services.AddSingleton<GitHubReleaseDownloadService>();
                    services.AddSingleton<SoftwareInstallService>();
                    services.AddSingleton<ActivationService>();
                    services.AddSingleton<WindowsUpdateService>();
                    services.AddSingleton<EdgeRemoverService>();
                    services.AddSingleton<DefenderService>();
                    services.AddSingleton<SystemInfoService>();
                    services.AddSingleton<MemoryService>();
                    services.AddSingleton<DiskCleanupService>();
                    services.AddSingleton<VirtualMemoryService>();
                    services.AddSingleton<MemoryAutoCleanService>();
                    services.AddSingleton<PowerPlanService>();
                    services.AddSingleton<BatteryService>();
                    services.AddSingleton<AppUpdateService>();
                    services.AddSingleton<AppUpdateCoordinator>();

                    services.AddTransient<SoftwarePageViewModel>();
                    services.AddTransient<ActivationPageViewModel>();
                    services.AddTransient<WindowsUpdatePageViewModel>();
                    services.AddTransient<EdgeRemoverPageViewModel>();
                    services.AddTransient<DefenderPageViewModel>();
                    services.AddTransient<SystemInfoPageViewModel>();
                    services.AddTransient<MemoryPageViewModel>();
                    services.AddTransient<DiskCleanupPageViewModel>();
                    services.AddTransient<PowerPlanPageViewModel>();
                    services.AddTransient<SettingsPageViewModel>();
                })
                .Build();

            _services = Host.Services;
            _diagnosticLog = Host.Services.GetRequiredService<DiagnosticLogService>();
            RegisterExceptionLogging();
            string version = Assembly.GetExecutingAssembly()
                .GetName()
                .Version?
                .ToString(3) ?? "unknown";
            _diagnosticLog.WriteInformation(
                DiagnosticLogSource.Application,
                $"ReToolbox v{version} 初始化完成");
        }

        public Microsoft.Extensions.Hosting.IHost Host { get; }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _diagnosticLog.WriteInformation(
                DiagnosticLogSource.Application,
                "正在创建主窗口");
            MainWindow = new MainWindow();
            MainWindow.Activate();
            _diagnosticLog.WriteInformation(
                DiagnosticLogSource.Application,
                "主窗口已激活");

            // Kick off the background auto-clean schedule if it was enabled in a
            // previous session. It then runs for the whole app session.
            _services!.GetService<MemoryAutoCleanService>()?.StartIfNeeded();
        }

        private void RegisterExceptionLogging()
        {
            UnhandledException += (_, args) =>
                _diagnosticLog.WriteError(
                    DiagnosticLogSource.Application,
                    "发生未处理的 WinUI 异常",
                    args.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                Exception exception = args.ExceptionObject as Exception ??
                    new InvalidOperationException(
                        args.ExceptionObject?.ToString() ?? "未知异常");
                _diagnosticLog.WriteError(
                    DiagnosticLogSource.Application,
                    "发生未处理的进程异常",
                    exception);
            };
            TaskScheduler.UnobservedTaskException += (_, args) =>
                _diagnosticLog.WriteError(
                    DiagnosticLogSource.Application,
                    "发生未观察的异步任务异常",
                    args.Exception);
        }
    }
}
