using LFM.Core;
using LFM.Core.Comparers;
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
using System.Linq;

namespace LFM.FileSorter.Services
{
    public class FileSorterService : BaseService, ITextFileSorterService
    {
        public List<string> PartFileTextPaths { get; set; }

        // Parsed representation of a generator line: "<number>. <words>"
        private sealed record ParsedLine(int Number, string Words, string Original);

        // Sort by words lexicographically first, then by number ascending
        private sealed class ParsedLineComparer : IComparer<ParsedLine>
        {
            private static readonly StringComparer WordsComparer = StringComparer.Ordinal;
            public int Compare(ParsedLine? x, ParsedLine? y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x is null) return -1;
                if (y is null) return 1;
                // First compare by words
                int byWords = WordsComparer.Compare(x.Words, y.Words);
                if (byWords != 0) return byWords;
                // If words equal, compare by number
                return x.Number.CompareTo(y.Number);
            }
        }

        private static bool TryParseLine(string line, out ParsedLine parsed)
        {
            parsed = default!;
            if (string.IsNullOrEmpty(line)) return false;
            int idx = line.IndexOf(". ");
            if (idx <= 0) return false;
            if (!int.TryParse(line.AsSpan(0, idx), out int number)) return false;
            string words = line.Substring(idx + 2);
            parsed = new ParsedLine(number, words, line);
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
                        partQueues.Add(new PartQueue(partIndex, currentPartLines.Select(pl => pl.Original).ToList()));

                        // Reset for the next part
                        currentPartLines.Clear();

                        currentPartSize = 0;

                        partIndex++;
                    }
                }

                if (currentPartLines.Count > 0)
                {
                    currentPartLines.Sort(new ParsedLineComparer());
                    partQueues.Add(new PartQueue(partIndex, currentPartLines.Select(pl => pl.Original).ToList()));
                    currentPartLines.Clear();
                }
                // Indicate that no more parts will be added.
                partQueues.CompleteAdding();
            });
        }

        private void MergeSortedPartFiles(List<string> partFileTextPaths, string outputFileTextPath)
        {
            // Implementation for merging sorted part files into a single output file
            // This can be done using a priority queue (min-heap) to efficiently merge the sorted files.
            ProgressStatus = string.Format(ServiceManager.StringLocalizer[TranslationConstant.MergingSortedPartFileInto], partFileTextPaths.Count, outputFileTextPath);
            ProgressMinValue = 0;
            ProgressMaxValue = partFileTextPaths.Sum(x => new FileInfo(x).Length);
            ProgressValue = 0;

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            var readers = partFileTextPaths.ToDictionary(x => x, x => new StreamReader(x, encoding, detectEncodingFromByteOrderMarks: true));
            var pq = new SortedDictionary<ParsedLine, Queue<string>>(new ParsedLineComparer());

            // Initialize with first line from each part
            foreach (var kvp in readers)
            {
                var line = kvp.Value.ReadLine();
                if (line != null && TryParseLine(line, out var parsed))
                {
                    if (!pq.TryGetValue(parsed, out var q))
                    {
                        q = new Queue<string>();
                        pq[parsed] = q;
                    }
                    q.Enqueue(kvp.Key);
                }
            }

            using var writer = new StreamWriter(outputFileTextPath, append: false, encoding);
            while (pq.Count > 0)
            {
                var minEntry = pq.First();
                var parsedToWrite = minEntry.Key;
                var filesQueue = minEntry.Value;

                writer.WriteLine(parsedToWrite.Original);
                int delta = encoding.GetByteCount(parsedToWrite.Original) + encoding.GetByteCount(writer.NewLine);
                lock (ProgressLock)
                {
                    ProgressValue += delta;
                }

                var filePath = filesQueue.Dequeue();
                var reader = readers[filePath];
                var next = reader.ReadLine();

                if (filesQueue.Count == 0)
                {
                    pq.Remove(parsedToWrite);
                }

                if (next != null)
                {
                    var nextParsed = TryParseLine(next, out var pl) ? pl : new ParsedLine(0, next, next);
                    if (!pq.TryGetValue(nextParsed, out var q))
                    {
                        q = new Queue<string>();
                        pq[nextParsed] = q;
                    }
                    q.Enqueue(filePath);
                }
            }

            // Close all readers.
            foreach (var reader in readers.Values)
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
    }
}
