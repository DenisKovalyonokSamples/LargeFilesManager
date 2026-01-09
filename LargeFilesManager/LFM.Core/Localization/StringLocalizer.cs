using Microsoft.Extensions.Localization;
using System.Globalization;

namespace LFM.Core.Localization
{
    // Minimal IStringLocalizer for tests: returns the key as the localized value.
    public class StringLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new LocalizedString(name, name, resourceNotFound: true);

        public LocalizedString this[string name, params object[] arguments] => new LocalizedString(name, string.Format(CultureInfo.InvariantCulture, name, arguments), resourceNotFound: true);


        private readonly Dictionary<string, string> _map;

        public StringLocalizer(Dictionary<string, string>? map = null)
        {
            _map = map ?? new Dictionary<string, string>();
        }

        private string Resolve(string name)
        {
            // Provide substring matching because tests don't rely on exact TranslationConstant values.
            var match = _map.FirstOrDefault(kv => name.Contains(kv.Key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(match.Value))
                return match.Value;

            // Fallback: return name itself (safe for messages), or a simple format for indexed usages.
            return name;
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }
}
