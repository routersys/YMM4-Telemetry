using System.Reflection;

namespace Telemetry;

internal static class PluginIdentity
{
    private const string UnknownName = "unknown";
    private const string UnknownVersion = "0.0.0";
    private const char BuildMetadataSeparator = '+';
    private const char ReleaseSeparator = '@';

    public static Assembly Assembly { get; } = typeof(PluginIdentity).Assembly;

    public static string Name { get; } = ReadName();

    public static string Version { get; } = ReadVersion();

    public static string Release { get; } = Name + ReleaseSeparator + Version;

    private static string ReadName()
    {
        try
        {
            var name = Assembly.GetName().Name;
            return string.IsNullOrWhiteSpace(name) ? UnknownName : name;
        }
        catch (Exception)
        {
            return UnknownName;
        }
    }

    private static string ReadVersion()
    {
        try
        {
            var informational = Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
            {
                var metadata = informational.IndexOf(BuildMetadataSeparator);
                return metadata < 0 ? informational : informational[..metadata];
            }

            return Assembly.GetName().Version?.ToString() ?? UnknownVersion;
        }
        catch (Exception)
        {
            return UnknownVersion;
        }
    }
}
