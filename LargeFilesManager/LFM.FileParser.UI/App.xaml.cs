using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using LFM.Core.Helpers;
using LFM.FileSorter.Services;
using LFM.FileSorter.Interfaces;

namespace LFM.FileSorter
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var builder = AppStartupHelper.CreateAppBuilder();

            // Register application services
            builder.Services.AddScoped<ITextFileSorterService, FileSorterService>();
            builder.Services.AddSingleton<MainWindow>();

            AppStartupHelper.CreateAppHost(builder, this);

            base.OnStartup(e); //Ensures that standard startup logic was executed.
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppStartupHelper.FinishAppServices();         
            base.OnExit(e); //Ensures that standard shutdown logic was executed.
        }
    }
}
