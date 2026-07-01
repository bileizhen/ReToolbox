using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using ReToolbox.Services;
using ReToolbox.ViewModels;

namespace ReToolbox
{
    public partial class App : Application
    {
        private static IServiceProvider? _services;
        public static IServiceProvider Services => _services!;
        public static MainWindow? MainWindow { get; private set; }

        public App()
        {
            InitializeComponent();

            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<SoftwareInstallService>();
                    services.AddSingleton<ActivationService>();
                    services.AddSingleton<WindowsUpdateService>();
                    services.AddSingleton<EdgeRemoverService>();
                    services.AddSingleton<DefenderService>();
                    services.AddSingleton<SystemInfoService>();
                    services.AddSingleton<MemoryService>();
                    services.AddSingleton<VirtualMemoryService>();
                    services.AddSingleton<MemoryAutoCleanService>();

                    services.AddTransient<SoftwarePageViewModel>();
                    services.AddTransient<ActivationPageViewModel>();
                    services.AddTransient<WindowsUpdatePageViewModel>();
                    services.AddTransient<EdgeRemoverPageViewModel>();
                    services.AddTransient<DefenderPageViewModel>();
                    services.AddTransient<SystemInfoPageViewModel>();
                    services.AddTransient<MemoryPageViewModel>();
                    services.AddTransient<SettingsPageViewModel>();
                })
                .Build();

            _services = Host.Services;
        }

        public Microsoft.Extensions.Hosting.IHost Host { get; }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            MainWindow.Activate();

            // Kick off the background auto-clean schedule if it was enabled in a
            // previous session. It then runs for the whole app session.
            _services!.GetService<MemoryAutoCleanService>()?.StartIfNeeded();
        }
    }
}
