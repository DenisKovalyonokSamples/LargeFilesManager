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
        protected override void OnStartup(StartupEventArgs e)
        {
            RegisterAppServices();
            base.OnStartup(e); //Ensures that standard startup logic was executed.
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppStartupHelper.FinishAppServices();         
            base.OnExit(e); //Ensures that standard shutdown logic was executed.
        }

        private void RegisterAppServices()
        {
            var builder = AppStartupHelper.CreateAppBuilder();

            // Register application services
            builder.Services.AddScoped<ITextFileSorterService, TextFileSorterService>();
            builder.Services.AddSingleton<MainWindow>();

            AppStartupHelper.CreateAppHost(builder, this);
        }
    }
}
