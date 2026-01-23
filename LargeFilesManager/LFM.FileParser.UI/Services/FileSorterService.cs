using LFM.Core;
using LFM.Core.Constants;
using LFM.Core.Enums;
using LFM.Core.Helpers;
using LFM.Core.Models;
using LFM.Core.Services;
using LFM.FileSorter.Interfaces;
using LFM.FileSorter.ViewModels;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace LFM.FileSorter.Services
{
    public class FileSorterService : BaseService, ITextFileSorterService
    {
        public List<string> PartFileTextPaths { get; set; }

        //Parsed representation of a generator line: "<number>. <string>"
        //For a positional record, PascalCase here is correct because those identifiers become properties. Using camelCase would produce camelCase property names, which violates .NET property naming.
        private sealed record ParsedLine(int NumericPrefix, string Text, string OriginalLine);        

        private static bool TryParseLine(string inputLine, out ParsedLine result)
        {
            result = default!;
            if (string.IsNullOrWhiteSpace(inputLine)) return false;

            int separatorIndex = inputLine.IndexOf(". ");
            if (separatorIndex <= 0) return false;

            // Parse the numeric prefix before ". "
            if (!int.TryParse(inputLine.AsSpan(0, separatorIndex), out int numericPrefix)) return false;

            // Extract the text part after ". "
            string textPart = inputLine.Substring(separatorIndex + 2);

            result = new ParsedLine(numericPrefix, textPart, inputLine);
            return true;
        }

        public FileSorterService() : base()
        {
            PartFileTextPaths = new List<string>();
        }

        public async Task SortTextFile(string inputFileTextPath, string outputFileTextPath, FileSorterViewModel model)
        {
            //Ensure clean state at entry.
            ResetProgressPanelState(); 
            ProgressStatus = ServiceManager.StringLocalizer[TranslationConstant.TextFileStartedToBeSorted];

            // Split the input file into sorted parts.
            await SplitFiles(inputFileTextPath, outputFileTextPath, AppSettings.TotalConsumerTasks, AppSettings.MaxPartFileSizeMegaBytes);

            // Merge sorted part files into the final output file.
            MergeSortedPartFiles(PartFileTextPaths, outputFileTextPath);

            IsDispatcherTimerStopped = true;

            // Delete temporary part files after merging.
            DeletePartFiles(PartFileTextPaths);
            PartFileTextPaths = new List<string>();
            model.ProgresStatus = ServiceManager.StringLocalizer[TranslationConstant.SortTextFileStatusCompleted];
            model.IsFileSorterButtonEnabled = false;
            model.IsResetProcessButtonEnabled = true;
        }

        private async Task SplitFiles(string inputFileTextPath, string outputFileTextPath, int totalConsumerTasks, long maxPartFileSizeMegaBytes)
        {
            string splitInputFileInMultipleSortedPartsFormat = string.Format(ServiceManager.StringLocalizer[TranslationConstant.SplitInputFileInMultipleSortedParts], inputFileTextPath);
            ProgressStatus = splitInputFileInMultipleSortedPartsFormat;

            // Get the size of the input file in bytes.
            string fileName = Path.GetFileNameWithoutExtension(inputFileTextPath);
            FileInfo fileInfo = new FileInfo(inputFileTextPath);
            long inputFileSizeBytes = fileInfo.Length;

            ProgressMaxValue = inputFileSizeBytes;
            ProgressValue = 0;

            // Calculate the target part file size in bytes.
            long targetPartFileSizeBytes = ByteHelper.ConvertToBytes(FileSizeType.MB, maxPartFileSizeMegaBytes);

            // Determine the bounded capacity for the BlockingCollection.
            decimal boundedCapacityDecimal = targetPartFileSizeBytes > 0
                ? (decimal)inputFileSizeBytes / targetPartFileSizeBytes
                : 1m;
            int boundedCapacity = (int)Math.Round(boundedCapacityDecimal, MidpointRounding.AwayFromZero);
            boundedCapacity = Math.Max(boundedCapacity, 1);

            // Create a BlockingCollection to hold parts that will be processed.
            var partQueues = new BlockingCollection<PartQueue>(boundedCapacity: boundedCapacity);

            // Consumer Tasks: Write parts into temporary files.
            var writerTasks = ConsumerTask(partQueues, fileName, totalConsumerTasks);

            // Producer Task: Read the input file and split into sorted parts.
            await ProducerTask(partQueues, inputFileTextPath, targetPartFileSizeBytes);
            await Task.WhenAll(writerTasks);

            // Force Garbage Collector.
            CollectGarbage();
        }

        private List<Task> ConsumerTask(BlockingCollection<PartQueue> partQueues, string fileName, int totalConsumerTasks)
        {
            var writerTasks = new List<Task>();
            var partFileTextPaths = new List<string>();

            // Create a temporary directory to store part files.
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }

            // Start multiple consumer tasks to write sorted text lines into part files.
            for (int i = 0; i < Math.Max(1, totalConsumerTasks); i++)
            {
                writerTasks.Add(Task.Run(() =>
                {
                    // Continuously take sorted text lines from the BlockingCollection and write them into part files.
                    foreach (var partQueue in partQueues.GetConsumingEnumerable())
                    {
                        string sortedPartFileNameFormat = string.Format(ServiceManager.StringLocalizer[TranslationConstant.SortedFilePartName], fileName, (partQueue.Index + 1));
                        string partFilePath = Path.Combine(tempPath, sortedPartFileNameFormat);

                        if (File.Exists(partFilePath))
                        {
                            File.Delete(partFilePath);
                        }

                        string partFileIsSortedFormat = string.Format(ServiceManager.StringLocalizer[TranslationConstant.PartFileIsSorted], partFilePath);
                        ProgressStatus = partFileIsSortedFormat;

                        // Write sorted lines to the part file with explicit UTF-8
                        File.WriteAllLines(partFilePath, partQueue.Lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                        // Ensure thread-safe writing to the output file.
                        lock (ProgressLock)
                        {
                            ProgressValue += new FileInfo(partFilePath).Length;
                        }

                        // Free memory.
                        partQueue.ClearLines();

                        // Ensure thread-safe access to partFileTextPaths.
                        lock (PartFileTextPathLock)
                        {
                            PartFileTextPaths.Add(partFilePath);
                        }
                    }
                }));
            }

            return writerTasks;
        }

        private Task ProducerTask(BlockingCollection<PartQueue> partQueues, string inputFileTextPath, long targetPartFileSizeBytes)
        {
            // Read the input file, split it into parts, sort each part, and add to the BlockingCollection.
            return Task.Run(async () =>
            {
                int partIndex = 0;
                long currentPartSize = 0;
                var currentPartLines = new List<ParsedLine>();

                string? textLine;

                // Read the input file line by line.
                using var reader = new StreamReader(inputFileTextPath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: true);
                while ((textLine = await reader.ReadLineAsync()) != null)
                {
                    // Accurate size in bytes (content + newline)
                    int textLineSize = Encoding.UTF8.GetByteCount(textLine) + Encoding.UTF8.GetByteCount(Environment.NewLine);
                    if (TryParseLine(textLine, out var parsed))
                    {
                        currentPartLines.Add(parsed);
                    }
                    else
                    {
                        // Fallback: treat as number 0 with whole line as words; maintains original
                        currentPartLines.Add(new ParsedLine(0, textLine, textLine));
                    }
                    currentPartSize += textLineSize;

                    if (currentPartSize >= targetPartFileSizeBytes)
                    {
                        // Sort the current part lines by parsed values
                        currentPartLines.Sort(new ParsedLineComparer());
                        partQueues.Add(new PartQueue(partIndex, currentPartLines.Select(pl => pl.OriginalLine).ToList()));

                        // Reset for the next part
                        currentPartLines.Clear();

                        currentPartSize = 0;

                        partIndex++;
                    }
                }

                if (currentPartLines.Count > 0)
                {
                    currentPartLines.Sort(new ParsedLineComparer());
                    partQueues.Add(new PartQueue(partIndex, currentPartLines.Select(pl => pl.OriginalLine).ToList()));
                    currentPartLines.Clear();
                }
                // Indicate that no more parts will be added.
                partQueues.CompleteAdding();
            });
        }

        private void MergeSortedPartFiles(List<string> sortedPartFilePaths, string outputFilePath)
        {
            // Merge sorted part files into one output file using a min-heap-like structure
            ProgressStatus = string.Format(ServiceManager.StringLocalizer[TranslationConstant.MergingSortedPartFileInto], sortedPartFilePaths.Count, outputFilePath);
            ProgressMinValue = 0;
            ProgressMaxValue = sortedPartFilePaths.Sum(x => new FileInfo(x).Length);
            ProgressValue = 0;

            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            var readersByPath = sortedPartFilePaths.ToDictionary(p => p, p => new StreamReader(p, utf8NoBom, detectEncodingFromByteOrderMarks: true));

            // Key = next line (parsed), Value = queue of part-file paths that currently yield that same line (preserves duplicates)
            var candidatesByLine = new SortedDictionary<ParsedLine, Queue<string>>(new ParsedLineComparer());

            // Prime the candidates with the first line of each part file
            foreach (var entry in readersByPath)
            {
                var firstLineText = entry.Value.ReadLine();
                if (firstLineText != null && TryParseLine(firstLineText, out var firstParsedLine))
                {
                    if (!candidatesByLine.TryGetValue(firstParsedLine, out var pathsQueue))
                    {
                        pathsQueue = new Queue<string>();
                        candidatesByLine[firstParsedLine] = pathsQueue;
                    }
                    pathsQueue.Enqueue(entry.Key);
                }
            }

            using var outputWriter = new StreamWriter(outputFilePath, append: false, utf8NoBom);

            while (candidatesByLine.Count > 0)
            {
                var smallestEntry = candidatesByLine.First();
                var currentLine = smallestEntry.Key;
                var pendingSources = smallestEntry.Value;

                // Write current smallest line
                outputWriter.WriteLine(currentLine.OriginalLine);
                int bytesWritten = utf8NoBom.GetByteCount(currentLine.OriginalLine) + utf8NoBom.GetByteCount(outputWriter.NewLine);
                lock (ProgressLock)
                {
                    ProgressValue += bytesWritten;
                }

                // Advance the source that produced this occurrence
                var sourcePath = pendingSources.Dequeue();
                var sourceReader = readersByPath[sourcePath];
                var nextLineText = sourceReader.ReadLine();

                if (pendingSources.Count == 0)
                {
                    candidatesByLine.Remove(currentLine);
                }

                if (nextLineText != null)
                {
                    var nextParsedLine = TryParseLine(nextLineText, out var parsed) ? parsed : new ParsedLine(0, nextLineText, nextLineText);
                    if (!candidatesByLine.TryGetValue(nextParsedLine, out var pathsQueue))
                    {
                        pathsQueue = new Queue<string>();
                        candidatesByLine[nextParsedLine] = pathsQueue;
                    }
                    pathsQueue.Enqueue(sourcePath);
                }
            }

            foreach (var reader in readersByPath.Values)
            {
                reader.Dispose();
            }
        }

        private void DeletePartFiles(List<string> partFileTextPaths)
        {
            foreach (var partFilePath in partFileTextPaths)
            {
                if (File.Exists(partFilePath))
                {
                    File.Delete(partFilePath);
                }
            }
        }

        #region Private Classes

        // Sort by words lexicographically first, then by number ascending
        private sealed class ParsedLineComparer : IComparer<ParsedLine>
        {
            private static readonly StringComparer WordsComparer = StringComparer.Ordinal;
            
            /// <summary>
            /// Compares two lines by Text (alphabetically) then by NumericPrefix (ascending).
            /// </summary>
            public int Compare(ParsedLine? left, ParsedLine? right)
            {
                if (ReferenceEquals(left, right)) return 0;
                if (left is null) return -1;
                if (right is null) return 1;

                // First compare by words
                int wordsComparison = WordsComparer.Compare(left.Text, right.Text);
                if (wordsComparison != 0) return wordsComparison;

                // If words equal, compare by number
                return left.NumericPrefix.CompareTo(right.NumericPrefix);
            }
        }

        #endregion
    }
}
