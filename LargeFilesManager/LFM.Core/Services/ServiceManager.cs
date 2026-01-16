using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace LFM.Core.Services
{
    public static class ServiceManager
    {
        public static IStringLocalizer StringLocalizer => ApplicationHost.Services?.GetRequiredService<IStringLocalizer>();

        public static IOptions<AppSettings> AppSettings => ApplicationHost.Services?.GetRequiredService<IOptions<AppSettings>>();
    }
}
