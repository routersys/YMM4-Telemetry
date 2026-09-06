using System.Diagnostics;
using System.Reflection;

namespace Telemetry;

internal static class ExceptionCapture
{
    public const int MaxChainLength = 5;
    public const int MaxFrames = 100;
    private const int NoLineNumber = 0;

    public static IReadOnlyList<CapturedException> Capture(Exception exception, Assembly? ownAssembly)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var thrownFirst = new List<Exception>(MaxChainLength);
        var current = exception;

        while (current is not null && thrownFirst.Count < MaxChainLength)
        {
            thrownFirst.Add(current);
            current = current is AggregateException aggregate && aggregate.InnerExceptions.Count > 0
                ? aggregate.InnerExceptions[0]
                : current.InnerException;
        }

        var captured = new List<CapturedException>(thrownFirst.Count);
        for (var index = thrownFirst.Count - 1; index >= 0; index--)
        {
            captured.Add(CaptureSingle(thrownFirst[index], ownAssembly));
        }

        return captured;
    }

    private static CapturedException CaptureSingle(Exception exception, Assembly? ownAssembly) => new(
        exception.GetType().FullName ?? exception.GetType().Name,
        TextScrubber.Scrub(exception.Message) ?? string.Empty,
        CaptureFrames(exception, ownAssembly));

    private static IReadOnlyList<CapturedFrame> CaptureFrames(Exception exception, Assembly? ownAssembly)
    {
        StackFrame[]? frames;

        try
        {
            frames = new StackTrace(exception, true).GetFrames();
        }
        catch (Exception)
        {
            return [];
        }

        if (frames is null || frames.Length == 0)
        {
            return [];
        }

        var captured = new List<CapturedFrame>(Math.Min(frames.Length, MaxFrames));

        for (var index = Math.Min(frames.Length, MaxFrames) - 1; index >= 0; index--)
        {
            var frame = CaptureFrame(frames[index], ownAssembly);
            if (frame is not null)
            {
                captured.Add(frame);
            }
        }

        return captured;
    }

    private static CapturedFrame? CaptureFrame(StackFrame frame, Assembly? ownAssembly)
    {
        MethodBase? method;

        try
        {
            method = frame.GetMethod();
        }
        catch (Exception)
        {
            method = null;
        }

        var function = method?.Name;
        var fileName = TextScrubber.Scrub(frame.GetFileName());

        if (string.IsNullOrEmpty(function) && string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        var declaringType = method?.DeclaringType;
        var lineNumber = frame.GetFileLineNumber();

        return new CapturedFrame(
            declaringType?.FullName,
            function,
            fileName,
            lineNumber > NoLineNumber ? lineNumber : NoLineNumber,
            ownAssembly is not null && declaringType is not null && ReferenceEquals(declaringType.Assembly, ownAssembly));
    }
}
