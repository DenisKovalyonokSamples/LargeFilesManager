namespace LFM.Core.Interfaces
{
    public interface IBaseService
    {
        string ProgressStatus { get; }

        int ProgressMinValue { get; }

        long ProgressMaxValue { get; }

        long ProgressValue { get; }

        bool IsDispatcherTimerStopped { get; }

        void ResetProgressPanelState();
    }
}
