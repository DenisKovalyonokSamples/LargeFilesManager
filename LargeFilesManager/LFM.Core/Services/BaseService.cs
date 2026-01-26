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


        /// <summary>
        /// Performs a single forced, blocking, compacting garbage collection to reclaim memory and compact the managed
        /// heap.
        /// </summary>
        /// NOTE: This is more efficient than multiple separate calls, because they are overly aggressive, adds unnecessary blocking, and duplicates work without delivering additional benefits.
        /// NOTE: GC.WaitForPendingFinalizers blocks the thread until all finalizers run. In a WPF app, this can stall the UI thread unexpectedly.
        /// NOTE: GC.WaitForFullGCComplete is for server GC scenarios where you listen for induced full collections; it’s rarely appropriate in app code and can block for a long time.
        /// NOTE: .NET 8 guidance: Explicit GC calls should be rare. If used, prefer a single, well-scoped compacting collection and avoid extra waits and repeated collections.
        /// NOTE: Used only after large temporary allocations to return memory to the OS.
        protected void CollectGarbage()
        {
            // Trigger a garbage collection for the highest generation available (all generations).
            GC.Collect(
                // Collect up to the max generation (includes Gen0, Gen1, Gen2).
                generation: GC.MaxGeneration,
                // Force the collection regardless of GC heuristics.
                mode: GCCollectionMode.Forced,
                // Block the current thread until the collection completes.
                blocking: true,
                // Request heap compaction to reduce fragmentation and return memory to the OS when possible.
                compacting: true
            );

            // Avoid waiting for finalizers or full GC cycles explicitly;
            // The compacting collection already performed a thorough sweep.
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
