using LFM.Core;
using LFM.Core.Enums;
using LFM.FileGenerator.Services;
using LFM.FileGenerator.ViewModels;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using LFM.Core.Services;
using LFM.FileGenerator.Interfaces;

namespace LFM.FileGenerator.Tests.UnitTests
{
    public class FileGeneratorTests
    {
        public FileGeneratorTests()
        {
            // Ensure default service provider with required registrations
            TestServiceHost.CreateDefaultServiceProvider();
        }

        [Fact]
        public void Constructor_SetsDefaults()
        {
            // Arrange
            var provider = TestServiceHost.CreateDefaultServiceProvider();

            // Act
            var vm = new FileGeneratorViewModel();

            // Assert
            Assert.Equal(provider.GetRequiredService<IOptions<AppSettings>>().Value.DefaultFileTextLineLengthMax, vm.FileTextLineLengthMax);
            Assert.NotNull(vm.FileSizeTypes);
            Assert.Equal(4, vm.FileSizeTypes.Count); // B, KB, MB, GB
            Assert.Equal(FileSizeType.GB, vm.SelectedFileSizeType);
            Assert.NotNull(vm.GenerateTextFileCommand);
            Assert.False(vm.GenerateTextFileCommand.CanExecute(null));
        }

        [Fact]
        public async Task GenerateTextFileCommand_CallsService_WriteTextFile()
        {
            // Arrange
            var mockGenerator = new Mock<ITextFileGeneratorService>();
            var provider = TestServiceHost.CreateDefaultServiceProvider(services =>
            {
                // Override ITextFileGeneratorService
                services.AddSingleton<ITextFileGeneratorService>(mockGenerator.Object);
            });

            var vm = new FileGeneratorViewModel();

            // Prepare valid inputs so command can execute
            vm.FilePath = Path.GetTempPath();
            vm.FileName = $"unittest_{Guid.NewGuid():N}.txt";
            vm.FileSize = 1; // 1 GB (SelectedFileSizeType default is GB)
            vm.FileTextLineLengthMax = 10;
            vm.SelectedFileSizeType = FileSizeType.B; // small to avoid real heavy operations; but service is mocked

            Assert.True(vm.GenerateTextFileCommand.CanExecute(null));

            // Act

            var thread = new Thread(() =>
            {
                vm.GenerateTextFileCommand.Execute(null);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();



            // The RelayCommand in view model runs an async lambda which uses Task.Run to call the service.
            // Wait for the mock to be invoked (short timeout).
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 2000)
            {
                try
                {
                    mockGenerator.Verify(s => s.WriteTextFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<FileSizeType>(), It.IsAny<int>()), Times.AtLeastOnce);
                    // Verified, exit loop
                    break;
                }
                catch (Moq.MockException)
                {
                    await Task.Delay(50);
                }
            }

            // Assert that the service's WriteTextFile was called at least once
            mockGenerator.Verify(s => s.WriteTextFile(vm.FilePath, vm.FileName, vm.FileSize, vm.SelectedFileSizeType, vm.FileTextLineLengthMax), Times.AtLeastOnce);
            Assert.Equal(Visibility.Visible, vm.ProgressBarVisibility);
        }

        [Fact]
        public void WriteTextFile_CreatesFinalFile_AndDeletesPartFiles()
        {
            // Arrange
            TestServiceHost.CreateDefaultServiceProvider(); // default settings are fine
            var service = new FileGeneratorService();

            var tempDir = Path.Combine(Path.GetTempPath(), "gen_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var fileName = "tiny_output.txt";
            var cpu = Environment.ProcessorCount;
            // Ensure sizePerFile >= 1 byte for each part
            long totalBytes = cpu * 8; // bytes
            int maxLineLen = 4;

            try
            {
                // Act
                service.WriteTextFile(tempDir, fileName, totalBytes, FileSizeType.B, maxLineLen);

                // Assert final file exists
                var finalPath = Path.Combine(tempDir, fileName);
                Assert.True(File.Exists(finalPath), "Final merged file was not created.");
                var length = new FileInfo(finalPath).Length;
                Assert.True(length >= 1, "Final merged file should not be empty for tiny write.");

                // Assert part files are deleted (if any were created)
                var fileNameNoExt = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                var partFiles = Directory.GetFiles(tempDir, $"{fileNameNoExt}.part_*{ext}");
                Assert.Empty(partFiles);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, recursive: true);
                }
                catch { /* ignore for test cleanup */ }
            }
        }

        [Fact]
        public void WriteTextFile_WithZeroMaxLineLength_WritesNumberOnlyLines()
        {
            // Arrange
            TestServiceHost.CreateDefaultServiceProvider();
            var service = new FileGeneratorService();

            var tempDir = Path.Combine(Path.GetTempPath(), "gen_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var fileName = "zero_len_lines.txt";
            var cpu = Environment.ProcessorCount;
            long totalBytes = cpu * 8; // bytes so each part writes a minimal line
            int maxLineLen = 0;

            try
            {
                // Act
                service.WriteTextFile(tempDir, fileName, totalBytes, FileSizeType.B, maxLineLen);

                // Assert
                var finalPath = Path.Combine(tempDir, fileName);
                Assert.True(File.Exists(finalPath), "Final merged file was not created.");

                var lines = File.ReadAllLines(finalPath);
                // Allow empty (in case of minimal size/part rounding), else validate format: "<number>. "
                if (lines.Length > 0)
                {
                    var rx = new Regex(@"^\d+\. $");
                    Assert.All(lines, l => Assert.Matches(rx, l));
                }

                // No part files remaining
                var fileNameNoExt = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                var partFiles = Directory.GetFiles(tempDir, $"{fileNameNoExt}.part_*{ext}");
                Assert.Empty(partFiles);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, recursive: true);
                }
                catch { /* ignore for test cleanup */ }
            }
        }
    }
}