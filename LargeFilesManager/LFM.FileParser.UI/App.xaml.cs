using LFM.Core;
using LFM.Core.Interfaces;
using LFM.FileSorter.BL.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Windows;
using LFM.Core.Helpers;

namespace LFM.FileSorter.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        //The IHost pattern is used to provide dependency injection, configuration, and logging—bringing modern .NET hosting features to desktop apps.
        private static IHost? _appHost { get; set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            _appHost = CreateAppHost();

            if (_appHost != null)
            {
                AppStartupHelper.ConfigureLogging();
                Log.Information("File Sorter application startup initiated. AppHost created. Logger configured.");
                // Ensure DI is available
                ApplicationHost.Services = _appHost.Services;
                // Register handlers after DI is ready
                AppStartupHelper.RegisterGlobalExceptionHandlers(this); 

                await _appHost.StartAsync();
                Log.Information("AppHost started.");
            }
            //Ensure that the base class (Application) performs its standard initialization logic.
            base.OnStartup(e);
        }

        //For WPF OnExit(ExitEventArgs): overriding with async void is not recommended, as the framework does not await the method — so, cleanup code may not finish before the process terminates.
        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("File Sorter application exit initiated.");

            if (_appHost != null)
            {
                //Synchronously waits for async cleanup. Ensures that all cleanup is completed before the application exits.
                _appHost.StopAsync().GetAwaiter().GetResult();
                _appHost.Dispose();
            }
            else
            {
                Log.Warning("AppHost was null during application exit.");
            }

            Log.Information("File Sorter application shutdown completed.");
            Log.CloseAndFlush();
            //Ensures that standard shutdown logic was executed.
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
