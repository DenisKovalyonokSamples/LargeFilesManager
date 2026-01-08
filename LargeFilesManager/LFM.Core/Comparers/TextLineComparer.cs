namespace LFM.Core.Comparers
{
    public class TextLineComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            var a = ParseLine(x);
            var b = ParseLine(y);

            int stringCompare = string.Compare(a.Item2, b.Item2, StringComparison.Ordinal);
            return stringCompare != 0 ? stringCompare : a.Item1.CompareTo(b.Item1);
        }

        private (int, string) ParseLine(string line)
        {
            var parts = line.Split(new[] { ". " }, 2, StringSplitOptions.RemoveEmptyEntries);
            return (int.Parse(parts[0]), parts[1]);
        }
    }
}
