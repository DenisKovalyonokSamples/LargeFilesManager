using LFM.Core;
using LFM.Core.Comparers;
using LFM.Core.Constants;
using LFM.Core.Enums;
using LFM.Core.Helpers;
using LFM.Core.Interfaces;
using LFM.Core.Models;
using LFM.Core.Services;
using System.Collections.Concurrent;
using System.Text;

namespace LFM.FileSorter.BL.Services
{
    public class TextFileSorterService : BaseService, ITextFileSorterService
    {
        public List<string> PartFileTextPaths { get; set; }

        public TextFileSorterService() : base()
        {
            PartFileTextPaths = new List<string>();
        }

        public async Task SortTextFile(string inputFileTextPath, string outputFileTextPath)
        {
            ProgressValue = 0;
            ProgressMinValue = 0;
            ProgressMaxValue = 100;
            ProgressSatus = ServiceManager.StringLocalizer[TranslationConstant.TextFileStartedToBeSorted];

            // Split the input file into sorted parts.
            await SplitFiles(inputFileTextPath, outputFileTextPath, AppSettings.TotalConsumerTasks, AppSettings.MaxPartFileSizeMegaBytes);

            // Merge sorted part files into the final output file.
            MergeSortedPartFiles(PartFileTextPaths, outputFileTextPath);

            IsDispatcherTimerStopped = true;

            // Delete temporary part files after merging.
            DeletePartFiles(PartFileTextPaths);
            PartFileTextPaths = new List<string>();

            //var files = Directory.GetFiles("\\AppData\\Local\\Temp\\0c52515a-5c9a-4bfe-9a43-73e6c06db3dc", "*_sorted_part_*.txt");
            //MergeSortedPartFiles(files.ToList(), outputFileTextPath);
        }

        private async Task SplitFiles(string inputFileTextPath, string outputFileTextPath, int totalConsumerTasks, long maxPartFileSizeMegaBytes)
        {
            string splitInputFileInMultipleSortedPartsFormat = string.Format(ServiceManager.StringLocalizer[TranslationConstant.SplitInputFileInMultipleSortedParts], inputFileTextPath);
            ProgressSatus = splitInputFileInMultipleSortedPartsFormat;

            // Get the size of the input file in bytes.
            string fileName = Path.GetFileNameWithoutExtension(inputFileTextPath);
            FileInfo fileInfo = new FileInfo(inputFileTextPath);
            long inputFileSizeBytes = fileInfo.Length;

            ProgressMaxValue = inputFileSizeBytes;
            ProgressValue = 0;

            // Calculate the target part file size in bytes.
            long targetPartFileSizeBytes = ByteHelper.ConvertToBytes(FileSizeType.MB, maxPartFileSizeMegaBytes);

            // Determine the bounded capacity for the BlockingCollection.
            decimal boundedCapacityDecimal = inputFileSizeBytes / targetPartFileSizeBytes;
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
            for (int i = 0; i < totalConsumerTasks; i++)
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
                        ProgressSatus = partFileIsSortedFormat;

                        // Write sorted lines to the part file.
                        File.WriteAllLines(partFilePath, partQueue.Lines);

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
                var currentPartLines = new List<string>();
                var currentPartLinesArray = Array.Empty<string>();

                string? textLine;

                // Read the input file line by line.
                using var reader = new StreamReader(inputFileTextPath);
                while ((textLine = await reader.ReadLineAsync()) != null)
                {
                    // Approximate size in bytes.
                    var textLineSize = Encoding.UTF8.GetByteCount(textLine + Environment.NewLine.Length);
                    currentPartLines.Add(textLine);
                    currentPartSize += textLineSize;

                    if (currentPartSize >= targetPartFileSizeBytes)
                    {
                        // Sort the current part lines.
                        currentPartLinesArray = currentPartLines.ToArray();
                        Array.Sort(currentPartLinesArray, new TextLineComparer());

                        // Add the sorted part to the BlockingCollection.
                        partQueues.Add(new PartQueue(partIndex, currentPartLinesArray.ToList()));

                        // Reset for the next part and free memory.
                        currentPartLines.Clear();
                        currentPartLines = null!;
                        currentPartLines = new List<string>();

                        Array.Clear(currentPartLinesArray, 0, currentPartLinesArray.Length);
                        currentPartLinesArray = null!;
                        currentPartLinesArray = Array.Empty<string>();

                        currentPartSize = 0;

                        partIndex++;
                    }
                }

                if (currentPartLines.Count > 0)
                {
                    // Sort the current part lines.
                    currentPartLinesArray = currentPartLines.ToArray();
                    Array.Sort(currentPartLinesArray, new TextLineComparer());

                    partQueues.Add(new PartQueue(partIndex, currentPartLinesArray.ToList()));

                    // Free memory.
                    currentPartLines.Clear();
                    currentPartLines = null!;

                    Array.Clear(currentPartLinesArray, 0, currentPartLinesArray.Length);
                    currentPartLinesArray = null!;
                }
                // Indicate that no more parts will be added.
                partQueues.CompleteAdding();
            });
        }

        private void MergeSortedPartFiles(List<string> partFileTextPaths, string outputFileTextPath)
        {
            // Implementation for merging sorted part files into a single output file
            // This can be done using a priority queue (min-heap) to efficiently merge the sorted files.

            ProgressSatus = string.Format(ServiceManager.StringLocalizer[TranslationConstant.MergingSortedPartFileInto], partFileTextPaths.Count, outputFileTextPath);
            ProgressMinValue = 0;
            ProgressMaxValue = partFileTextPaths.Sum(x => new FileInfo(x).Length);
            ProgressValue = 0;

            var readers = partFileTextPaths.ToDictionary(x => x, x => new StreamReader(x));
            var comparer = new TextLineComparer();
            var sortedDictionary = new SortedDictionary<string, List<string>>(comparer);

            // Initialize a sorted dictionary with the first line from each part file.
            foreach (var reader in readers)
            {
                var line = reader.Value.ReadLine();
                if (line != null)
                {
                    Add(sortedDictionary, line, reader.Key);
                }
            }

            if (!File.Exists(outputFileTextPath))
            {
                File.Create(outputFileTextPath).Close();
            }

            // Write merged lines into the output file.
            using var writer = new StreamWriter(outputFileTextPath);
            while (sortedDictionary.Count > 0)
            {
                // Get the minimal entry sorted line ordered by the comparer.
                var minEntry = sortedDictionary.First();

                // Write the minimal entry sorted line into output file.
                writer.WriteLine(minEntry.Key);

                // Read the next line from the corresponding part file and add it to the sorted dictionary.
                foreach (var filePath in minEntry.Value)
                {
                    var reader = readers[filePath];
                    var nextLine = reader.ReadLine();
                    if (nextLine != null)
                    {
                        Add(sortedDictionary, nextLine, filePath);
                    }
                    ProgressValue += Encoding.UTF8.GetByteCount(nextLine + Environment.NewLine);
                }

                // Remove the minimal entry sorted line that has already written to the output file.
                sortedDictionary.Remove(minEntry.Key);
            }

            // Close all readers.
            foreach (var reader in readers.Values)
            {
                reader.Close();
            }
        }

        private void Add(SortedDictionary<string, List<string>> sortedDictionary, string line, string filePath)
        {
            if (!sortedDictionary.ContainsKey(line))
            {
                sortedDictionary[line] = new List<string>();
            }

            if (!sortedDictionary[line].Contains(filePath))
            {
                sortedDictionary[line].Add(filePath);
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
