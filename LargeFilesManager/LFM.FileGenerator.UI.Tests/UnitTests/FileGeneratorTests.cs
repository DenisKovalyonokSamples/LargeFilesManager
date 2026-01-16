using LFM.Core;
using LFM.Core.Enums;
using LFM.FileGenerator.ViewModels;
using Microsoft.Extensions.Options;
using Moq;
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
                // override ITextFileGeneratorService
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
    }
}