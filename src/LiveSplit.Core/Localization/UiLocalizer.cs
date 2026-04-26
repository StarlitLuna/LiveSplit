namespace LiveSplit.Localization;

/// <summary>
/// String-translation lookup. The original WinForms-targeted version also walked
/// <c>Form</c>/<c>Control</c> hierarchies and applied translations to inherited
/// <c>Text</c>/<c>HeaderText</c> properties. The Avalonia front-end binds via XAML
/// resources, so the only surviving members are the lookup helpers (<see cref="Translate(string)"/>,
/// <see cref="TranslateKey(string, string)"/>) used by component code.
/// </summary>
public static class UiLocalizer
{
    public static string Translate(string source)
    {
        return Translate(source, LanguageResolver.ResolveCurrentCultureLanguage());
    }

    public static string Translate(string source, AppLanguage language)
    {
        if (string.IsNullOrEmpty(source) || language == null || !language.RequiresLocalization)
        {
            return source;
        }

        return UiTextCatalog.TryGetTranslation(source, language, out string translated)
            ? translated
            : source;
    }

    public static string TranslateKey(string key, string fallback)
    {
        return TranslateKey(key, fallback, LanguageResolver.ResolveCurrentCultureLanguage());
    }

    public static string TranslateKey(string key, string fallback, AppLanguage language)
    {
        if (UiTextCatalog.TryGetKeyTranslation(language, key, out string translated))
        {
            return translated;
        }

        return Translate(fallback, language);
    }
}
