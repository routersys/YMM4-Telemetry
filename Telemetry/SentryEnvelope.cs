using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Telemetry;

internal static class SentryEnvelope
{
    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    private const string Platform = "csharp";
    private const string DefaultGrouping = "{{ default }}";
    private const string Environment = "production";
    private const string RuntimeName = ".NET";
    private const string UnknownValue = "unknown";
    private const byte LineSeparator = (byte)'\n';
    private const int InitialCapacity = 4096;

    public static byte[] Build(string eventIdentifier, IReadOnlyList<CapturedException> captured, string level, DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventIdentifier);
        ArgumentNullException.ThrowIfNull(captured);
        ArgumentException.ThrowIfNullOrWhiteSpace(level);

        var buffer = new ArrayBufferWriter<byte>(InitialCapacity);

        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            WriteEnvelopeHeader(writer, eventIdentifier);
            writer.Flush();
            WriteLineSeparator(buffer);

            writer.Reset(buffer);
            WriteItemHeader(writer);
            writer.Flush();
            WriteLineSeparator(buffer);

            writer.Reset(buffer);
            WriteEvent(writer, eventIdentifier, captured, level, occurredAt);
            writer.Flush();
            WriteLineSeparator(buffer);
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteEnvelopeHeader(Utf8JsonWriter writer, string eventIdentifier)
    {
        writer.WriteStartObject();
        writer.WriteString("event_id", eventIdentifier);
        writer.WriteString("sent_at", Format(DateTimeOffset.UtcNow));
        writer.WriteEndObject();
    }

    private static void WriteItemHeader(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "event");
        writer.WriteString("content_type", "application/json");
        writer.WriteEndObject();
    }

    private static void WriteEvent(
        Utf8JsonWriter writer,
        string eventIdentifier,
        IReadOnlyList<CapturedException> captured,
        string level,
        DateTimeOffset occurredAt)
    {
        writer.WriteStartObject();
        writer.WriteString("event_id", eventIdentifier);
        writer.WriteString("timestamp", Format(occurredAt));
        writer.WriteString("platform", Platform);
        writer.WriteString("level", level);
        writer.WriteString("logger", TelemetryEndpoint.ClientName);
        writer.WriteString("release", PluginIdentity.Release);
        writer.WriteString("environment", Environment);

        writer.WriteStartObject("user");
        writer.WriteString("id", TelemetrySession.Id);
        writer.WriteEndObject();

        writer.WriteStartObject("sdk");
        writer.WriteString("name", TelemetryEndpoint.ClientName);
        writer.WriteString("version", TelemetryEndpoint.ClientVersion);
        writer.WriteEndObject();

        writer.WriteStartArray("fingerprint");
        writer.WriteStringValue(DefaultGrouping);
        writer.WriteStringValue(PluginIdentity.Name);
        writer.WriteEndArray();

        WriteTags(writer);
        WriteContexts(writer);
        WriteExceptions(writer, captured);

        writer.WriteEndObject();
    }

    private static void WriteTags(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("tags");
        writer.WriteString("plugin", PluginIdentity.Name);
        writer.WriteString("plugin_version", PluginIdentity.Version);
        writer.WriteString("session", TelemetrySession.Id);
        writer.WriteEndObject();
    }

    private static void WriteContexts(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("contexts");

        writer.WriteStartObject("os");
        writer.WriteString("type", "os");
        writer.WriteString("name", OperatingSystemName());
        writer.WriteString("version", Read(static () => System.Environment.OSVersion.Version.ToString()));
        writer.WriteEndObject();

        writer.WriteStartObject("runtime");
        writer.WriteString("type", "runtime");
        writer.WriteString("name", RuntimeName);
        writer.WriteString("version", Read(static () => RuntimeInformation.FrameworkDescription));
        writer.WriteEndObject();

        writer.WriteStartObject("device");
        writer.WriteString("type", "device");
        writer.WriteString("arch", Read(static () => RuntimeInformation.OSArchitecture.ToString()));
        writer.WriteNumber("processor_count", System.Environment.ProcessorCount);
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private static void WriteExceptions(Utf8JsonWriter writer, IReadOnlyList<CapturedException> captured)
    {
        writer.WriteStartObject("exception");
        writer.WriteStartArray("values");

        foreach (var exception in captured)
        {
            writer.WriteStartObject();
            writer.WriteString("type", exception.Type);
            writer.WriteString("value", exception.Value);

            writer.WriteStartObject("stacktrace");
            writer.WriteStartArray("frames");

            foreach (var frame in exception.Frames)
            {
                WriteFrame(writer, frame);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteFrame(Utf8JsonWriter writer, CapturedFrame frame)
    {
        writer.WriteStartObject();

        if (!string.IsNullOrEmpty(frame.FileName))
        {
            writer.WriteString("filename", frame.FileName);
        }

        if (!string.IsNullOrEmpty(frame.Function))
        {
            writer.WriteString("function", frame.Function);
        }

        if (!string.IsNullOrEmpty(frame.Module))
        {
            writer.WriteString("module", frame.Module);
        }

        if (frame.LineNumber > 0)
        {
            writer.WriteNumber("lineno", frame.LineNumber);
        }

        writer.WriteBoolean("in_app", frame.InApp);
        writer.WriteEndObject();
    }

    private static void WriteLineSeparator(ArrayBufferWriter<byte> buffer)
    {
        buffer.GetSpan(1)[0] = LineSeparator;
        buffer.Advance(1);
    }

    private static string Format(DateTimeOffset value) =>
        value.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture);

    private static string OperatingSystemName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Windows";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Linux";
        }

        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS" : UnknownValue;
    }

    private static string Read(Func<string> reader)
    {
        try
        {
            return reader() ?? UnknownValue;
        }
        catch (Exception)
        {
            return UnknownValue;
        }
    }
}
