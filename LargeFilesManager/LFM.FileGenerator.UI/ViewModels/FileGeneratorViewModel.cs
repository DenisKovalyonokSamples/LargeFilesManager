using CommunityToolkit.Mvvm.Input;
using LFM.Core.Constants;
using LFM.Core.Enums;
using LFM.Core.Models;
using LFM.Core.Services;
using LFM.FileGenerator.Services;
using Serilog;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace LFM.FileGenerator.ViewModels
{
    public class FileGeneratorViewModel : BaseViewModel
    {
        #region Commands

        public ICommand BrowseFolderCommand { get; }

        public ICommand GenerateTextFileCommand { get; }

        #endregion

        #region Properties

        private string _filePath;
        public string FilePath
        {
            get => _filePath;
            set
            {
                _filePath = value;
                OnPropertyChanged();
                CanExecuteGenerateTextFile = true;
            }
        }

        private string _fileName;
        public string FileName
        {
            get => _fileName;
            set
            {
                _fileName = value;
                CanExecuteGenerateTextFile = true;
                OnPropertyChanged();
            }
        }

        private long _fileSize;
        public long FileSize
        {
            get => _fileSize;
            set
            {
                _fileSize = value;
                OnPropertyChanged();
                CanExecuteGenerateTextFile = true;
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

        private string _progresStatusColor = string.Empty;
        public string ProgresStatusColor
        {
            get => _progresStatusColor;
            set
            {
                _progresStatusColor = value;
                OnPropertyChanged();
            }
        }

        private int _fileTextLineLengthMax;
        public int FileTextLineLengthMax
        {
            get => _fileTextLineLengthMax;
            set
            {
                _fileTextLineLengthMax = value;
                OnPropertyChanged();
                CanExecuteGenerateTextFile = true;
            }
        }

        public ObservableCollection<FileSizeType> FileSizeTypes { get; private set; }

        public FileSizeType _selectedFileSizeType;
        public FileSizeType SelectedFileSizeType
        {
            get { return _selectedFileSizeType; }
            set
            {
                _selectedFileSizeType = value;
                OnPropertyChanged();
                CanExecuteGenerateTextFile = true;
            }
        }

        private bool _isFileGeneratorButtonEnabled;
        public bool IsFileGeneratorButtonEnabled
        {
            get => _isFileGeneratorButtonEnabled;
            set
            {
                _isFileGeneratorButtonEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool _canExecuteGenerateTextFile;
        private bool CanExecuteGenerateTextFile
        {
            get => IsStatusPanelFilled;
            set
            {
                _canExecuteGenerateTextFile = value;
                IsFileGeneratorButtonEnabled = IsStatusPanelFilled;
                OnPropertyChanged();
                ((RelayCommand)GenerateTextFileCommand)?.NotifyCanExecuteChanged();
            }
        }

        #endregion

        private bool IsStatusPanelFilled => _canExecuteGenerateTextFile && !string.IsNullOrWhiteSpace(FilePath)
                                               && !string.IsNullOrWhiteSpace(FileName)
                                               && FileSize > 0
                                               && FileTextLineLengthMax > 0;

        public FileGeneratorViewModel() : base()
        {
            _filePath = string.Empty;
            _fileName = string.Empty;
            _fileSize = 0;

            FilePath = string.Empty;
            FileName = string.Empty;
            FileSize = 0;
            _isFileInformationPanelEnabled = true;
            _isFileGeneratorButtonEnabled = false;
            ProgresStatus = ServiceManager.StringLocalizer[TranslationConstant.GenerateTextFileStatusInit];

            FileTextLineLengthMax = ServiceManager.AppSettings.Value.DefaultFileTextLineLengthMax;

            FileSizeTypes = new ObservableCollection<FileSizeType>(Enum.GetValues(typeof(FileSizeType)).Cast<FileSizeType>().ToList());
            SelectedFileSizeType = FileSizeType.GB;


            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            GenerateTextFileCommand = new RelayCommand(async () => await GenerateTextFile(), () => CanExecuteGenerateTextFile);
            ResetFormCommand = new RelayCommand(ResetForm);
        }

        private void ResetForm()
        {
            FilePath = string.Empty;
            FileName = string.Empty;
            IsFileInformationPanelEnabled = true;
            IsResetProcessButtonEnabled = false;
            ProgresStatus = ServiceManager.StringLocalizer[TranslationConstant.SortTextFileStatusInit];

            ResetProgressPanel();
        }

        protected override void Timer_Click(object? sender, EventArgs e)
        {
            ProgressBarStatus = ServiceLocator.TextFileGeneratorService.ProgressSatus;

            ProgressBarValueMin = ServiceLocator.TextFileGeneratorService.ProgressMinValue;
            ProgressBarValueMax = ServiceLocator.TextFileGeneratorService.ProgressMaxValue;
            ProgressBarValue = ServiceLocator.TextFileGeneratorService.ProgressValue;

            string stopWatchElapsedFormat = ServiceManager.StringLocalizer[TranslationConstant.StopWatchElapsedFormat];
            ProgressBarElapsed = StopWatch.Elapsed.ToString(stopWatchElapsedFormat);

            if (ServiceLocator.TextFileGeneratorService.IsDispatcherTimerStopped)
            {
                ProgressBarValue = ProgressBarValueMax;
                DispatcherTimer.Stop();
                StopWatch.Stop();

                // Reset cursor 
                Mouse.OverrideCursor = null;
            }
        }

        private async Task GenerateTextFile()
        {
            // Set wait cursor
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            IsFileInformationPanelEnabled = false;
            IsFileGeneratorButtonEnabled = false;

            ProgressBarVisibility = Visibility.Visible;
            ProgresStatus = ServiceManager.StringLocalizer[TranslationConstant.GenerateTextFileStatusInProgress];
            Log.Information(ServiceManager.StringLocalizer[TranslationConstant.GenerateTextFileButtonClicked]);

            StopWatch.Start();
            DispatcherTimer.Start();

            await Task.Run(() =>
            {
                ServiceLocator.TextFileGeneratorService.WriteTextFile(FilePath, FileName, FileSize, SelectedFileSizeType, FileTextLineLengthMax);
            });

            ProgresStatus = ServiceManager.StringLocalizer[TranslationConstant.GenerateTextFileStatusCompleted];
            IsFileGeneratorButtonEnabled = false;
            IsResetProcessButtonEnabled = true;
        }

        private void BrowseFolder()
        {
            using var dialog = new FolderBrowserDialog();
            string description = ServiceManager.StringLocalizer[TranslationConstant.FolderBrowserDialogDescriptionSelectFolder];
            dialog.Description = description;
            dialog.ShowNewFolderButton = true;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FilePath = dialog.SelectedPath;
            }
        }
    }
}
