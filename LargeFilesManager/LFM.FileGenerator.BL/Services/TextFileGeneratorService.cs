using LFM.Core;
using LFM.Core.Constants;
using LFM.Core.Enums;
using LFM.Core.Helpers;
using LFM.Core.Interfaces;
using LFM.Core.Services;
using Serilog;
using System.Text;

namespace LFM.FileGenerator.BL.Services
{
    public class TextFileGeneratorService : BaseService, ITextFileGeneratorService
    {
        private object MergeFilePartWriterLock { get; set; }

        public TextFileGeneratorService() : base()
        {
            MergeFilePartWriterLock = new object();
        }

        public void WriteTextFile(string filePath, string fileName, long fileSize, FileSizeType fileSizeType, int maxLineLength)
        {
            int bufferSize = AppSettings.BufferFileWriteSize * 1024; // number KB buffer size
            int numberOfFilesToWriteInParallel = Math.Max(1, ProcessorCount);

            long targetFileSizeInBytes = ByteHelper.ConvertToBytes(fileSizeType, fileSize);

            ProgressValue = 0;
            ProgressMinValue = 0;
            ProgressMaxValue = targetFileSizeInBytes;

            long sizePerFile = targetFileSizeInBytes / numberOfFilesToWriteInParallel;
            string[] filePartPaths = new string[numberOfFilesToWriteInParallel];

            ProgressSatus = string.Format(ServiceManager.StringLocalizer[TranslationConstant.PartFilesToWriteInParallel], numberOfFilesToWriteInParallel);

            string fulllFileNamePath = Path.Combine(filePath, fileName);
            var filePathsToDelete = filePartPaths.ToList();
            filePathsToDelete.Add(fulllFileNamePath);

            // Delete final file if it already exists.
            DeleteFile(filePathsToDelete);

            var parallelWrittenFileParts = Parallel.For(0, numberOfFilesToWriteInParallel, new ParallelOptions { MaxDegreeOfParallelism = ProcessorCount }, i =>
            {
                string startWriteFilePartFormat = string.Format(ServiceManager.StringLocalizer[TranslationConstant.StartWriteFilePart], (i + 1), numberOfFilesToWriteInParallel, sizePerFile);
                Log.Information(startWriteFilePartFormat);

                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                string fileExtension = Path.GetExtension(fileName);

                string partFileName = numberOfFilesToWriteInParallel > 1 ? $"{fileNameWithoutExtension}.part_{i + 1}{fileExtension}" : fileName;
                string partFilePath = Path.Combine(filePath, partFileName);
                filePartPaths[i] = partFilePath;

                WriteTextFilePart(partFilePath, sizePerFile, maxLineLength, bufferSize);

                string finishWriteFilePartFormat = string.Format(ServiceManager.StringLocalizer[TranslationConstant.FinishWriteFilePart], partFilePath);
                Log.Information(finishWriteFilePartFormat);
            });

            if (parallelWrittenFileParts.IsCompleted)
            {
                Log.Information(ServiceManager.StringLocalizer[TranslationConstant.FinishWriteFilePart]);
                MergeFileParts(filePartPaths, filePath, fileName, bufferSize);

                // Force Garbage Collector
                CollectGarbage();
            }
        }

