namespace LFM.Core.Services
{
    public class BaseService
    {
        #region Properties

        protected object PartFileTextPathLock { get; set; }

        protected object LineNumberLock { get; set; }

        protected object ProgressLock { get; set; }

        protected AppSettings AppSettings => ServiceManager.AppSettings.Value;

        protected int ProcessorCount => Environment.ProcessorCount;

        protected ulong LineNumber { get; set; }

        public string ProgressStatus { get; set; }

        public int ProgressMinValue { get; set; }

        public long ProgressMaxValue { get; set; }

        public long ProgressValue { get; set; }

        public bool IsDispatcherTimerStopped { get; set; }

        #endregion

        public BaseService()
        {
            PartFileTextPathLock = new object();
            LineNumberLock = new object();
            ProgressLock = new object();
            LineNumber = 0;
            ProgressStatus = string.Empty;
            ProgressMinValue = 0;
            ProgressMaxValue = 0;
            ProgressValue = 0;
            IsDispatcherTimerStopped = false;
        }

        protected void CollectGarbage()
        {
            GC.Collect(GC.MaxGeneration);
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCComplete();
            GC.Collect();
        }

        public void ResetProgressPanelState()
        {
            IsDispatcherTimerStopped = false;
            ProgressMinValue = 0;
            ProgressMaxValue = 100;
            ProgressValue = 0;
            ProgressStatus = string.Empty;
        }
    }
}
