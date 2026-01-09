using Lepo.i18n.DependencyInjection;
using LFM.Core.Constants;
using LFM.Core.Localization;
using LFM.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Globalization;
using System.Text;
using System.Windows;

namespace LFM.Core.Helpers
{
    public static class AppStartupHelper
    {
        //The IHost pattern is used to provide dependency injection, configuration, and logging—bringing modern .NET hosting features to desktop apps.
        private static IHost? _appHost;

        #region Public Methods

        public static HostApplicationBuilder CreateAppBuilder()
        {
            // Set default culture to en-US
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            var builder = Host.CreateApplicationBuilder();

            // Add appsettings.json
            builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            // Register AppSettings
            builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
           
            // Replace Serilog with default logger
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(Log.Logger);

            //Register Lepo.i18n service
            builder.Services.AddStringLocalizer(x =>
            {
                // Load translations from embedded resources
                x.FromResource<Resource>(new CultureInfo("en-US"));
            });

            return builder;
        }

        public static void CreateAppHost(HostApplicationBuilder builder, Application app)
        {
            // Build the host and resolve services
            _appHost = builder.Build();
            // Make application services available externally
            ApplicationHost.Services = _appHost.Services;

            if (_appHost != null)
            {
                AppStartupHelper.ConfigureLogging();
                Log.Information("Application startup initiated. AppHost created. Logger configured.");
                // Ensure DI is available
                ApplicationHost.Services = _appHost.Services;
                // Register handlers after DI is ready
                AppStartupHelper.RegisterGlobalExceptionHandlers(app);
                //Synchronously waits for async startup. Ensures that all startup process is completed.
                _appHost.StartAsync().GetAwaiter().GetResult();
                Log.Information("Application AppHost started.");
            }
        }

        public static void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
           .MinimumLevel.Debug()
           .WriteTo.Console()
           .WriteTo.File(ServiceManager.AppSettings.Value.LogFilePath, rollingInterval: RollingInterval.Day, shared: true,
               outputTemplate: ServiceManager.AppSettings.Value.OutputLogToFileTemplate)
           .Enrich.WithThreadId()
           .Enrich.WithProcessId()
           .CreateLogger();
        }

        public static void FinishAppServices()
        {
            Log.Information("Application exit initiated.");

            if (_appHost != null)
            {
                //For WPF OnStartup/OnExit: overriding with async void is not recommended, as the framework does not await the method — some code may not finish before the process terminates (cleanup code etc).
                //Synchronously waits for async cleanup. Ensures that all cleanup is completed before the application exits.
                _appHost.StopAsync().GetAwaiter().GetResult();
                _appHost.Dispose();
            }
            else
            {
                Log.Warning("AppHost was null during application exit.");
            }

            Log.Information("Application shutdown completed.");
            Log.CloseAndFlush();
        }

        public static void RegisterGlobalExceptionHandlers(Application app)
        {
            // UI thread exceptions (WPF Dispatcher)
            app.DispatcherUnhandledException += (sender, args) =>
            {
                try
                {
                    // This code runs when an unhandled exception occurs on the UI thread
                    Log.Error(args.Exception, "DispatcherUnhandledException");
                    ShowExceptionDialog(app, args.Exception, ServiceManager.StringLocalizer[TranslationConstant.UIThreadError]);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "RegisterGlobalExceptionHandlers function, DispatcherUnhandledExceptions error.");
                }
                finally
                {
                    args.Handled = true;
                }
            };

            // Non-UI thread exceptions
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                try
                {
                    string unknownNonUIExceptionMessage = ServiceManager.StringLocalizer[TranslationConstant.UnhandledException];
                    var ex = args.ExceptionObject as Exception ?? new Exception(unknownNonUIExceptionMessage);

                    Log.Error(ex, "AppDomain.CurrentDomain.UnhandledException (IsTerminating={IsTerminating})", args.IsTerminating);

                    string nonUIThreadErrorMessage = ServiceManager.StringLocalizer[TranslationConstant.NonUIThreadError];
                    // Marshals to UI thread so we show the dialog owned by MainWindow
                    app.Dispatcher.Invoke(() => ShowExceptionDialog(app, ex, nonUIThreadErrorMessage));

                    string errorMessage = ServiceManager.StringLocalizer[TranslationConstant.UnhandledException];

                    Log.Fatal(ex, errorMessage);
                    Log.CloseAndFlush();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Non-UI thread exception.");
                }
            };

            // Task scheduler unobserved task exceptions
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                try
                {
                    var agg = args.Exception;
                    Log.Error(agg, "TaskScheduler.UnobservedTaskException");
                    app.Dispatcher.Invoke(() => ShowExceptionDialog(app, agg.Flatten(), ServiceManager.StringLocalizer[TranslationConstant.TaskError]));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Task scheduler unobserved task exception.");
                }
                finally
                {
                    args.SetObserved();
                }
            };
        }

        #endregion

        #region Private Methods

        private static void ShowExceptionDialog(Application app, Exception exeption, string caption = "Application error")
        {
            try
            {
                // Compose a concise message, include full details in "Details" tooltip for debugging
                var messageBuilder = new StringBuilder();
                messageBuilder.AppendLine(ServiceManager.StringLocalizer[TranslationConstant.UnexpectedErrorOccurred]);
                if (!string.IsNullOrWhiteSpace(exeption.Message))
                {
                    messageBuilder.AppendLine();
                    messageBuilder.AppendLine(ServiceManager.StringLocalizer[TranslationConstant.Message] + ":");
                    messageBuilder.AppendLine(exeption.Message);
                }

                messageBuilder.AppendLine();
                messageBuilder.AppendLine(ServiceManager.StringLocalizer[TranslationConstant.Details] + ":");
                messageBuilder.AppendLine(exeption.ToString());

                // Use MainWindow as owner when available to ensure modal behaviour
                var owner = app?.MainWindow;
                MessageBox.Show(owner, messageBuilder.ToString(), caption, MessageBoxButton.OK, MessageBoxImage.Error);

                Log.Error(messageBuilder.ToString());
            }
            catch (Exception exceptionDialogEx)
            {
                Log.Error(exceptionDialogEx, "ShowExceptionDialog error.");
                // If showing the dialog fails, attempt a minimal MessageBox as a last resort
                try
                {
                    string errorMessage = ServiceManager.StringLocalizer[TranslationConstant.FatalErrorOccurred];
                    string fatalErrorTitle = ServiceManager.StringLocalizer[TranslationConstant.FatalErrorTitle];

                    MessageBox.Show(errorMessage, fatalErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception minimalMessageBoxEx)
                {
                    Log.Error(minimalMessageBoxEx, "Show minimal MessageBox error.");
                }
            }
        }

        #endregion
    }
}
