using Lepo.i18n.DependencyInjection;
using LFM.Core;
using LFM.Core.Constants;
using LFM.Core.Interfaces;
using LFM.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Globalization;
using System.Text;
using System.Windows;
using LFM.FileGenerator.BL.Services;
using LFM.Core.Helpers;

namespace LFM.FileGenerator.UI
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
            builder.Services.AddScoped<ITextFileGeneratorService, TextFileGeneratorService>();

            // Register MainWindow
            builder.Services.AddSingleton<MainWindow>();
            var appHost = builder.Build();

            // Make application services available externally
            ApplicationHost.Services = appHost.Services;

            return appHost;
        }
    }
}
