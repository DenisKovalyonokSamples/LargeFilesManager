using LFM.Core.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace LFM.Core.Services
{
    public static class TestServiceHost
    {
        public static ServiceProvider CreateDefaultServiceProvider(Action<IServiceCollection>? configure = null)
        {
            var services = new ServiceCollection();

            // Default AppSettings for tests
            var appSettings = new AppSettings
            {
                DefaultFileTextLineLengthMax = 100 // default used by ViewModel constructor
            };
            services.AddSingleton(Options.Create(appSettings));

            // Simple string localizer
            services.AddSingleton<IStringLocalizer>(new StringLocalizer());

            configure?.Invoke(services);

            var provider = services.BuildServiceProvider();

            // Ensure ServiceLocator points to our test provider
            ApplicationHost.Services = provider;

            return provider;
        }
    }
}
