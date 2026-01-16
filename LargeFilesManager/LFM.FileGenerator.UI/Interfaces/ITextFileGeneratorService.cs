using LFM.Core.Enums;
using LFM.Core.Interfaces;

namespace LFM.FileGenerator.Interfaces
{
    public interface ITextFileGeneratorService : IBaseService
    {
        /// <summary>
        ///  Write a text file with this line format <number>. <string>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <param name="fileSize"></param>
        /// <param name="fileSizeType"></param>
        /// <param name="maxLineLength"></param>
        void WriteTextFile(string filePath, string fileName, long fileSize, FileSizeType fileSizeType, int maxLineLength);
    }
}
