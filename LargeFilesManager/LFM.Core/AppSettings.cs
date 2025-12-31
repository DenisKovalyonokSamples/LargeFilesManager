using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LFM.Core
{
    public class AppSettings
    {
        public string LogFilePath { get; set; } = string.Empty;

        public string OutputLogToFileTemplate { get; set; } = string.Empty;
        
        public string MainWindowTitle { get; set; } = string.Empty;

        public string ApplicationIconUri { get; set; } = string.Empty;

        public string BrowseFolderButtonIconUri { get; set; } = string.Empty;

        public string GenerateTextFileButtonIconUri { get; set; } = string.Empty;

        public string SelectInputFileTextButtonIconUri { get; set; } = string.Empty;

        public string SelectOutputFileTextButtonIconUri { get; set; } = string.Empty;

        public string SortTextFileButtonIconUri { get; set; } = string.Empty;

        public int DefaultFileTextLineLengthMax { get; set; } = 0;

        public int BufferFileWriteSize { get; set; } = 0;

        public int TotalConsumerTasks { get; set; } = 0;

        public int MaxPartFileSizeMegaBytes { get; set; } = 0;
    }
}
