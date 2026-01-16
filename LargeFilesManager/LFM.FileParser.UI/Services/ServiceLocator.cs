using LFM.Core;
using LFM.FileSorter.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace LFM.FileSorter.Services
{
    public static class ServiceLocator
    {
        public static ITextFileSorterService TextFileSorterService => ApplicationHost.Services?.GetRequiredService<ITextFileSorterService>();
    }
}
