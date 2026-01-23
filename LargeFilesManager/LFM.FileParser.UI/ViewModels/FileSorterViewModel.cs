using CommunityToolkit.Mvvm.Input;
using LFM.Core.Constants;
using LFM.Core.Models;
using LFM.Core.Services;
using LFM.FileSorter.Services;
using Serilog;
using System.Windows;
using System.Windows.Input;

namespace LFM.FileSorter.ViewModels
{
    public class FileSorterViewModel : BaseViewModel
    {
        #region Commands

        public ICommand SelectInputFileTextCommand { get; set; }

        public ICommand SelectOutputFileTextCommand { get; set; }

        public ICommand SortTextFileCommand { get; set; }

        #endregion

        #region Properties

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
                IsFileSorterButtonEnabled = _canExecuteSortTextFile && !string.IsNullOrEmpty(InputFileNamePath) && !string.IsNullOrEmpty(OutputFileNamePath);
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

        private bool _isFileSorterButtonEnabled;
        public bool IsFileSorterButtonEnabled
        {
            get => _isFileSorterButtonEnabled;
            set
            {
                _isFileSorterButtonEnabled = value;
                OnPropertyChanged();
            }
        }

        private string _progresStatus = string.Empty;
        public string ProgresStatus
        {
            get => _progresStatus;
            set
            {
                _progresStatus = value;
                OnPropertyChanged();
            }
        }

        #endregion

        public FileSorterViewModel() : base()
        {
            _inputFileNamePath = string.Empty;
            _outputFileNamePath = string.Empty;
            _isFileInformationPanelEnabled = true;
            _isFileSorterButtonEnabled = false;
            IsResetProcessButtonEnabled = false;
            ProgresStatus = ServiceManager.StringLocalizer[TranslationConstant.SortTextFileStatusInit];

            SelectInputFileTextCommand = new RelayCommand(SelectInputFileText);
            SelectOutputFileTextCommand = new RelayCommand(SelectOutputFileText);
            SortTextFileCommand = new RelayCommand(async () => await SortTextFile(), () => CanExecuteSortTextFile);
            ResetFormCommand = new RelayCommand(ResetForm);
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

        private void ResetForm()
        {
            InputFileNamePath = string.Empty;
            OutputFileNamePath = string.Empty;
            IsFileInformationPanelEnabled = true;
            IsFileSorterButtonEnabled = false;
            IsResetProcessButtonEnabled = false;
            ProgresStatus = ServiceManager.StringLocalizer[TranslationConstant.SortTextFileStatusInit];
            ResetProgressPanel();
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
            ProgressBarStatus = ServiceLocator.TextFileSorterService.ProgressStatus;

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
            // Reset UI and service state before starting a new run
            ResetProgressPanel();
            ServiceLocator.TextFileSorterService.ResetProgressPanelState();

            IsFileInformationPanelEnabled = false;
            IsFileSorterButtonEnabled = false;

            ProgressBarVisibility = Visibility.Visible;
            ProgresStatus = ServiceManager.StringLocalizer[TranslationConstant.SortTextFileStatusInProgress];
            Log.Information(ServiceManager.StringLocalizer[TranslationConstant.GenerateTextFileButtonClicked]);

            StopWatch.Start();
            DispatcherTimer.Start();

            await Task.Run(() =>
            {
                ServiceLocator.TextFileSorterService.SortTextFile(InputFileNamePath, OutputFileNamePath, this);
            });
        }
    }
}
