using LFM.Core.Constants;
using LFM.Core.Enums;
using LFM.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LFM.Core.Helpers
{
    public static class ByteHelper
    {
        public static long ConvertToBytes(FileSizeType FileSizeType, long fileSize)
        {
            return FileSizeType switch
            {
                FileSizeType.B => fileSize,
                FileSizeType.KB => fileSize * 1024,
                FileSizeType.MB => fileSize * 1024 * 1024,
                FileSizeType.GB => fileSize * 1024 * 1024 * 1024,
                _ => throw new ArgumentOutOfRangeException(nameof(FileSizeType), ServiceManager.StringLocalizer[TranslationConstant.UnsupportedFileSizeType])
            };
        }
    }
}
