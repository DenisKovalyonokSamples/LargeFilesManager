using LFM.Core;
using LFM.Core.Localization;
using LFM.Core.Services;
using LFM.FileSorter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Serilog;

namespace LFM.FileSorter.Tests.UnitTests
{
    public class FileSorterTests
    {
        public FileSorterTests()
        {
            // baseline provider (will be overridden per-test as needed)
            TestServiceHost.CreateDefaultServiceProvider();
        }

        [Fact]
        public async Task SortTextFile_SortsSmallFile_CreatesSortedOutputAndDeletesPartFiles()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var inputPath = Path.Combine(tempDir, "input.txt");
            var outputPath = Path.Combine(tempDir, "output_sorted.txt");

            // Create unsorted lines (format "<number>. <text>")
            var unsorted = new[]
            {
                "4. z",
                "1. y",
                "2. x",
                "3. y"
            };

            await File.WriteAllLinesAsync(inputPath, unsorted);

            // Configure AppSettings for deterministic behavior
            var customSettings = new AppSettings
            {
                TotalConsumerTasks = 1,
                MaxPartFileSizeMegaBytes = 1, // 1 MB so small test file will be handled as single (or few) part(s)
                DefaultFileTextLineLengthMax = 100,
                BufferFileWriteSize = 4
            };

            // Provide a localizer that returns a safe filename format for part files
            var customLocalizer = new StringLocalizer(new Dictionary<string, string>
            {
                // The key names are the translation keys used by the service. We map the
                // SplitInputFileInMultipleSortedParts key to a safe file name format used by the consumers.
                // We don't require exact TranslationConstant values; TestLocalizer will match on substring.
                { "SplitInputFileInMultipleSortedParts", "1/3 Split \"{0}\" in multiple sorted parts..." },
            });

            TestServiceHost.CreateDefaultServiceProvider(services =>
            {
                services.AddSingleton<IStringLocalizer>(customLocalizer);
                services.AddSingleton(Options.Create(customSettings));
            });

            var service = new FileSorterService();

            try
            {
                // Act TODO
                //await service.SortTextFile(inputPath, outputPath);

                // Assert - output file exists
                Assert.True(File.Exists(outputPath), "Sorted output file was not created.");

                var outputLines = File.ReadAllLines(outputPath).ToList();

                // Expected order according to TextLineComparer:
                // Compare by text part first, then by numeric prefix if texts equal.
                var expected = new[]
                {
                    "2. x",
                    "1. y",
                    "3. y",
                    "4. z",
                };

                Assert.Equal(expected, outputLines);

                // Ensure part files referred by the service were deleted
                if (service.PartFileTextPaths != null)
                {
                    foreach (var part in service.PartFileTextPaths)
                    {
                        Assert.False(File.Exists(part), $"Part file '{part}' should have been deleted after merge.");
                    }
                }
            }
            finally
            {
                // Cleanup
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, recursive: true);
                }
                catch (Exception ex) 
                {
                    Log.Error(ex, "SortTextFile_SortsSmallFile_CreatesSortedOutputAndDeletesPartFiles error.");
                }
            }
        }

        [Fact]
        public async Task SortTextFile_WithSingleLine_ProducesSameSingleLineOutput()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var inputPath = Path.Combine(tempDir, "input_single_line.txt");
            var outputPath = Path.Combine(tempDir, "output_single_sorted_line.txt");

            await File.WriteAllLinesAsync(inputPath, new[] { "1. apple" });

            var customSettings = new AppSettings
            {
                TotalConsumerTasks = 1,
                MaxPartFileSizeMegaBytes = 1,
                DefaultFileTextLineLengthMax = 100,
                BufferFileWriteSize = 4
            };

            TestServiceHost.CreateDefaultServiceProvider(services =>
            {
                // reuse default simple localizer
                services.AddSingleton(Options.Create(customSettings));
            });

            var service = new FileSorterService();

            try
            {
                // Act
                //await service.SortTextFile(inputPath, outputPath);

                // Assert
                Assert.True(File.Exists(outputPath));
                var lines = File.ReadAllLines(outputPath);
                Assert.Single(lines);
                Assert.Equal("1. apple", lines[0]);

                // Ensure any part files were deleted
                if (service.PartFileTextPaths != null)
                {
                    foreach (var part in service.PartFileTextPaths)
                    {
                        Assert.False(File.Exists(part));
                    }
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, recursive: true);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "SortTextFile_WithSingleLine_ProducesSameSingleLineOutput error.");
                }
            }
        }
    }
}
