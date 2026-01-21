using LFM.Core.Constants;
using LFM.Core.Enums;
using LFM.Core.Services;

namespace LFM.Core.Helpers
{
    public static class ByteHelper
    {
        private static readonly int _byteInKB = 1024;
        public static long ConvertToBytes(FileSizeType FileSizeType, long fileSize)
        {
            return FileSizeType switch
            {
                FileSizeType.B => fileSize,
                FileSizeType.KB => fileSize * _byteInKB,
                FileSizeType.MB => fileSize * _byteInKB * _byteInKB,
                FileSizeType.GB => fileSize * _byteInKB * _byteInKB * _byteInKB,
                _ => throw new ArgumentOutOfRangeException(nameof(FileSizeType), ServiceManager.StringLocalizer[TranslationConstant.UnsupportedFileSizeType])
            };
        }
    }
}
