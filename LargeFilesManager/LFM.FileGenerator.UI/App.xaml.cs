using LFM.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
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
        protected override void OnStartup(StartupEventArgs e)
        {
            RegisterAppServices();           
            base.OnStartup(e);//Ensures that standard startup logic was executed.
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
            builder.Services.AddScoped<ITextFileGeneratorService, TextFileGeneratorService>();
            builder.Services.AddSingleton<MainWindow>();

            AppStartupHelper.CreateAppHost(builder, this);
        }
    }
}
