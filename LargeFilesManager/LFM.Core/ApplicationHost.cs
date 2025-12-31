using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LFM.Core
{
    public static class ApplicationHost
    {
        /// <summary>
        /// Sets or gets the service provider for dependency injection.
        /// </summary>
        public static IServiceProvider? Services { get; set; }
    }
}
