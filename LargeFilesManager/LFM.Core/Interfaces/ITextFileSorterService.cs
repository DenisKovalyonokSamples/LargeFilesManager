namespace LFM.Core.Interfaces
{
    public interface ITextFileSorterService : IBaseService
    {
        /// <summary>
        /// Sort a text file with this line format <number>. <string>
        /// </summary>
        /// <param name="inputFileTextPath"></param>
        /// <param name="outputFileTextPath"></param>
        /// <returns></returns>
        Task SortTextFile(string inputFileTextPath, string outputFileTextPath);
    }
}
