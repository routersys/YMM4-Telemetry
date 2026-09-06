using System.IO;
using YukkuriMovieMaker.Commons;

namespace Telemetry;

internal sealed class TelemetrySpool
{
    private const string DirectoryName = "telemetry";
    private const string FileExtension = ".envelope";
    private const string TemporaryExtension = ".tmp";
    private const string SearchPattern = "*" + FileExtension;
    public const int MaxFiles = 50;
    private const long MaxTotalBytes = 2L * 1024L * 1024L;

    private readonly string _directory;

    public TelemetrySpool(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public static TelemetrySpool Default { get; } = new(Path.Combine(AppDirectories.UserDirectory, DirectoryName));

    public string? TryWrite(string eventIdentifier, byte[] envelope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventIdentifier);
        ArgumentNullException.ThrowIfNull(envelope);

        try
        {
            Directory.CreateDirectory(_directory);

            var path = Path.Combine(_directory, eventIdentifier + FileExtension);
            var temporary = path + TemporaryExtension;

            File.WriteAllBytes(temporary, envelope);
            File.Move(temporary, path, true);

            Trim();
            return path;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public IReadOnlyList<string> List()
    {
        try
        {
            if (!Directory.Exists(_directory))
            {
                return [];
            }

            return [.. Directory.EnumerateFiles(_directory, SearchPattern)
                .Select(static path => new FileInfo(path))
                .OrderBy(static file => file.CreationTimeUtc)
                .Select(static file => file.FullName)];
        }
        catch (Exception)
        {
            return [];
        }
    }

    public byte[]? TryRead(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void TryDelete(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
        }
    }

    public void Clear()
    {
        foreach (var path in List())
        {
            TryDelete(path);
        }
    }

    private void Trim()
    {
        try
        {
            var files = Directory.EnumerateFiles(_directory, SearchPattern)
                .Select(static path => new FileInfo(path))
                .OrderBy(static file => file.CreationTimeUtc)
                .ToList();

            var total = files.Sum(static file => file.Length);
            var index = 0;

            while (index < files.Count && (files.Count - index > MaxFiles || total > MaxTotalBytes))
            {
                total -= files[index].Length;
                TryDelete(files[index].FullName);
                index++;
            }
        }
        catch (Exception)
        {
        }
    }
}
