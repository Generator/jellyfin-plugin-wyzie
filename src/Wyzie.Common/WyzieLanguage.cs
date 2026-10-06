using System;
using System.Globalization;

namespace Wyzie.Common;

/// <summary>
/// BCP-47 language handling for Wyzie rows and Jellyfin requests.
///
/// Wyzie's wire codes are OpenSubtitles-style two-letter codes, including
/// non-ISO ones (see wyzie-plugins/bazarr/wyzie.py `_REGIONAL`): pb, zt, ea
/// plus aliases sp/ze/zc/iw. Jellyfin only recognizes BCP-47 tags, so every
/// row is canonicalized to BCP-47 for output (file names, tokens, responses)
/// and every request is mapped to an exact wire code. Variants are returned
/// only when explicitly selected: generic requests never include regional rows.
/// </summary>
public static class WyzieLanguage
{
    public const string PortugueseGeneric = "pt";

    public const string PortugueseBrazil = "pt-BR";

    public const string PortuguesePortugal = "pt-PT";

    public const string ChineseGeneric = "zh";

    public const string ChineseTraditional = "zh-Hant";

    public const string ChineseSimplified = "zh-Hans";

    public const string ChineseHongKong = "zh-HK";

    public const string SpanishGeneric = "es";

    public const string SpanishLatin = "es-419";

    public const string Hebrew = "he";

    /// <summary>
    /// Pure: canonical BCP-47 variant of a Wyzie result row, using the
    /// language code first, then display/flag hints, then BCP-47 tags
    /// (AI rows). Unknown codes pass through lowercased.
    /// </summary>
    public static string CanonicalRowVariant(string? language, string? display = null, string? flagUrl = null)
    {
        var tag = NormalizeTag(language);
        switch (tag)
        {
            case "pb":
            case "pob":
                return PortugueseBrazil;
            case "zt":
                return ChineseTraditional;
            case "ze":
                return ChineseGeneric;
            case "zc":
                return ChineseHongKong;
            case "ea":
                return SpanishLatin;
            case "sp":
                return SpanishGeneric;
            case "iw":
                return Hebrew;
        }

        var fromTag = CanonicalTagVariant(tag);
        if (fromTag != null)
        {
            return fromTag;
        }

        if (LooksBrazilian(display, flagUrl))
        {
            return PortugueseBrazil;
        }

        if (string.IsNullOrWhiteSpace(tag))
        {
            return "und";
        }

        return tag;
    }

    /// <summary>
    /// Pure: canonical BCP-47 variant of a Jellyfin search request, from its
    /// Language and TwoLetterISOLanguageName fields. Unknown values pass
    /// through lowercased.
    /// </summary>
    public static string CanonicalRequestVariant(string? language, string? twoLetter)
    {
        var tag = NormalizeTag(!string.IsNullOrWhiteSpace(language) ? language : twoLetter);
        switch (tag)
        {
            case "pob":
            case "pb":
                return PortugueseBrazil;
            case "por":
                return PortugueseGeneric;
            case "zt":
                return ChineseTraditional;
            case "ze":
                return ChineseGeneric;
            case "zc":
                return ChineseHongKong;
            case "ea":
                return SpanishLatin;
            case "sp":
                return SpanishGeneric;
            case "iw":
                return Hebrew;
        }

        var fromTag = CanonicalTagVariant(tag);
        if (fromTag != null)
        {
            return fromTag;
        }

        if (string.IsNullOrWhiteSpace(tag))
        {
            return "und";
        }

        return tag;
    }

    /// <summary>
    /// Pure: exact Wyzie wire code for a canonical request variant.
    /// Regional selections use their exact codes (pt-BR to pb); everything
    /// else goes out as strict two-letter codes. Null means unfiltered.
    /// </summary>
    public static string? ToWyzieRequestCode(string canonicalVariant)
    {
        switch (LowerInvariant(canonicalVariant))
        {
            case "pt-br":
                return "pb";
            case "pt-pt":
            case "pt":
            case "por":
            case "pob":
                return "pt";
            case "zh-hant":
            case "zh-tw":
                return "zt";
            case "zh-hans":
            case "zh-cn":
            case "zh-hk":
            case "zh":
            case "zho":
            case "ze":
            case "zc":
            case "yue":
                return "zh";
            case "es-419":
            case "es-mx":
                return "ea";
            case "es-es":
            case "es":
            case "spa":
            case "sp":
                return "es";
            case "he":
            case "heb":
            case "iw":
                return "he";
            default:
                var tag = NormalizeTag(canonicalVariant);
                return tag.Length == 2 && IsAlpha(tag) ? tag : null;
        }
    }

