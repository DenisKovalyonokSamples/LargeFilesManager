using LFM.Core;
using LFM.Core.Interfaces;
using LFM.FileParser.BL.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Windows;
using LFM.Core.Helpers;

namespace LFM.FileParser.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        //The IHost pattern is used to provide dependency injection, configuration, and logging—bringing modern .NET hosting features to desktop apps.
        public static IHost? _appHost { get; private set; }

        protected override async void OnStartup(System.Windows.StartupEventArgs e)
        {
            _appHost = CreateAppHost();
            AppStartupHelper.RegisterGlobalExceptionHandlers(this);

            if (_appHost != null)
            {
                await _appHost.StartAsync();
            }
            AppStartupHelper.ConfigureLogging();
            base.OnStartup(e);
        }

        protected override async void OnExit(System.Windows.ExitEventArgs e)
        {
            if (_appHost != null)
            {
                await _appHost.StopAsync();
                _appHost.Dispose();
            }

            Log.CloseAndFlush();
            base.OnExit(e);
        }

        private IHost CreateAppHost()
        {
            var builder = AppStartupHelper.CreateAppBuilder();

            // Register application services
            builder.Services.AddScoped<ITextFileSorterService, TextFileSorterService>();

            // Register MainWindow
            builder.Services.AddSingleton<MainWindow>();
            // Build the host and resolve services
            var appHost = builder.Build();

            // Make application services available externally
            ApplicationHost.Services = appHost.Services;

            return appHost;
        }
    }
}
