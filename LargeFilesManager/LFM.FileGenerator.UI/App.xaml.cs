using LFM.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using LFM.FileGenerator.BL.Services;
using LFM.Core.Helpers;

namespace LFM.FileGenerator.UI
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var builder = AppStartupHelper.CreateAppBuilder();

            // Register application services
            builder.Services.AddScoped<ITextFileGeneratorService, TextFileGeneratorService>();
            builder.Services.AddSingleton<MainWindow>();

            AppStartupHelper.CreateAppHost(builder, this);

            base.OnStartup(e);//Ensures that standard startup logic was executed.
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppStartupHelper.FinishAppServices();            
            base.OnExit(e); //Ensures that standard shutdown logic was executed.
        }
    }
}
