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
        private object MergeFilePartWriterLock { get; set; } // Lock to protect concurrent writes when merging parts

        // Cache allowed chars once: letters only
        private static readonly char[] AllowedChars = // Precomputed set of allowed letters for random words
            Enumerable.Range('A', 26).Select(x => (char)x) // Uppercase A-Z
            .Concat(Enumerable.Range('a', 26).Select(x => (char)x)) // Lowercase a-z
            .ToArray(); // Materialize into array

        public FileGeneratorService() : base()
        {
            MergeFilePartWriterLock = new object(); // Initialize merge writer lock
        }

        public void WriteTextFile(string filePath, string fileName, long fileSize, FileSizeType fileSizeType, int maxLineLength) // Main entry: generate file
        {
            // Ensure clean state at entry.
            ResetProgressPanelState();

            // Ensure a sane minimum buffer (4KB)
            int bufferSize = Math.Max(4096, AppSettings.BufferFileWriteSize * 1024); // Determine FS buffer (min 4KB)
            int numberOfFilesToWriteInParallel = Math.Max(1, ProcessorCount); // Use CPU count or at least 1

            long targetFileSizeInBytes = ByteHelper.ConvertToBytes(fileSizeType, fileSize);

            ProgressValue = 0;
            ProgressMinValue = 0;
            ProgressMaxValue = targetFileSizeInBytes;

            long sizePerFile = targetFileSizeInBytes / numberOfFilesToWriteInParallel; // Divide work among parts
            string[] filePartPaths = new string[numberOfFilesToWriteInParallel]; // Hold paths for part files

            ProgressStatus = string.Format(ServiceManager.StringLocalizer[TranslationConstant.PartFilesToWriteInParallel], numberOfFilesToWriteInParallel); // Update status with parallel part count

            string fulllFileNamePath = Path.Combine(filePath, fileName); // Final output path
            var filePathsToDelete = filePartPaths.ToList(); // Collect part paths for cleanup
            filePathsToDelete.Add(fulllFileNamePath); // Also include final output for pre-clean

            DeleteFile(filePathsToDelete); // Remove any previous files to start clean

            var parallelWrittenFileParts = Parallel.For(0, numberOfFilesToWriteInParallel, new ParallelOptions { MaxDegreeOfParallelism = ProcessorCount }, i => // Write parts in parallel
            {
                string startWriteFilePartFormat = string.Format(ServiceManager.StringLocalizer[TranslationConstant.StartWriteFilePart], (i + 1), numberOfFilesToWriteInParallel, sizePerFile); // Compose start message
                Log.Information(startWriteFilePartFormat);

                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                string fileExtension = Path.GetExtension(fileName);

                string partFileName = numberOfFilesToWriteInParallel > 1 ? $"{fileNameWithoutExtension}.part_{i + 1}{fileExtension}" : fileName; // Part naming or full name if single part
                string partFilePath = Path.Combine(filePath, partFileName);
                filePartPaths[i] = partFilePath; // Store path for merge later

                WriteTextFilePart(partFilePath, sizePerFile, maxLineLength, bufferSize); // Generate the part content

                string finishWriteFilePartFormat = string.Format(ServiceManager.StringLocalizer[TranslationConstant.FinishWriteFilePart], partFilePath); // Compose completion message
                Log.Information(finishWriteFilePartFormat);
            });

            if (parallelWrittenFileParts.IsCompleted)
            {
                Log.Information(ServiceManager.StringLocalizer[TranslationConstant.FinishWriteFilePart]);
                MergeFileParts(filePartPaths, filePath, fileName, bufferSize); // Merge parts into final file

                // Force Garbage Collector
                CollectGarbage();
            }
        }

        private void WriteTextFilePart(string partFilePath, long sizePerFile, int maxLineLength, int bufferSize)
        {
            string writeToFilePartFormat = string.Format(ServiceManager.StringLocalizer[TranslationConstant.WriteToFilePart], partFilePath, sizePerFile);
            Log.Information(writeToFilePartFormat);

            long totalBytesWritten = 0;

            using (var stream = new FileStream(partFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize)) // Create output stream for part
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize)) // Writer with UTF-8 no BOM
            {
                while (totalBytesWritten < sizePerFile) // Loop until reaching target size for part
                {
                    // Generate only alphabetic words separated by spaces; total <= maxLineLength
                    string randomWords = GenerateRandomWordsLine(maxLineLength); // Create random words payload

                    lock (LineNumberLock) // Safely increment global line counter
                    {
                        LineNumber++; // Increment line number used in prefix
                    }

                    int bytesPerLine = WriteLine(writer, randomWords); // Write line and get its byte size
                    totalBytesWritten += bytesPerLine; // Accumulate bytes written

                    lock (ProgressLock) // Update global progress atomically
                    {
                        ProgressValue += bytesPerLine; // Add this line to progress
                    }

                    // If next line would exceed target, write one more of same to approximate size and exit loop
                    if (totalBytesWritten + bytesPerLine >= sizePerFile) // Check if an extra line would exceed target
                    {
                        bytesPerLine = WriteLine(writer, randomWords); // Write one more line to approach target
                        totalBytesWritten += bytesPerLine; // Accumulate bytes
                        lock (ProgressLock) // Update progress safely
                        {
                            ProgressValue += bytesPerLine; // Add bytes to progress
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
            return writer.Encoding.GetByteCount(textLine) + writer.Encoding.GetByteCount(writer.NewLine); // Compute bytes including newline
        }

        // Generates a line comprised solely of alphabetic words separated by single spaces
        // The total character count of the [words] part is <= maxLineLength
        // Produce random alphabetic words up to max length
        private string GenerateRandomWordsLine(int maxLineLength) 
        {
            if (maxLineLength <= 0) return string.Empty;

            var sb = new StringBuilder(maxLineLength); // Pre-size builder for efficiency
            int remaining = maxLineLength; // Track remaining capacity for words

            // Choose 1..N words, fitting within maxLineLength; word length 1..12
            // Stop when adding another word + optional space would exceed limit
            bool first = true; // Flag for first word (no leading space)
            while (remaining > 0) // Until no space remains
            {
                int maxWordLen = Math.Min(12, remaining); // Limit word length to 12 chars or remaining
                if (maxWordLen <= 0) break; // Stop if no space for any word

                int wordLen = Random.Shared.Next(1, Math.Max(2, maxWordLen + 1)); // [1..maxWordLen] inclusive
                if (!first) // If not first, we need a preceding space
                {
                    // Need one char for the space separator
                    if (remaining < (wordLen + 1)) break; // If insufficient for space+word, stop
                    sb.Append(' '); // Append space between words
                    remaining--; // Consume one char for space
                }

                // Append a word of letters only
                for (int i = 0; i < wordLen; i++) // For each character in word
                {
                    sb.Append(AllowedChars[Random.Shared.Next(AllowedChars.Length)]); // Append random letter
                }
                remaining -= wordLen; // Consume used chars
                first = false; // Subsequent iterations will add spaces

                // Occasionally stop early to vary word count. Random early stop or when low remaining.
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

            var outputStream = new FileStream(fulllFileNamePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize); // Create output stream
            var writer = new StreamWriter(outputStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize); // Writer with UTF-8 no BOM

            var mergedFilePartParallelResult = Parallel.For(0, filePartPaths.Length, new ParallelOptions { MaxDegreeOfParallelism = ProcessorCount }, (i) => // Read parts in parallel
            {
                string mergeFilePartFormat = string.Format(ServiceManager.StringLocalizer[TranslationConstant.MergingFilePartOf], (i + 1), filePartPaths.Length, fulllFileNamePath); // Compose merge message
                Log.Information(mergeFilePartFormat);

                using (var inputStream = new FileStream(filePartPaths[i], FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize)) // Open part for reading
                using (var reader = new StreamReader(inputStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: true, bufferSize: bufferSize)) // Reader with BOM detection
                {
                    while (!reader.EndOfStream) // Read until EOF
                    {
                        string? line = reader.ReadLine();
                        if (line == null) break;

                        // Ensure thread-safe writing to the output file.
                        lock (MergeFilePartWriterLock) // Serialize writes to final file
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
                writer.Dispose(); // Dispose writer (flush and close)
                outputStream.Dispose(); // Dispose stream

                string mergingCompletedFinalFileCreatedFormat = string.Format(ServiceManager.StringLocalizer[TranslationConstant.MergingCompletedFinalFileCreated], fulllFileNamePath);
                Log.Information(mergingCompletedFinalFileCreatedFormat);
                // Delete part file after merging.
                DeleteFile(filePartPaths.ToList());

                ProgressStatus = ServiceManager.StringLocalizer[TranslationConstant.FileWritingMergingCompleted];
                IsDispatcherTimerStopped = true; // Signal UI timer to stop
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