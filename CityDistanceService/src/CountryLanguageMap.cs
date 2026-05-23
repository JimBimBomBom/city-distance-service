using System.Collections.Generic;

/// <summary>
/// Maps ISO-3166-1 alpha-2 country codes to the primary language code used to
/// key per-language fields on <see cref="CityDoc"/> (e.g. <c>CityNames</c>,
/// <c>Country</c>, <c>AdminRegion</c>). One language per country.
/// </summary>
public static class CountryLanguageMap
{
    private static readonly Dictionary<string, string> Map = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ["AD"] = "ca", // Andorra
        ["AE"] = "ar", // United Arab Emirates
        ["AF"] = "ps", // Afghanistan
        ["AG"] = "en", // Antigua and Barbuda
        ["AI"] = "en", // Anguilla
        ["AL"] = "sq", // Albania
        ["AM"] = "hy", // Armenia
        ["AO"] = "pt", // Angola
        ["AQ"] = "en", // Antarctica
        ["AR"] = "es", // Argentina
        ["AS"] = "en", // American Samoa
        ["AT"] = "de", // Austria
        ["AU"] = "en", // Australia
        ["AW"] = "nl", // Aruba
        ["AX"] = "sv", // Aland Islands
        ["AZ"] = "az", // Azerbaijan
        ["BA"] = "bs", // Bosnia and Herzegovina
        ["BB"] = "en", // Barbados
        ["BD"] = "bn", // Bangladesh
        ["BE"] = "nl", // Belgium
        ["BF"] = "fr", // Burkina Faso
        ["BG"] = "bg", // Bulgaria
        ["BH"] = "ar", // Bahrain
        ["BI"] = "fr", // Burundi
        ["BJ"] = "fr", // Benin
        ["BL"] = "fr", // Saint Barthelemy
        ["BM"] = "en", // Bermuda
        ["BN"] = "ms", // Brunei
        ["BO"] = "es", // Bolivia
        ["BQ"] = "nl", // Bonaire, Sint Eustatius and Saba
        ["BR"] = "pt", // Brazil
        ["BS"] = "en", // Bahamas
        ["BT"] = "dz", // Bhutan
        ["BV"] = "no", // Bouvet Island
        ["BW"] = "en", // Botswana
        ["BY"] = "be", // Belarus
        ["BZ"] = "en", // Belize
        ["CA"] = "en", // Canada
        ["CC"] = "en", // Cocos (Keeling) Islands
        ["CD"] = "fr", // DR Congo
        ["CF"] = "fr", // Central African Republic
        ["CG"] = "fr", // Congo
        ["CH"] = "de", // Switzerland
        ["CI"] = "fr", // Cote d'Ivoire
        ["CK"] = "en", // Cook Islands
        ["CL"] = "es", // Chile
        ["CM"] = "fr", // Cameroon
        ["CN"] = "zh", // China
        ["CO"] = "es", // Colombia
        ["CR"] = "es", // Costa Rica
        ["CU"] = "es", // Cuba
        ["CV"] = "pt", // Cape Verde
        ["CW"] = "nl", // Curacao
        ["CX"] = "en", // Christmas Island
        ["CY"] = "el", // Cyprus
        ["CZ"] = "cs", // Czech Republic
        ["DE"] = "de", // Germany
        ["DJ"] = "fr", // Djibouti
        ["DK"] = "da", // Denmark
        ["DM"] = "en", // Dominica
        ["DO"] = "es", // Dominican Republic
        ["DZ"] = "ar", // Algeria
        ["EC"] = "es", // Ecuador
        ["EE"] = "et", // Estonia
        ["EG"] = "ar", // Egypt
        ["EH"] = "ar", // Western Sahara
        ["ER"] = "ti", // Eritrea
        ["ES"] = "es", // Spain
        ["ET"] = "am", // Ethiopia
        ["FI"] = "fi", // Finland
        ["FJ"] = "en", // Fiji
        ["FK"] = "en", // Falkland Islands
        ["FM"] = "en", // Micronesia
        ["FO"] = "fo", // Faroe Islands
        ["FR"] = "fr", // France
        ["GA"] = "fr", // Gabon
        ["GB"] = "en", // United Kingdom
        ["GD"] = "en", // Grenada
        ["GE"] = "ka", // Georgia
        ["GF"] = "fr", // French Guiana
        ["GG"] = "en", // Guernsey
        ["GH"] = "en", // Ghana
        ["GI"] = "en", // Gibraltar
        ["GL"] = "kl", // Greenland
        ["GM"] = "en", // Gambia
        ["GN"] = "fr", // Guinea
        ["GP"] = "fr", // Guadeloupe
        ["GQ"] = "es", // Equatorial Guinea
        ["GR"] = "el", // Greece
        ["GS"] = "en", // South Georgia
        ["GT"] = "es", // Guatemala
        ["GU"] = "en", // Guam
        ["GW"] = "pt", // Guinea-Bissau
        ["GY"] = "en", // Guyana
        ["HK"] = "zh", // Hong Kong
        ["HM"] = "en", // Heard Island
        ["HN"] = "es", // Honduras
        ["HR"] = "hr", // Croatia
        ["HT"] = "fr", // Haiti
        ["HU"] = "hu", // Hungary
        ["ID"] = "id", // Indonesia
        ["IE"] = "en", // Ireland
        ["IL"] = "he", // Israel
        ["IM"] = "en", // Isle of Man
        ["IN"] = "hi", // India
        ["IO"] = "en", // British Indian Ocean Territory
        ["IQ"] = "ar", // Iraq
        ["IR"] = "fa", // Iran
        ["IS"] = "is", // Iceland
        ["IT"] = "it", // Italy
        ["JE"] = "en", // Jersey
        ["JM"] = "en", // Jamaica
        ["JO"] = "ar", // Jordan
        ["JP"] = "ja", // Japan
        ["KE"] = "sw", // Kenya
        ["KG"] = "ky", // Kyrgyzstan
        ["KH"] = "km", // Cambodia
        ["KI"] = "en", // Kiribati
        ["KM"] = "ar", // Comoros
        ["KN"] = "en", // Saint Kitts and Nevis
        ["KP"] = "ko", // North Korea
        ["KR"] = "ko", // South Korea
        ["KW"] = "ar", // Kuwait
        ["KY"] = "en", // Cayman Islands
        ["KZ"] = "kk", // Kazakhstan
        ["LA"] = "lo", // Laos
        ["LB"] = "ar", // Lebanon
        ["LC"] = "en", // Saint Lucia
        ["LI"] = "de", // Liechtenstein
        ["LK"] = "si", // Sri Lanka
        ["LR"] = "en", // Liberia
        ["LS"] = "en", // Lesotho
        ["LT"] = "lt", // Lithuania
        ["LU"] = "fr", // Luxembourg
        ["LV"] = "lv", // Latvia
        ["LY"] = "ar", // Libya
        ["MA"] = "ar", // Morocco
        ["MC"] = "fr", // Monaco
        ["MD"] = "ro", // Moldova
        ["ME"] = "sr", // Montenegro
        ["MF"] = "fr", // Saint Martin
        ["MG"] = "mg", // Madagascar
        ["MH"] = "en", // Marshall Islands
        ["MK"] = "mk", // North Macedonia
        ["ML"] = "fr", // Mali
        ["MM"] = "my", // Myanmar
        ["MN"] = "mn", // Mongolia
        ["MO"] = "zh", // Macao
        ["MP"] = "en", // Northern Mariana Islands
        ["MQ"] = "fr", // Martinique
        ["MR"] = "ar", // Mauritania
        ["MS"] = "en", // Montserrat
        ["MT"] = "mt", // Malta
        ["MU"] = "en", // Mauritius
        ["MV"] = "dv", // Maldives
        ["MW"] = "en", // Malawi
        ["MX"] = "es", // Mexico
        ["MY"] = "ms", // Malaysia
        ["MZ"] = "pt", // Mozambique
        ["NA"] = "en", // Namibia
        ["NC"] = "fr", // New Caledonia
        ["NE"] = "fr", // Niger
        ["NF"] = "en", // Norfolk Island
        ["NG"] = "en", // Nigeria
        ["NI"] = "es", // Nicaragua
        ["NL"] = "nl", // Netherlands
        ["NO"] = "no", // Norway
        ["NP"] = "ne", // Nepal
        ["NR"] = "en", // Nauru
        ["NU"] = "en", // Niue
        ["NZ"] = "en", // New Zealand
        ["OM"] = "ar", // Oman
        ["PA"] = "es", // Panama
        ["PE"] = "es", // Peru
        ["PF"] = "fr", // French Polynesia
        ["PG"] = "en", // Papua New Guinea
        ["PH"] = "en", // Philippines
        ["PK"] = "ur", // Pakistan
        ["PL"] = "pl", // Poland
        ["PM"] = "fr", // Saint Pierre and Miquelon
        ["PN"] = "en", // Pitcairn
        ["PR"] = "es", // Puerto Rico
        ["PS"] = "ar", // Palestine
        ["PT"] = "pt", // Portugal
        ["PW"] = "en", // Palau
        ["PY"] = "es", // Paraguay
        ["QA"] = "ar", // Qatar
        ["RE"] = "fr", // Reunion
        ["RO"] = "ro", // Romania
        ["RS"] = "sr", // Serbia
        ["RU"] = "ru", // Russia
        ["RW"] = "rw", // Rwanda
        ["SA"] = "ar", // Saudi Arabia
        ["SB"] = "en", // Solomon Islands
        ["SC"] = "fr", // Seychelles
        ["SD"] = "ar", // Sudan
        ["SE"] = "sv", // Sweden
        ["SG"] = "en", // Singapore
        ["SH"] = "en", // Saint Helena
        ["SI"] = "sl", // Slovenia
        ["SJ"] = "no", // Svalbard and Jan Mayen
        ["SK"] = "sk", // Slovakia
        ["SL"] = "en", // Sierra Leone
        ["SM"] = "it", // San Marino
        ["SN"] = "fr", // Senegal
        ["SO"] = "so", // Somalia
        ["SR"] = "nl", // Suriname
        ["SS"] = "en", // South Sudan
        ["ST"] = "pt", // Sao Tome and Principe
        ["SV"] = "es", // El Salvador
        ["SX"] = "nl", // Sint Maarten
        ["SY"] = "ar", // Syria
        ["SZ"] = "en", // Eswatini
        ["TC"] = "en", // Turks and Caicos Islands
        ["TD"] = "fr", // Chad
        ["TF"] = "fr", // French Southern Territories
        ["TG"] = "fr", // Togo
        ["TH"] = "th", // Thailand
        ["TJ"] = "tg", // Tajikistan
        ["TK"] = "en", // Tokelau
        ["TL"] = "pt", // Timor-Leste
        ["TM"] = "tk", // Turkmenistan
        ["TN"] = "ar", // Tunisia
        ["TO"] = "en", // Tonga
        ["TR"] = "tr", // Turkey
        ["TT"] = "en", // Trinidad and Tobago
        ["TV"] = "en", // Tuvalu
        ["TW"] = "zh", // Taiwan
        ["TZ"] = "sw", // Tanzania
        ["UA"] = "uk", // Ukraine
        ["UG"] = "en", // Uganda
        ["UM"] = "en", // U.S. Minor Outlying Islands
        ["US"] = "en", // United States
        ["UY"] = "es", // Uruguay
        ["UZ"] = "uz", // Uzbekistan
        ["VA"] = "it", // Vatican City
        ["VC"] = "en", // Saint Vincent and the Grenadines
        ["VE"] = "es", // Venezuela
        ["VG"] = "en", // British Virgin Islands
        ["VI"] = "en", // U.S. Virgin Islands
        ["VN"] = "vi", // Vietnam
        ["VU"] = "bi", // Vanuatu
        ["WF"] = "fr", // Wallis and Futuna
        ["WS"] = "en", // Samoa
        ["XK"] = "sq", // Kosovo
        ["YE"] = "ar", // Yemen
        ["YT"] = "fr", // Mayotte
        ["ZA"] = "en", // South Africa
        ["ZM"] = "en", // Zambia
        ["ZW"] = "en", // Zimbabwe
    };

    /// <summary>
    /// Returns the primary language code for the given ISO-3166-1 alpha-2
    /// country code, or <c>null</c> if the country is unknown.
    /// </summary>
    public static string? Get(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) return null;
        return Map.TryGetValue(countryCode, out var lang) ? lang : null;
    }

    /// <summary>
    /// Resolves a localized value from a per-language dictionary using the
    /// fallback chain: requested user language -> country's primary language
    /// (derived from <paramref name="countryCode"/>) -> default language
    /// (typically "en"). Returns <c>null</c> if none of the candidates are
    /// present in the dictionary.
    /// </summary>
    public static string? ResolveLocalized(
        Dictionary<string, string>? values,
        string? userLanguage,
        string? countryCode,
        string defaultLanguage = "en")
    {
        if (values == null || values.Count == 0) return null;

        if (!string.IsNullOrWhiteSpace(userLanguage)
            && values.TryGetValue(userLanguage, out var v) && !string.IsNullOrEmpty(v))
            return v;

        var countryLang = Get(countryCode);
        if (!string.IsNullOrWhiteSpace(countryLang)
            && values.TryGetValue(countryLang, out var cv) && !string.IsNullOrEmpty(cv))
            return cv;

        if (!string.IsNullOrWhiteSpace(defaultLanguage)
            && values.TryGetValue(defaultLanguage, out var dv) && !string.IsNullOrEmpty(dv))
            return dv;

        return null;
    }
}
