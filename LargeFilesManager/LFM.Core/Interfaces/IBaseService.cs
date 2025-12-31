using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LFM.Core.Interfaces
{
    public interface IBaseService
    {
        string ProgressSatus { get; }

        int ProgressMinValue { get; }

        long ProgressMaxValue { get; }

        long ProgressValue { get; }

        bool IsDispatcherTimerStopped { get; }
    }
}
