public interface ILocalizationService
{
    string                          Get(string key, string lang);
    bool                            IsImperial(string lang);
    DistanceResult                  FormatDistance(double distanceKm, string lang);
    IReadOnlyList<LanguageMetadata> AvailableLanguages { get; }
}

public class LanguageMetadata
{
    public string Code        { get; set; } = "";
    public string Name        { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public bool UseImperial   { get; set; } = false;

    // Computed — same flag logic as CitySuggestion
    public string Flag => CountryCode.Length == 2
        ? string.Concat(CountryCode.ToUpper().Select(c => char.ConvertFromUtf32(c + 0x1F1A5)))
        : "";
}

// MessageKeys.cs — single source of truth for key names
public static class MsgKey
{
    public const string DistanceCalculated  = "distance_calculated";
    public const string CityNotFound        = "city_not_found";
    public const string InvalidQuery        = "invalid_query";
    public const string NoSuggestions       = "no_suggestions";
    public const string SuggestionsFound    = "suggestions_found";
    public const string CityFound           = "city_found";
    public const string CityAdded          = "city_added";
    public const string CityUpdated         = "city_updated";
    public const string CityDeleted         = "city_deleted";
    public const string CityAlreadyExists   = "city_already_exists";
    public const string UnitKm              = "unit_km";
    public const string UnitMiles           = "unit_miles";
    public const string DistanceFormat      = "distance_format";
    public const string InternalError       = "internal_error";
}
