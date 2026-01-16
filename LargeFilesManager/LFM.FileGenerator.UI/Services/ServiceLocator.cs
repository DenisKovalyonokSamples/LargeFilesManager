using LFM.Core;
using LFM.FileGenerator.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace LFM.FileGenerator.Services
{
    public static class ServiceLocator
    {
        public static ITextFileGeneratorService TextFileGeneratorService => ApplicationHost.Services?.GetRequiredService<ITextFileGeneratorService>();
    }
}
