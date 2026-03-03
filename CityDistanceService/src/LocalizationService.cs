using System.Text.Json;
using System.Text.Json.Serialization;

public class LocalizationService : ILocalizationService
{
    private readonly Dictionary<string, Dictionary<string, string>> _translations;
    private readonly Dictionary<string, LanguageMetadata>           _metadata;
    private readonly string _defaultLang;

    public IReadOnlyList<LanguageMetadata> AvailableLanguages => _metadata.Values.ToList();

    public LocalizationService(string resourcesPath, string defaultLang = "en")
    {
        _defaultLang  = defaultLang;
        _translations = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        _metadata     = new Dictionary<string, LanguageMetadata>(StringComparer.OrdinalIgnoreCase);

        LoadMetadata(resourcesPath);
        LoadTranslations(resourcesPath);
    }

    // ── Loading ───────────────────────────────────────────────────────────────

    private void LoadMetadata(string path)
    {
        var metaFile = Path.Combine(path, "languages.json");
        if (!File.Exists(metaFile))
        {
            Console.WriteLine($"[Localization] languages.json not found at {metaFile}");
            return;
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var entries = JsonSerializer.Deserialize<List<LanguageMetadata>>(
            File.ReadAllText(metaFile), options) ?? [];

        foreach (var entry in entries)
            _metadata[entry.Code] = entry;

        Console.WriteLine($"[Localization] Loaded metadata for {_metadata.Count} locale(s).");
    }

    private void LoadTranslations(string path)
    {
        if (!Directory.Exists(path)) return;

        foreach (var file in Directory.GetFiles(path, "*.json"))
        {
            var lang = Path.GetFileNameWithoutExtension(file);
            if (lang.Equals("languages", StringComparison.OrdinalIgnoreCase))
                continue; // skip the metadata file itself

            try
            {
                var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    File.ReadAllText(file));
                if (entries != null)
                    _translations[lang] = entries;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Localization] Failed to load {file}: {ex.Message}");
            }
        }

        Console.WriteLine($"[Localization] Loaded translations for: " +
                          string.Join(", ", _translations.Keys));
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public string Get(string key, string lang)
    {
        // Try exact locale ("en-US"), then base language ("en"), then default
        foreach (var candidate in Candidates(lang))
        {
            if (_translations.TryGetValue(candidate, out var dict) &&
                dict.TryGetValue(key, out var val))
                return val;
        }
        return key; // Last resort
    }

    public bool IsImperial(string lang)
    {
        foreach (var candidate in Candidates(lang))
        {
            if (_metadata.TryGetValue(candidate, out var meta))
                return meta.UseImperial;
        }
        return false;
    }

    public DistanceResult FormatDistance(double distanceKm, string lang)
    {
        bool imperial     = IsImperial(lang);
        double value      = imperial ? Math.Round(distanceKm * 0.621371, 2)
                                     : Math.Round(distanceKm, 2);
        string unit       = Get(imperial ? MsgKey.UnitMiles : MsgKey.UnitKm, lang);
        return new DistanceResult(value, unit);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Yields candidates in priority order: "en-US" → "en" → default lang
    private IEnumerable<string> Candidates(string lang)
    {
        if (!string.IsNullOrEmpty(lang))
        {
            yield return lang;              // exact:   "en-US"
            var parts = lang.Split('-');
            if (parts.Length > 1)
                yield return parts[0];      // base:    "en"
        }
        yield return _defaultLang;          // default: "en"
    }
}