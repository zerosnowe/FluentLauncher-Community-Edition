using FluentLauncher.Infra.LocalizedStrings;
using Microsoft.Windows.Globalization;
using Natsurainko.FluentLauncher.Models;
using Natsurainko.FluentLauncher.Services.Settings;
using System.Collections.Generic;

namespace Natsurainko.FluentLauncher.Utils;

[GeneratedLocalizedStrings]
static partial class LocalizedStrings
{
    public static List<LanguageInfo> SupportedLanguages = [
        new LanguageInfo("en-US", "English"),
        new LanguageInfo("ru-RU", "Русский"),
        new LanguageInfo("uk-UA", "Український"),
        new LanguageInfo("zh-Hans", "简体中文"),
        new LanguageInfo("zh-Hant", "繁體中文")
    ];

    /// <summary>
    /// Get a localized string in Resources.resw
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static string GetString(string key)
    {
        try
        {
            return s_resourceMap.GetValue($"Resources/{key}").ValueAsString;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // NamedResource not found. Return a sensible fallback instead of letting the app crash.
            // If key follows the pattern "...__text", return the trailing text; otherwise return the raw key.
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            int idx = key.LastIndexOf("__");
            if (idx >= 0 && idx + 2 < key.Length)
                return key.Substring(idx + 2);

            return key;
        }
        catch
        {
            // Any other unexpected error: return key as fallback
            return key ?? string.Empty;
        }
    }

    public static string[] GetStrings(string key)
    {
        var s = GetString(key);
        return string.IsNullOrEmpty(s) ? new string[0] : s.Split(";");
    }

    public static void ApplyLanguage(string language)
    {
        ApplicationLanguages.PrimaryLanguageOverride = language.Split(',')[0];
        App.GetService<SettingsService>().CurrentLanguage = language;
    }
}
