using LFM.Core;
using LFM.FileGenerator.UI.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace LFM.FileGenerator.UI.Services
{
    public static class ServiceLocator
    {
        public static ITextFileGeneratorService TextFileGeneratorService => ApplicationHost.Services?.GetRequiredService<ITextFileGeneratorService>();
    }
}