    /// <summary>
    /// Pure: strict variant match. A row is offered only for its exact
    /// variant; generic requests never match regional rows and vice versa.
    /// Exception: explicitly-European requests also match generic rows of the
    /// same family (pt-PT matches pt), since generic rows are the
    /// European-compatible content and no European-specific wire rows exist.
    /// </summary>
    public static bool VariantMatches(string rowVariant, string requestVariant)
    {
        if (string.Equals(
            rowVariant?.Trim(),
            requestVariant?.Trim(),
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var row = LowerInvariant(rowVariant);
        var request = LowerInvariant(requestVariant);
        if (request == "pt-pt")
        {
            return row == "pt";
        }

        return false;
    }

    /// <summary>
    /// Pure: 3-letter code for RemoteSubtitleInfo.ThreeLetterISOLanguageName.
    /// </summary>
    public static string ToThreeLetter(string? canonicalVariant)
    {
        if (string.IsNullOrWhiteSpace(canonicalVariant))
        {
            return "und";
        }

        switch (LowerInvariant(canonicalVariant))
        {
            case "pt":
            case "pt-br":
            case "pt-pt":
            case "por":
            case "pob":
            case "pb":
                return "por";
            case "zh":
            case "zh-hant":
            case "zh-hans":
            case "zh-hk":
            case "zh-tw":
            case "zh-cn":
            case "zho":
            case "zt":
            case "ze":
            case "zc":
                return "zho";
            case "es":
            case "es-419":
            case "es-mx":
            case "es-es":
            case "spa":
            case "ea":
            case "sp":
                return "spa";
            case "he":
            case "heb":
            case "iw":
                return "heb";
        }

        try
        {
            var ci = new CultureInfo(canonicalVariant!.Trim());
            var threeLetter = ci.ThreeLetterISOLanguageName;
            return string.IsNullOrEmpty(threeLetter)
                ? canonicalVariant!.Trim().ToLowerInvariant()
                : threeLetter.ToLowerInvariant();
        }
        catch (CultureNotFoundException)
        {
            return canonicalVariant!.Trim().ToLowerInvariant();
        }
    }

    /// <summary>
    /// Pure: true when the subtitle is Brazilian Portuguese, by code, tag or hint.
    /// </summary>
    public static bool IsBrazilianPortuguese(string? language, string? display = null, string? flagUrl = null)
    {
        return string.Equals(CanonicalRowVariant(language, display, flagUrl), PortugueseBrazil, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Back-compat normalization: pb (and hints) to pt-BR, else lowercased input.
    /// </summary>
    public static string Normalize(string? language, string? display = null, string? flagUrl = null)
    {
        return CanonicalRowVariant(language, display, flagUrl);
    }

    private static string? CanonicalTagVariant(string tag)
    {
        var dash = tag.IndexOf('-');
        var baseCode = dash < 0 ? tag : tag.Substring(0, dash);
        var sub = dash < 0 ? string.Empty : tag.Substring(dash + 1);
        var subs = sub.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);

        switch (baseCode)
        {
            case "pt":
                foreach (var s in subs)
                {
                    if (string.Equals(s, "br", StringComparison.OrdinalIgnoreCase)) return PortugueseBrazil;
                    if (string.Equals(s, "pt", StringComparison.OrdinalIgnoreCase)) return PortuguesePortugal;
                }

                return subs.Length == 0 ? PortugueseGeneric : null;
            case "por":
                return subs.Length == 0 ? PortugueseGeneric : null;
            case "zh":
            case "zho":
            case "chi":
                foreach (var s in subs)
                {
                    if (string.Equals(s, "hant", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(s, "tw", StringComparison.OrdinalIgnoreCase)) return ChineseTraditional;
                    if (string.Equals(s, "hans", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(s, "cn", StringComparison.OrdinalIgnoreCase)) return ChineseSimplified;
                    if (string.Equals(s, "hk", StringComparison.OrdinalIgnoreCase)) return ChineseHongKong;
                }

                return subs.Length == 0 ? ChineseGeneric : null;
            case "es":
            case "spa":
                foreach (var s in subs)
                {
                    if (string.Equals(s, "419", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(s, "mx", StringComparison.OrdinalIgnoreCase)) return SpanishLatin;
                    if (string.Equals(s, "es", StringComparison.OrdinalIgnoreCase)) return SpanishGeneric;
                }

                return subs.Length == 0 ? SpanishGeneric : null;
            case "he":
            case "heb":
                return subs.Length == 0 ? Hebrew : null;
            case "yue":
                return ChineseHongKong;
        }

        return null;
    }

    private static bool LooksBrazilian(string? display, string? flagUrl)
    {
        if (!string.IsNullOrEmpty(display)
            && display!.IndexOf("(BR)", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(display)
            && display!.IndexOf("Brazil", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(flagUrl)
            && flagUrl!.IndexOf("/BR/", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return false;
    }

    private static string NormalizeTag(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant().Replace('_', '-');
    }

    private static string LowerInvariant(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static bool IsAlpha(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')))
            {
                return false;
            }
        }

        return true;
    }
}
