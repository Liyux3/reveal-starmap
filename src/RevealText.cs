using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;

namespace RevealStarMap;

internal static class RevealText
{
    private static readonly Dictionary<string, string> English = ReadEnglish();
    private static readonly Dictionary<string, Dictionary<string, string>> Catalogs =
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    internal static string Get(string key) => GetForLanguage(key,
        ResolveLanguage(RevealStarMapSettingsManager.Current.Language, Localization.GetLocale()?.Code));

    internal static string Format(string key, params object[] values)
    {
        try { return string.Format(CultureInfo.CurrentCulture, Get(key), values); }
        catch (FormatException) { return string.Format(CultureInfo.CurrentCulture, English[key], values); }
    }

    internal static string ResolveLanguage(UiLanguage preference, string? gameCode)
    {
        if (preference == UiLanguage.English) return "en";
        if (preference == UiLanguage.SimplifiedChinese) return "zh-CN";
        if (preference == UiLanguage.TraditionalChinese) return "zh-TW";
        string code = (gameCode ?? "en").Replace('_', '-').ToLowerInvariant();
        foreach (char c in code)
            if (!char.IsLetterOrDigit(c) && c != '-') return "en";
        if (code == "zh" || code.StartsWith("zh-", StringComparison.Ordinal))
            return code.Contains("tw") || code.Contains("hk") || code.Contains("mo") || code.Contains("hant")
                ? "zh-TW" : "zh-CN";
        return string.IsNullOrEmpty(code) ? "en" : code;
    }

    internal static string GetForLanguage(string key, string code)
    {
        if (!English.TryGetValue(key, out string fallback)) return RevealStarMapMod.ModName;
        if (code == "en") return fallback;
        if (!Catalogs.TryGetValue(code, out var catalog))
        {
            catalog = LoadCatalog(code);
            Catalogs[code] = catalog;
        }
        return catalog.TryGetValue(key, out string text) && !string.IsNullOrWhiteSpace(text) ? text : fallback;
    }

    private static Dictionary<string, string> ReadEnglish()
    {
        using var stream = typeof(RevealText).Assembly.GetManifestResourceStream("RevealStarMap.en.json");
        using var reader = new StreamReader(stream!);
        return JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd())!;
    }

    private static Dictionary<string, string> LoadCatalog(string code)
    {
        try
        {
            string directory = Path.Combine(Path.GetDirectoryName(typeof(RevealText).Assembly.Location)!, "translations");
            string path = Path.Combine(directory, code + ".json");
            if (!File.Exists(path)) path = Path.Combine(directory, code.Split('-')[0] + ".json");
            if (File.Exists(path))
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path)) ?? English;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (JsonException) { }
        return English;
    }
}
