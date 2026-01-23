using LFM.Core.Enums;
using LFM.Core.Helpers;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace LFM.Core.Models
{
    public class BaseViewModel : INotifyPropertyChanged
    {
        #region Commands

        public ICommand? ResetFormCommand { get; set; }

        #endregion

        #region Properties 

        private Visibility _progressBarVisibility;
        public Visibility ProgressBarVisibility
        {
            get => _progressBarVisibility;
            set
            {
                if (_progressBarVisibility == value) return;
                _progressBarVisibility = value;
                OnPropertyChanged();
            }
        }

        private long _progressBarValueMin;
        public long ProgressBarValueMin
        {
            get => _progressBarValueMin;
            set
            {
                if (_progressBarValueMin == value) return;
                _progressBarValueMin = value;
                OnPropertyChanged();
            }
        }

        private long _progressBarValueMax;
        public long ProgressBarValueMax
        {
            get => _progressBarValueMax;
            set
            {
                if (_progressBarValueMax == value) return;
                _progressBarValueMax = value;
                OnPropertyChanged();
                // Recalculate string when max changes
                ProgressBarValueString = GetProgressBarValueString(SelectFileSizeUnit(_progressBarValue), _progressBarValueMax, _progressBarValue);
            }
        }

        private long _progressBarValue;
        public long ProgressBarValue
        {
            get => _progressBarValue;
            set
            {
                // Clamp to [Min, Max]
                var clamped = value;
                if (clamped < _progressBarValueMin) clamped = _progressBarValueMin;
                if (_progressBarValueMax > _progressBarValueMin && clamped > _progressBarValueMax) clamped = _progressBarValueMax;

                if (_progressBarValue == clamped) return;

                _progressBarValue = clamped;
                OnPropertyChanged();

                var unit = SelectFileSizeUnit(_progressBarValue);
                ProgressBarValueString = GetProgressBarValueString(unit, _progressBarValueMax, _progressBarValue);
            }
        }

        private string _progressBarStatus = string.Empty;
        public string ProgressBarStatus
        {
            get => _progressBarStatus;
            set
            {
                if (_progressBarStatus == value) return;
                _progressBarStatus = value;
                OnPropertyChanged();
            }
        }

        private string _progressBarValueString = string.Empty;
        public string ProgressBarValueString
        {
            get => _progressBarValueString;
            set
            {
                if (_progressBarValueString == value) return;
                _progressBarValueString = value;
                OnPropertyChanged();
            }
        }

        private string _progressBarElapsed = string.Empty;
        public string ProgressBarElapsed
        {
            get => _progressBarElapsed;
            set
            {
                if (_progressBarElapsed == value) return;
                _progressBarElapsed = value;
                OnPropertyChanged();
            }
        }

        private bool _isResetProcessButtonEnabled;
        public bool IsResetProcessButtonEnabled
        {
            get => _isResetProcessButtonEnabled;
            set
            {
                if (_isResetProcessButtonEnabled == value) return;
                _isResetProcessButtonEnabled = value;
                OnPropertyChanged();
            }
        }

        protected DispatcherTimer DispatcherTimer { get; set; }
        protected Stopwatch StopWatch { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        // Cached byte units to avoid recomputation (repeated ByteHelper calls).
        private static readonly long BytesKB = ByteHelper.ConvertToBytes(FileSizeType.KB, 1);
        private static readonly long BytesMB = ByteHelper.ConvertToBytes(FileSizeType.MB, 1);
        private static readonly long BytesGB = ByteHelper.ConvertToBytes(FileSizeType.GB, 1);

        #endregion

        public BaseViewModel()
        {
            ProgressBarVisibility = Visibility.Hidden;

            ProgressBarValueMin = 0;
            ProgressBarValueMax = 100;
            ProgressBarValue = 0;

            DispatcherTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };

            DispatcherTimer.Tick += Timer_Click;

            StopWatch = new Stopwatch();
        }

        public void StartProgressPanel()
        {
            // Make visible, reset and start timing
            ProgressBarVisibility = Visibility.Visible;
            StopWatch.Restart();
            DispatcherTimer.Start();
        }

        public void StopProgressPanel()
        {
            StopWatch.Stop();
            DispatcherTimer.Stop();
        }

        // Stop producing updates and clear elapsed time to 0.
        public void ClearProgressPanelState()
        {
            //Stop producing updates. Clears elapsed time to 0. Without it, a subsequent Start() would continue from the previous run, and the next tick would show an old elapsed value.
            StopWatch.Reset();
            // Zero elapsed. If itwill not be stopped, it will immediately repopulate the cleared properties (elapsed, values, status) and keep consuming CPU.
            DispatcherTimer.Stop();
        }

        public void ResetProgressPanel()
        {
            ClearProgressPanelState();

            ProgressBarVisibility = Visibility.Hidden;
            ProgressBarValueMin = 0;
            ProgressBarValueMax = 100;
            ProgressBarValue = 0;
            ProgressBarStatus = string.Empty;
            ProgressBarValueString = string.Empty;
            ProgressBarElapsed = string.Empty;
        }

        private static FileSizeType SelectFileSizeUnit(long value)
        {
            if (value >= BytesGB) return FileSizeType.GB;
            if (value >= BytesMB) return FileSizeType.MB;
            if (value >= BytesKB) return FileSizeType.KB;
            return FileSizeType.B;
        }

        private string GetProgressBarValueString(FileSizeType fileSizeType, long maxValue, long value)
        {
            // Handle undefined max explicitly
            if (maxValue <= 0)
            {
                if (value <= 0) return string.Empty;
                // Show only current value when max is unknown/zero
                return $"{FormatValue(fileSizeType, value)} {fileSizeType}";
            }

            var current = FormatValue(fileSizeType, value);
            var max = FormatValue(fileSizeType, maxValue);

            if (current == "0") return string.Empty;

            return $"{current} {fileSizeType} / {max} {fileSizeType}";
        }

        private static string FormatValue(FileSizeType fileSizeType, long bytesValue)
        {
            long unitBytes = fileSizeType switch
            {
                FileSizeType.GB => BytesGB,
                FileSizeType.MB => BytesMB,
                FileSizeType.KB => BytesKB,
                _ => 1
            };

            decimal measured = unitBytes == 0 ? 0 : (decimal)bytesValue / unitBytes;
            measured = Math.Round(measured, 2, MidpointRounding.ToEven);

            // Use invariant culture to avoid locale-dependent decimal separators
            return measured.ToString(CultureInfo.InvariantCulture);
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            // If raised from background threads, consider marshaling to UI Dispatcher.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void Timer_Click(object? sender, EventArgs e)
        {
            // Default behavior: update elapsed text
            if (StopWatch.IsRunning)
            {
                var elapsed = StopWatch.Elapsed;
                ProgressBarElapsed = $"{elapsed:hh\\:mm\\:ss}";
            }
        }

        // Optional: call in derived VM or when disposing view
        protected void DetachTimer()
        {
            DispatcherTimer.Tick -= Timer_Click;
        }
    }
}
