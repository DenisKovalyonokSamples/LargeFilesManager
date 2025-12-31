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

namespace LFM.FileParser.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        //The IHost pattern is used to provide dependency injection, configuration, and logging—bringing modern .NET hosting features to desktop apps.
        public static IHost? AppHost { get; private set; }

        protected override async void OnStartup(System.Windows.StartupEventArgs e)
        {
            // Set default culture to en-US
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            RegisterGlobalExceptionHandlers();
            AppHost = CreateAppHost();

            ConfigureLogging();

            if (AppHost != null)
            {
                await AppHost.StartAsync();
            }
            base.OnStartup(e);
        }

        protected override async void OnExit(System.Windows.ExitEventArgs e)
        {
            if (AppHost != null)
            {
                await AppHost.StopAsync();
                AppHost.Dispose();
            }

            Log.CloseAndFlush();
            base.OnExit(e);
        }

        private void ConfigureLogging()
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

        private IHost CreateAppHost()
        {
            var builder = Host.CreateApplicationBuilder();

            // Add appsettings.json
            builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            // Register AppSettings
            builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));

            // Replace Serilog with default logger
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(Log.Logger);

            // Register Lepo.i18n service
            //builder.Services.AddStringLocalizer(x =>
            //{
            //    // Load translations from embedded resources
            //    x.FromResource<Translations>(new CultureInfo("en-US"));
            //});

            // Register application services
            builder.Services.AddScoped<ITextFileGeneratorService, TextFileGeneratorService>();
            builder.Services.AddScoped<ITextFileSorterService, TextFileSorterService>();

            // Register MainWindow
            builder.Services.AddSingleton<MainWindow>();
            var appHost = builder.Build();

            // Make application services available externally
            ApplicationHost.Services = appHost.Services;

            return appHost;
        }

        private void RegisterGlobalExceptionHandlers()
        {
            // UI thread exceptions (WPF Dispatcher)
            this.DispatcherUnhandledException += (sender, args) =>
            {
                try
                {
                    Log.Error(args.Exception, "DispatcherUnhandledException");
                    ShowExceptionDialog(args.Exception, ServiceManager.StringLocalizer[TranslationConstant.UIThreadError]);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "RegisterGlobalExceptionHandlers function error.");
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
                    // marshals to UI thread so we show the dialog owned by MainWindow
                    Current?.Dispatcher.Invoke(() => ShowExceptionDialog(ex, nonUIThreadErrorMessage));

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
                    Current?.Dispatcher.Invoke(() => ShowExceptionDialog(agg.Flatten(), ServiceManager.StringLocalizer[TranslationConstant.TaskError]));
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

        private void ShowExceptionDialog(Exception ex, string caption = "Application error")
        {
            try
            {
                // Compose a concise message, include full details in "Details" tooltip for debugging
                var messageBuilder = new StringBuilder();
                messageBuilder.AppendLine(ServiceManager.StringLocalizer[TranslationConstant.UnexpectedErrorOccurred]);
                if (!string.IsNullOrWhiteSpace(ex.Message))
                {
                    messageBuilder.AppendLine();
                    messageBuilder.AppendLine(ServiceManager.StringLocalizer[TranslationConstant.Message] + ":");
                    messageBuilder.AppendLine(ex.Message);
                }

                messageBuilder.AppendLine();
                messageBuilder.AppendLine(ServiceManager.StringLocalizer[TranslationConstant.Details] + ":");
                messageBuilder.AppendLine(ex.ToString());

                // Use MainWindow as owner when available to ensure modal behaviour
                var owner = Current?.MainWindow;
                System.Windows.MessageBox.Show(owner, messageBuilder.ToString(), caption, MessageBoxButton.OK, MessageBoxImage.Error);

                Log.Error(messageBuilder.ToString());
            }
            catch
            {
                // If showing the dialog fails, attempt a minimal MessageBox as a last resort
                try
                {
                    string errorMessage = ServiceManager.StringLocalizer[TranslationConstant.FatalErrorOccurred];
                    string fatalErrorTitle = ServiceManager.StringLocalizer[TranslationConstant.FatalErrorTitle];

                    System.Windows.MessageBox.Show(errorMessage, fatalErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { /* swallow */ }
            }
        }
    }

}