        private void WriteTextFilePart(string partFilePath, long sizePerFile, int maxLineLength, int bufferSize)
        {
            string writeToFilePartFormat = string.Format(ServiceManager.StringLocalizer[TranslationConstant.WriteToFilePart], partFilePath, sizePerFile);
            Log.Information(writeToFilePartFormat);

            var random = new Random();
            long totalBytesWritten = 0;
            int textLineLength = 0;

            string randomText = string.Empty;
            string textLine = string.Empty;
            int bytesPerLine = 0;

            // FileStream and StreamWriter are used to avoid loading everything into memory.
            using (var stream = new FileStream(partFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize))
            {
                while (totalBytesWritten < sizePerFile)
                {
                    textLineLength = random.Next(1, maxLineLength + 1);

                    // A chance to generate a new random text line or reuse the previous one
                    randomText = GenerateRandomStringLine(textLineLength);

                    // Ensure thread-safe incrementing of LineNumber.
                    lock (LineNumberLock)
                    {
                        LineNumber++;
                    }

                    bytesPerLine = WriteLine(writer, randomText);
                    totalBytesWritten += bytesPerLine;

                    // Ensure thread-safe updating of ProgressValue.
                    lock (ProgressLock)
                    {
                        ProgressValue += bytesPerLine;
                    }

                    /* Check if adding another line would exceed the target size.
                     * If so, write one last line and exit the loop.
                     * This ensures we do not exceed the target file size significantly.
                     * Each part file will have the last 2 lines duplicated.
                    */
                    if ((totalBytesWritten + bytesPerLine) >= sizePerFile)
                    {
                        bytesPerLine = WriteLine(writer, randomText);
                        totalBytesWritten += bytesPerLine;

                        // Ensure thread-safe updating of ProgressValue.
                        lock (ProgressLock)
                        {
                            ProgressValue += bytesPerLine;
                        }
                    }
                }
            }
        }

        private int WriteLine(StreamWriter writer, string randomText)
        {
            string textLine = $"{LineNumber}. {randomText}";

            writer.WriteLine(textLine);
            return Encoding.UTF8.GetByteCount(textLine) + Environment.NewLine.Length;
        }

        private string GenerateRandomStringLine(int length)
        {
            var lowerCaseChars = Enumerable.Range('a', 26).Select(x => (char)x);
            var upperCaseChars = Enumerable.Range('A', 26).Select(x => (char)x);

            string allChars = new string(upperCaseChars.Concat(lowerCaseChars).ToArray());

            var random = new Random();

            var stringBuilder = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                stringBuilder.Append(allChars[random.Next(allChars.Length)]);
            }
            return stringBuilder.ToString();
        }

        private void MergeFileParts(string[] filePartPaths, string filePath, string fileName, int bufferSize)
        {
            string fulllFileNamePath = Path.Combine(filePath, fileName);
            ProgressSatus = string.Format(ServiceManager.StringLocalizer[TranslationConstant.MergeFilePartInto], filePartPaths.Length, fulllFileNamePath);

            ProgressValue = 0;
            ProgressMinValue = 0;

            var outputStream = new FileStream(fulllFileNamePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize);
            var writer = new StreamWriter(outputStream);

            var mergedFilePartParallelResult = Parallel.For(0, filePartPaths.Length, new ParallelOptions { MaxDegreeOfParallelism = ProcessorCount }, (i) =>
            {
                string mergeFilePartFormat = string.Format(ServiceManager.StringLocalizer[TranslationConstant.MergingFilePartOf], (i + 1), filePartPaths.Length, fulllFileNamePath);
                Log.Information(mergeFilePartFormat);

                using (var inputStream = new FileStream(filePartPaths[i], FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize))
                using (var reader = new StreamReader(inputStream))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();

                        // Ensure thread-safe writing to the output file.
                        lock (MergeFilePartWriterLock)
                        {
                            writer.WriteLine(line);
                            ProgressValue += Encoding.UTF8.GetByteCount(line + Environment.NewLine);
                        }
                    }
                }
            });

            // Wait for all parts to be merged.
            if (mergedFilePartParallelResult.IsCompleted)
            {
                writer.Dispose();
                outputStream.Dispose();

                string mergingCompletedFinalFileCreatedFormat = string.Format(ServiceManager.StringLocalizer[TranslationConstant.MergingCompletedFinalFileCreated], fulllFileNamePath);
                Log.Information(mergingCompletedFinalFileCreatedFormat);

                // Delete part file after merging.
                DeleteFile(filePartPaths.ToList());

                ProgressSatus = ServiceManager.StringLocalizer[TranslationConstant.FileWritingMergingCompleted];
                IsDispatcherTimerStopped = true;
            }
        }

        private void DeleteFile(List<string> filePaths)
        {
            string deleteExistingFileFormat = string.Empty;
            foreach (var filePath in filePaths)
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);

                    deleteExistingFileFormat = string.Format(ServiceManager.StringLocalizer[TranslationConstant.DeleteExistingFile], filePath);
                    Log.Information(deleteExistingFileFormat);
                }
            }
        }
    }
}
