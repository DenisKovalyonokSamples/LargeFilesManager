using CommunityToolkit.Mvvm.Input;
using LFM.Core.Constants;
using LFM.Core.Models;
using LFM.Core.Services;
using LFM.FileSorter.UI.Services;
using Serilog;
using System.Windows;
using System.Windows.Input;

namespace LFM.FileSorter.UI.ViewModels
{
    public class FileSorterViewModel : BaseViewModel
    {
        #region [ Commands]

        public ICommand SelectInputFileTextCommand { get; set; }

        public ICommand SelectOutputFileTextCommand { get; set; }

        public ICommand SortTextFileCommand { get; set; }

        #endregion

        #region [ Properties ]

        private string _inputFileNamePath;
        public string InputFileNamePath
        {
            get => _inputFileNamePath;
            set
            {
                _inputFileNamePath = value;
                OnPropertyChanged();
                CanExecuteSortTextFile = true;
            }
        }

        private string _outputFileNamePath;
        public string OutputFileNamePath
        {
            get => _outputFileNamePath;
            set
            {
                _outputFileNamePath = value;
                OnPropertyChanged();
                CanExecuteSortTextFile = true;
            }
        }

        private bool _canExecuteSortTextFile;
        private bool CanExecuteSortTextFile
        {
            get => _canExecuteSortTextFile && !string.IsNullOrEmpty(InputFileNamePath) && !string.IsNullOrEmpty(OutputFileNamePath);
            set
            {
                _canExecuteSortTextFile = value;
                OnPropertyChanged();
                ((RelayCommand)SortTextFileCommand).NotifyCanExecuteChanged();
            }
        }

        private bool _isFileInformationPanelEnabled;
        public bool IsFileInformationPanelEnabled
        {
            get => _isFileInformationPanelEnabled;
            set
            {
                _isFileInformationPanelEnabled = value;
                OnPropertyChanged();
            }
        }

        #endregion

        public FileSorterViewModel() : base()
        {
            _inputFileNamePath = string.Empty;
            _outputFileNamePath = string.Empty;
            _isFileInformationPanelEnabled = true;

            SelectInputFileTextCommand = new RelayCommand(SelectInputFileText);
            SelectOutputFileTextCommand = new RelayCommand(SelectOutputFileText);
            SortTextFileCommand = new RelayCommand(async () => await SortTextFile(), () => CanExecuteSortTextFile);
        }

        private void SelectInputFileText()
        {
            string selectedFile = GetSelectedFile();
            if (!string.IsNullOrEmpty(selectedFile))
            {
                InputFileNamePath = selectedFile;
            }
        }

        private void SelectOutputFileText()
        {
            string selectedFile = GetSelectedFile();
            if (!string.IsNullOrEmpty(selectedFile))
            {
                OutputFileNamePath = selectedFile;
            }
        }

        private string GetSelectedFile()
        {
            string fileNamePath = string.Empty;

            using var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.Multiselect = false;
            dialog.Filter = ServiceManager.StringLocalizer[TranslationConstant.DialogFilter];
            dialog.Title = ServiceManager.StringLocalizer[TranslationConstant.BrowseFolder];
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                fileNamePath = dialog.FileName;
            }
            return fileNamePath;
        }

        protected override void Timer_Click(object? sender, EventArgs e)
        {
            ProgressBarSatus = ServiceLocator.TextFileSorterService.ProgressSatus;

            ProgressBarValueMin = ServiceLocator.TextFileSorterService.ProgressMinValue;
            ProgressBarValueMax = ServiceLocator.TextFileSorterService.ProgressMaxValue;
            ProgressBarValue = ServiceLocator.TextFileSorterService.ProgressValue;

            string stopWatchElapsedFormat = ServiceManager.StringLocalizer[TranslationConstant.StopWatchElapsedFormat];
            ProgressBarElapsed = StopWatch.Elapsed.ToString(stopWatchElapsedFormat);

            if (ServiceLocator.TextFileSorterService.IsDispatcherTimerStopped)
            {
                ProgressBarValue = ProgressBarValueMax;
                DispatcherTimer.Stop();
                StopWatch.Stop();

                // Reset cursor
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        private async Task SortTextFile()
        {
            // Set wait cursor
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            IsFileInformationPanelEnabled = false;
            ProgressBarSatus = string.Empty;
            ProgressBarValueMin = 0;
            ProgressBarValueMax = 100;

            ProgressBarVisibility = Visibility.Visible;

            string message = ServiceManager.StringLocalizer[TranslationConstant.GenerateTextFileButtonClicked];
            Log.Information(message);

            StopWatch.Start();
            DispatcherTimer.Start();

            await Task.Run(() =>
            {
                ServiceLocator.TextFileSorterService.SortTextFile(InputFileNamePath, OutputFileNamePath, this);
            });
        }
    }
}
