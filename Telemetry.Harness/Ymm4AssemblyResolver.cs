using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Telemetry.Harness;

internal static class Ymm4AssemblyResolver
{
    private const string MetadataKey = "Ymm4Directory";
    private const string AssemblyExtension = ".dll";

    [ModuleInitializer]
    internal static void Initialize()
    {
        var directory = ReadDirectory();

        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        AppDomain.CurrentDomain.AssemblyResolve += (_, arguments) =>
        {
            var name = new AssemblyName(arguments.Name).Name;

            if (name is null)
            {
                return null;
            }

            var candidate = Path.Combine(directory, name + AssemblyExtension);
            return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
        };
    }

    private static string? ReadDirectory() => typeof(Ymm4AssemblyResolver).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => string.Equals(attribute.Key, MetadataKey, StringComparison.Ordinal))
        ?.Value;
}
