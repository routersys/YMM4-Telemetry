using System.Text.RegularExpressions;

namespace Telemetry;

internal static partial class TextScrubber
{
    private const int MinimumReplaceableLength = 3;

    private const string UserPlaceholder = "[user]";
    private const string UserProfilePlaceholder = "[userprofile]";
    private const string AppDataPlaceholder = "[appdata]";
    private const string LocalAppDataPlaceholder = "[localappdata]";
    private const string DomainPlaceholder = "[domain]";
    private const string MachinePlaceholder = "[machine]";
    private const string MailPlaceholder = "[mail]";
    private const string AddressPlaceholder = "[address]";

    private static readonly Lazy<IReadOnlyList<KeyValuePair<string, string>>> LiteralReplacements =
        new(BuildLiteralReplacements, true);

    public static string? Scrub(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var result = value;

        foreach (var (literal, placeholder) in LiteralReplacements.Value)
        {
            if (result.Contains(literal, StringComparison.OrdinalIgnoreCase))
            {
                result = result.Replace(literal, placeholder, StringComparison.OrdinalIgnoreCase);
            }
        }

        result = WindowsUserDirectory().Replace(result, "$1:\\Users\\" + UserPlaceholder);
        result = UnixHomeDirectory().Replace(result, "/home/" + UserPlaceholder);
        result = MailAddress().Replace(result, MailPlaceholder);
        result = InternetAddress().Replace(result, AddressPlaceholder);

        return result;
    }

    private static IReadOnlyList<KeyValuePair<string, string>> BuildLiteralReplacements() => SelectReplacements(
    [
        new KeyValuePair<string?, string>(ReadFolder(Environment.SpecialFolder.UserProfile), UserProfilePlaceholder),
        new KeyValuePair<string?, string>(ReadVariable("USERPROFILE"), UserProfilePlaceholder),
        new KeyValuePair<string?, string>(ReadFolder(Environment.SpecialFolder.ApplicationData), AppDataPlaceholder),
        new KeyValuePair<string?, string>(ReadFolder(Environment.SpecialFolder.LocalApplicationData), LocalAppDataPlaceholder),
        new KeyValuePair<string?, string>(ReadVariable("USERDOMAIN"), DomainPlaceholder),
        new KeyValuePair<string?, string>(Read(static () => Environment.UserName), UserPlaceholder),
        new KeyValuePair<string?, string>(Read(static () => Environment.MachineName), MachinePlaceholder),
    ]);

    internal static IReadOnlyList<KeyValuePair<string, string>> SelectReplacements(
        IReadOnlyList<KeyValuePair<string?, string>> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var replacements = new List<KeyValuePair<string, string>>(candidates.Count);

        foreach (var (literal, placeholder) in candidates)
        {
            if (literal is null
                || literal.Length < MinimumReplaceableLength
                || replacements.Exists(existing => string.Equals(existing.Key, literal, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            replacements.Add(new KeyValuePair<string, string>(literal, placeholder));
        }

        replacements.Sort(static (left, right) => right.Key.Length.CompareTo(left.Key.Length));
        return replacements;
    }

    private static string? ReadFolder(Environment.SpecialFolder folder) =>
        Read(() => Environment.GetFolderPath(folder, Environment.SpecialFolderOption.DoNotVerify));

    private static string? ReadVariable(string name) => Read(() => Environment.GetEnvironmentVariable(name));

    private static string? Read(Func<string?> reader)
    {
        try
        {
            var value = reader();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (Exception)
        {
            return null;
        }
    }

    [GeneratedRegex(@"([A-Za-z]):\\Users\\[^\\/:*?""<>|\r\n]+", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsUserDirectory();

    [GeneratedRegex(@"/home/[^/\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex UnixHomeDirectory();

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex MailAddress();

    [GeneratedRegex(@"\b(?:(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\.){3}(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\b", RegexOptions.CultureInvariant)]
    private static partial Regex InternetAddress();
}
