namespace LFM.Core.Comparers
{
    public class TextLineComparer : IComparer<string>
    {
        /// <summary>
        /// Compares two lines in the format "<number>. <text>".
        /// Sort by the text part (alphabetically), then by the numeric prefix (ascending) when texts are equal.
        /// </summary>
        public int Compare(string? leftLine, string? rightLine)
        {
            var left = ParseLine(leftLine);
            var right = ParseLine(rightLine);

            int textComparison = string.Compare(left.textPart, right.textPart, StringComparison.Ordinal);
            if (textComparison != 0) return textComparison;

            return left.numericPrefix.CompareTo(right.numericPrefix);
        }

        private (int numericPrefix, string textPart) ParseLine(string? inputLine)
        {
            if (string.IsNullOrWhiteSpace(inputLine))
                return (0, string.Empty);

            var parts = inputLine.Split(new[] { ". " }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return (0, inputLine);

            // If parsing the number fails, treat it as 0 to keep a stable, deterministic order.
            int prefix = int.TryParse(parts[0], out var n) ? n : 0;
            return (prefix, parts[1]);
        }
    }
}
