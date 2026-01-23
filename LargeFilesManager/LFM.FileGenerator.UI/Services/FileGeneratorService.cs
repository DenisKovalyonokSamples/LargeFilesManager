using LFM.Core;
using LFM.Core.Constants;
using LFM.Core.Enums;
using LFM.Core.Helpers;
using LFM.Core.Services;
using LFM.FileGenerator.Interfaces;
using Serilog;
using System.IO;
using System.Text;

namespace LFM.FileGenerator.Services
{
    public class FileGeneratorService : BaseService, ITextFileGeneratorService
    {
        private object MergeFilePartWriterLock { get; set; }

        // Cache allowed chars once: letters only
        private static readonly char[] AllowedChars =
            Enumerable.Range('A', 26).Select(x => (char)x)
            .Concat(Enumerable.Range('a', 26).Select(x => (char)x))
            .ToArray();

        public FileGeneratorService() : base()
        {
            MergeFilePartWriterLock = new object();
        }

        public void WriteTextFile(string filePath, string fileName, long fileSize, FileSizeType fileSizeType, int maxLineLength)
        {
            // Ensure clean state at entry.
            ResetProgressPanelState();

            // Ensure a sane minimum buffer (4KB)
            int bufferSize = Math.Max(4096, AppSettings.BufferFileWriteSize * 1024);
            int numberOfFilesToWriteInParallel = Math.Max(1, ProcessorCount);

            long targetFileSizeInBytes = ByteHelper.ConvertToBytes(fileSizeType, fileSize);

            ProgressValue = 0;
            ProgressMinValue = 0;
            ProgressMaxValue = targetFileSizeInBytes;

            long sizePerFile = targetFileSizeInBytes / numberOfFilesToWriteInParallel;
            string[] filePartPaths = new string[numberOfFilesToWriteInParallel];

            ProgressStatus = string.Format(ServiceManager.StringLocalizer[TranslationConstant.PartFilesToWriteInParallel], numberOfFilesToWriteInParallel);

            string fulllFileNamePath = Path.Combine(filePath, fileName);
            var filePathsToDelete = filePartPaths.ToList();
            filePathsToDelete.Add(fulllFileNamePath);

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

            long totalBytesWritten = 0;

            using (var stream = new FileStream(partFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize))
            {
                while (totalBytesWritten < sizePerFile)
                {
                    // Generate only alphabetic words separated by spaces; total <= maxLineLength
                    string randomWords = GenerateRandomWordsLine(maxLineLength);

                    lock (LineNumberLock)
                    {
                        LineNumber++;
                    }

                    int bytesPerLine = WriteLine(writer, randomWords);
                    totalBytesWritten += bytesPerLine;

                    lock (ProgressLock)
                    {
                        ProgressValue += bytesPerLine;
                    }

                    // If next line would exceed target, write one more of same to approximate size and exit loop
                    if (totalBytesWritten + bytesPerLine >= sizePerFile)
                    {
                        bytesPerLine = WriteLine(writer, randomWords);
                        totalBytesWritten += bytesPerLine;
                        lock (ProgressLock)
                        {
                            ProgressValue += bytesPerLine;
                        }
                    }
                }
            }
        }

        private int WriteLine(StreamWriter writer, string words)
        {
            string textLine = $"{LineNumber}. {words}";
            writer.WriteLine(textLine);

            // Accurate byte count: content + newline per encoding
            return writer.Encoding.GetByteCount(textLine) + writer.Encoding.GetByteCount(writer.NewLine);
        }

        // Generates a line comprised solely of alphabetic words separated by single spaces
        // The total character count of the [words] part is <= maxLineLength
        private string GenerateRandomWordsLine(int maxLineLength)
        {
            if (maxLineLength <= 0) return string.Empty;

            var sb = new StringBuilder(maxLineLength);
            int remaining = maxLineLength;

            // Choose 1..N words, fitting within maxLineLength; word length 1..12
            // Stop when adding another word + optional space would exceed limit
            bool first = true;
            while (remaining > 0)
            {
                int maxWordLen = Math.Min(12, remaining);
                if (maxWordLen <= 0) break;

                int wordLen = Random.Shared.Next(1, Math.Max(2, maxWordLen + 1)); // [1..maxWordLen]
                if (!first)
                {
                    // Need one char for the space separator
                    if (remaining < (wordLen + 1)) break;
                    sb.Append(' ');
                    remaining--;
                }

                // Append a word of letters only
                for (int i = 0; i < wordLen; i++)
                {
                    sb.Append(AllowedChars[Random.Shared.Next(AllowedChars.Length)]);
                }
                remaining -= wordLen;
                first = false;

                // Occasionally stop early to vary word count
                if (remaining <= 2 || Random.Shared.Next(0, 5) == 0) break;
            }

            return sb.ToString();
        }

        private void MergeFileParts(string[] filePartPaths, string filePath, string fileName, int bufferSize)
        {
            string fulllFileNamePath = Path.Combine(filePath, fileName);
            ProgressStatus = string.Format(ServiceManager.StringLocalizer[TranslationConstant.MergeFilePartInto], filePartPaths.Length, fulllFileNamePath);

            ProgressValue = 0;
            ProgressMinValue = 0;

            var outputStream = new FileStream(fulllFileNamePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize);
            var writer = new StreamWriter(outputStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize);

            var mergedFilePartParallelResult = Parallel.For(0, filePartPaths.Length, new ParallelOptions { MaxDegreeOfParallelism = ProcessorCount }, (i) =>
            {
                string mergeFilePartFormat = string.Format(ServiceManager.StringLocalizer[TranslationConstant.MergingFilePartOf], (i + 1), filePartPaths.Length, fulllFileNamePath);
                Log.Information(mergeFilePartFormat);

                using (var inputStream = new FileStream(filePartPaths[i], FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize))
                using (var reader = new StreamReader(inputStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: true, bufferSize: bufferSize))
                {
                    while (!reader.EndOfStream)
                    {
                        string? line = reader.ReadLine();
                        if (line == null) break;

                        // Ensure thread-safe writing to the output file.
                        lock (MergeFilePartWriterLock)
                        {
                            writer.WriteLine(line);
                            // Accurate byte count for progress
                            ProgressValue += writer.Encoding.GetByteCount(line) + writer.Encoding.GetByteCount(writer.NewLine);
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

                ProgressStatus = ServiceManager.StringLocalizer[TranslationConstant.FileWritingMergingCompleted];
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