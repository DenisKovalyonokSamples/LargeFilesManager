# LargeFilesManager

LargeFilesManager consists of two WPF applications and a shared core:
<br><br>

1.	File Generator (LFM.FileGenerator.UI): Creates large text files efficiently.
   
   ![image_generate_def](https://github.com/DenisKovalyonokSamples/LargeFilesManager/blob/main/Screenshots/FileGeneratorDefaultState.png)
<br><br>

2.	File Sorter (LFM.FileParser.UI): Sorts very large files deterministically without loading the whole file into memory.
   
   ![image_sort_def](https://github.com/DenisKovalyonokSamples/LargeFilesManager/blob/main/Screenshots/FileSorterDefaultState.png)
<br><br>

Both apps are designed to process large files with parallelism, streaming I/O, and consistent progress reporting.

3.	Core (LFM.Core): Shared services, configuration, logging, localization, helpers, and comparers.
<br><br>

File Format
<br><br>

Lines generated and processed follow the template: <number>. <text>

Example:
1.	Apple
2.	Apple
3.	Banana is yellow
4.	Cherry is the best
5.	Something something something
<br><br>

File Generator
<br><br>

Purpose: Generates large text files quickly, splitting work across part files and merging them into a single output.

Key characteristics:
1.	Streaming I/O with FileStream + StreamWriter.
2.	Parallel part generation based on processor count.
3.	Deterministic line structure; words-only generation for text.
4.	Accurate, thread-safe progress tracking.
5.	Explicit UTF-8 (no BOM) encoding for consistency.
<br><br>

Generation steps:
<br><br>

1.	Initialization
-	Reset progress panel state: ProgressMinValue, ProgressMaxValue, ProgressValue, ProgressStatus.
-	Compute target file size in bytes using ByteHelper.ConvertToBytes(fileSizeType, fileSize).
-	Determine buffer size from BufferFileWriteSize with a safe minimum (4 KB).
-	Select degree of parallelism based on ProcessorCount.
-	Compute sizePerFile = targetSize / parallelParts.
-	Delete any existing final file and stale part files.

![image_generate_def](https://github.com/DenisKovalyonokSamples/LargeFilesManager/blob/main/Screenshots/FileGeneratorDefaultState.png)
<br><br>

2.	Information added:
   
![image_generate_def](https://github.com/DenisKovalyonokSamples/LargeFilesManager/blob/main/Screenshots/FileGeneratorInformationFilledState.png)

3.	Parallel part file creation
-	For i in [0..parallelParts):
-	Compute part file name: <base>.part_{i+1}.<ext> when parallel parts > 1.
-	Open a write stream: FileStream(partPath, Create) + StreamWriter(UTF8 no BOM).
-	Loop until part reaches sizePerFile:
-	Generate [text] using words-only generator:
-	Build alphabetic words separated by single spaces; total length <= maxLineLength.
-	Increment the global LineNumber atomically.
-	Write line with template: "{LineNumber}. {text}".
-	Update progress by actual bytes written: encoding byte count of line + newline.
-	If the next similar write would exceed sizePerFile, write one last line to approximate target and exit.

![image_generate_def](https://github.com/DenisKovalyonokSamples/LargeFilesManager/blob/main/Screenshots/FileGeneratorWritingPartsState.png)
<br><br>

4.	Merge part files into final file
-	Open the final output stream once.
-	Read each part file line-by-line in parallel.
-	Serialize writes to the final writer via a lock.
-	Update progress using accurate byte counts on each merged line.
-	Log merge completion, delete part files, and mark process complete.

![image_generate_def](https://github.com/DenisKovalyonokSamples/LargeFilesManager/blob/main/Screenshots/FileGeneratorMergingPartsState.png)
<br><br>

5.	Process completed state

![image_generate_def](https://github.com/DenisKovalyonokSamples/LargeFilesManager/blob/main/Screenshots/FileGeneratorCompletedState.png)
<br><br>

6. Click "Reset Form" button to start new generation process.
<br><br>

Error handling and logging
-	All major operations (generation, merging, file deletion) log progress and errors using Serilog.
-	Exceptions during merge do not delete part files, enabling retry.

Encoding and counting
-	Use UTF-8 without BOM for both writer and reader.
-	Calculate progress by encoding-aware byte counts (line + newline) to avoid drift.
<br><br>

File Sorter
<br><br>

Purpose: Sorts very large files according to the format’s semantics, using external sorting:
-	Sort by the text portion alphabetically (Ordinal).
-	When texts are equal, sort by the numeric prefix ascending.
<br><br>

Key characteristics:
-	Split-then-merge pipeline (external sorting).
-	Streaming I/O, blocking queues, and parallel consumers.
-	Stable, duplicate-preserving k-way merge.
-	Accurate, thread-safe progress updates.
-	Explicit UTF-8 (no BOM) encoding throughout.
<br><br>

Sorting steps:
<br><br>

1.	Initialization
-	Reset progress panel state and status.
-	Inspect input file size for progress tracking.
-	Compute target part size in bytes using MaxPartFileSizeMegaBytes.
-	Derive bounded capacity for the internal BlockingCollection<PartQueue> to regulate memory/flow.
-	Start consumer tasks (writers) based on TotalConsumerTasks.
<br><br>

2.	Producer: read and split
-	Open the input file with StreamReader (UTF-8 no BOM).
-	Read line-by-line, calculating byte size per line (text + newline).
-	Parse each line to ParsedLine:
-	NumericPrefix: integer portion before . 
-	Text: substring after . 
-	OriginalLine: original text line
-	Accumulate lines until the current part reaches targetPartFileSizeBytes.
-	Sort the current part in-memory using ParsedLineComparer:
-	Compare by Text (Ordinal).
-	If equal, compare by NumericPrefix.
-	Add the sorted lines (OriginalLine) to the blocking collection as a PartQueue.
-	Repeat until EOF; flush remaining lines as the last part.
<br><br>

3.	Consumers: write sorted parts
-	Each consumer:
-	Dequeues PartQueue items.
-	Writes sorted lines to a temporary part file in a unique temp directory.
-	Updates progress by the resulting part file length.
-	Records the part file path and clears queue memory.
<br><br>

4.	Merge sorted part files
-	Initialize readers for each part (UTF-8 no BOM).
-	Build a candidate map: SortedDictionary<ParsedLine, Queue<string>> keyed by the next line from each part (parsed).
-	Queue maintains file paths producing identical parsed lines; this preserves duplicates.
-	Open the final output writer (UTF-8 no BOM).
-	While candidates exist:
-	Pop the smallest parsed line (Text first, then NumericPrefix).
-	Write the OriginalLine to output.
-	Update progress with accurate bytes written.
-	Dequeue the producing file path and read its next line:
-	If available, reinsert into candidates; otherwise, that path is exhausted.
-	Dispose readers, report completion, and delete temp part files.
<br><br>

5.	Error handling and logging
-	Both split and merge stages update ProgressStatus and ProgressValue.
-	Exceptions log context and are allowed to propagate to global handlers (App-level safety net).
<br><br>

Configuration, Logging, and Localization
-	AppSettings (LFM.Core.AppSettings):
-	BufferFileWriteSize (KB), TotalConsumerTasks, MaxPartFileSizeMegaBytes, and UI resources.
-	Logging:
-	Serilog configured in AppStartupHelper.ConfigureLogging(), with console + file sinks.
-	Localization:
-	StringLocalizer provides UI strings and messages.
<br><br>

Progress and UI
-	BaseService (LFM.Core.Services.BaseService) manages:
-	ProgressMinValue, ProgressMaxValue, ProgressValue, ProgressStatus.
-	Dispatcher timer tick to display elapsed time.
-	Thread-safe locks for progress and shared counters (e.g., LineNumberLock).
<br><br>

Best Practices Implemented
-	Streaming I/O with explicit buffer sizes.
-	Accurate encoding-aware byte counting for progress.
-	Deterministic formatting and sorting semantics.
-	Parallelism used where safe (part generation; part writing; merge reads).
-	Thread-safe updates via locks around shared state.
-	Resource cleanup via using and guarded deletes.
-	Error logging with actionable context; failures avoid destructive cleanup when retry is possible.
<br><br>

Running the Apps
<br><br>

File Generator:
-	Choose output folder, file name, target size (B/KB/MB/GB).
-	Start generation; progress bar and logs indicate status.
<br><br>

File Sorter:
-	Choose input file and output file path.
-	Start sorting; the app splits, sorts parts, and merges to output.
-	Final output is sorted by text then number, matching the generator’s semantics.
