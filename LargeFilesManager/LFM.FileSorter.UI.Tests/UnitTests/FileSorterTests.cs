using LFM.Core;
using LFM.Core.Services;
using LFM.FileSorter.Services;
using LFM.FileSorter.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace LFM.FileSorter.Tests.UnitTests
{
    public class FileSorterTests
    {
        public FileSorterTests()
        {
            // Baseline provider (will be overridden per-test as needed)
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

            // Configure AppSettings for deterministic behavior and small test footprint
            var customSettings = new AppSettings
            {
                TotalConsumerTasks = 1,
                MaxPartFileSizeMegaBytes = 1, // small parts for test
                DefaultFileTextLineLengthMax = 100,
                BufferFileWriteSize = 4
            };

            TestServiceHost.CreateDefaultServiceProvider(services =>
            {
                // Default simple localizer is fine
                services.AddSingleton(Options.Create(customSettings));
            });

            var service = new FileSorterService();
            var model = new FileSorterViewModel();

            try
            {
                // Act
                await service.SortTextFile(inputPath, outputPath, model);

                // Assert - output file exists
                Assert.True(File.Exists(outputPath), "Sorted output file was not created.");

                var outputLines = File.ReadAllLines(outputPath).ToList();

                // Expected order according to ParsedLineComparer:
                // Compare by text part first (alphabetically), then by numeric prefix (ascending)
                var expected = new[]
                {
                    "2. x",
                    "1. y",
                    "3. y",
                    "4. z",
                };

                Assert.Equal(expected, outputLines);

                // Final model flags updated
                Assert.Equal("SortTextFile_Status_Completed", model.ProgresStatus);
                Assert.False(model.IsFileSorterButtonEnabled);
                Assert.True(model.IsResetProcessButtonEnabled);
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
                services.AddSingleton(Options.Create(customSettings));
            });

            var service = new FileSorterService();
            var model = new FileSorterViewModel();

            try
            {
                // Act
                await service.SortTextFile(inputPath, outputPath, model);

                // Assert
                Assert.True(File.Exists(outputPath));
                var lines = File.ReadAllLines(outputPath);
                Assert.Single(lines);
                Assert.Equal("1. apple", lines[0]);
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

        [Fact]
        public async Task SortTextFile_HandlesMalformedLines_ByTreatingAsTextWithZeroNumericPrefix()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var inputPath = Path.Combine(tempDir, "input_malformed.txt");
            var outputPath = Path.Combine(tempDir, "output_malformed_sorted.txt");

            // Lines missing the "<num>. " pattern, and some valid ones
            var lines = new[]
            {
                "no number header",
                "2. alpha",
                "another bad line",
                "1. alpha"
            };
            await File.WriteAllLinesAsync(inputPath, lines);

            var customSettings = new AppSettings
            {
                TotalConsumerTasks = 1,
                MaxPartFileSizeMegaBytes = 1,
                DefaultFileTextLineLengthMax = 100,
                BufferFileWriteSize = 4
            };

            TestServiceHost.CreateDefaultServiceProvider(services =>
            {
                services.AddSingleton(Options.Create(customSettings));
            });

            var service = new FileSorterService();
            var model = new FileSorterViewModel();

            try
            {
                // Act
                await service.SortTextFile(inputPath, outputPath, model);

                // Assert: Expected ordering by text (Ordinal), then numeric prefix
                // Malformed lines are treated as text with numeric prefix 0.
                var expected = new[]
                {
                    "1. alpha",          // alpha, 1
                    "2. alpha",          // alpha, 2
                    "another bad line",  // a...
                    "no number header"   // n...
                };
                Assert.Equal(expected, File.ReadAllLines(outputPath));
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
                    Log.Error(ex, "SortTextFile_HandlesMalformedLines_ByTreatingAsTextWithZeroNumericPrefix error.");
                }
            }
        }

        [Fact]
        public async Task SortTextFile_TieBreaksByNumericPrefix_WhenTextEqual()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var inputPath = Path.Combine(tempDir, "input_tie.txt");
            var outputPath = Path.Combine(tempDir, "output_tie_sorted.txt");

            var lines = new[]
            {
                "10. same",
                "3. same",
                "2. same",
                "5. same"
            };
            await File.WriteAllLinesAsync(inputPath, lines);

            var customSettings = new AppSettings
            {
                TotalConsumerTasks = 1,
                MaxPartFileSizeMegaBytes = 1,
                DefaultFileTextLineLengthMax = 100,
                BufferFileWriteSize = 4
            };

            TestServiceHost.CreateDefaultServiceProvider(services =>
            {
                services.AddSingleton(Options.Create(customSettings));
            });

            var service = new FileSorterService();
            var model = new FileSorterViewModel();

            try
            {
                // Act
                await service.SortTextFile(inputPath, outputPath, model);

                // Assert
                var expected = new[]
                {
                    "2. same",
                    "3. same",
                    "5. same",
                    "10. same"
                };
                Assert.Equal(expected, File.ReadAllLines(outputPath));
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
                    Log.Error(ex, "SortTextFile_TieBreaksByNumericPrefix_WhenTextEqual error.");
                }
            }
        }
    }
}
