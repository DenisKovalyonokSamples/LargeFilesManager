using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using LFM.Core.Interfaces;

namespace LFM.Core.Services
{
    public static class ServiceManager
    {
        public static IStringLocalizer StringLocalizer => ApplicationHost.Services?.GetRequiredService<IStringLocalizer>();

        public static IOptions<AppSettings> AppSettings => ApplicationHost.Services?.GetRequiredService<IOptions<AppSettings>>();

        public static ITextFileGeneratorService TextFileGeneratorService => ApplicationHost.Services?.GetRequiredService<ITextFileGeneratorService>();

        public static ITextFileSorterService TextFileSorterService => ApplicationHost.Services?.GetRequiredService<ITextFileSorterService>();
    }
}
