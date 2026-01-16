using LFM.Core.Enums;
using LFM.Core.Helpers;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace LFM.Core.Models
{
    public class BaseViewModel : INotifyPropertyChanged
    {
        #region Properties 

        private Visibility _progressBarVisibility;
        public Visibility ProgressBarVisibility
        {
            get => _progressBarVisibility;
            set
            {
                _progressBarVisibility = value;
                OnPropertyChanged();
            }
        }

        private int _progressBarValueMin;
        public int ProgressBarValueMin
        {
            get => _progressBarValueMin;
            set
            {
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
                _progressBarValueMax = value;
                OnPropertyChanged();
            }
        }

        private long _progressBarValue;
        public long ProgressBarValue
        {
            get => _progressBarValue;
            set
            {
                _progressBarValue = value;
                OnPropertyChanged();

                long kb = ByteHelper.ConvertToBytes(FileSizeType.KB, 1);
                long mb = ByteHelper.ConvertToBytes(FileSizeType.MB, 1);
                long gb = ByteHelper.ConvertToBytes(FileSizeType.GB, 1);

                FileSizeType fileSizeType = FileSizeType.B;

                if (value >= gb)
                {
                    fileSizeType = FileSizeType.GB;
                }
                else if (value >= mb)
                {
                    fileSizeType = FileSizeType.MB;
                }
                else if (value >= kb)
                {
                    fileSizeType = FileSizeType.KB;
                }

                ProgressBarValueString = GetProgressBarValueString(fileSizeType, _progressBarValueMax, value);
            }
        }

        private string _progressBarSatus;
        public string ProgressBarSatus
        {
            get => _progressBarSatus;
            set
            {
                _progressBarSatus = value;
                OnPropertyChanged();
            }
        }

        private string _progressBarValueString;
        public string ProgressBarValueString
        {
            get => _progressBarValueString;
            set
            {
                _progressBarValueString = value;
                OnPropertyChanged();
            }
        }

        private string _progressBarElapsed;
        public string ProgressBarElapsed
        {
            get => _progressBarElapsed;
            set
            {
                _progressBarElapsed = value;
                OnPropertyChanged();
            }
        }

        protected DispatcherTimer DispatcherTimer { get; set; }
        protected Stopwatch StopWatch { get; set; }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;

        public BaseViewModel()
        {
            ProgressBarVisibility = Visibility.Hidden;

            _progressBarSatus = string.Empty;
            _progressBarValueString = string.Empty;
            _progressBarElapsed = string.Empty;

            ProgressBarValueMin = 0;
            ProgressBarValueMax = 100;
            ProgressBarValue = 0;

            DispatcherTimer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };

            DispatcherTimer.Tick += Timer_Click;
            StopWatch = new Stopwatch();
        }

        private string GetProgressBarValueString(FileSizeType fileSizeType, decimal maxValue, decimal value)
        {
            long bytes = ByteHelper.ConvertToBytes(fileSizeType, 1);

            decimal valueByteMeasure = value / bytes;
            valueByteMeasure = Math.Round(valueByteMeasure, 2, MidpointRounding.ToEven);

            decimal maxValueByteMeasure = maxValue / bytes;
            maxValueByteMeasure = Math.Round(maxValueByteMeasure, 2, MidpointRounding.ToEven);

            if (valueByteMeasure == 0)
            {
                return string.Empty;
            }
            else
            {
                return $"{valueByteMeasure} {fileSizeType.ToString()} / {maxValueByteMeasure} {fileSizeType.ToString()}";
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected virtual void Timer_Click(object? sender, EventArgs e)
        {
        }
    }
}
