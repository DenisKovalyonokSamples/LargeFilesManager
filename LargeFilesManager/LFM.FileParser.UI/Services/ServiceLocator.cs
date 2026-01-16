using LFM.Core;
using LFM.FileSorter.UI.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace LFM.FileSorter.UI.Services
{
    public static class ServiceLocator
    {
        public static ITextFileSorterService TextFileSorterService => ApplicationHost.Services?.GetRequiredService<ITextFileSorterService>();
    }
}
