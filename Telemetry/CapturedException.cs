namespace Telemetry;

internal sealed record CapturedFrame(
    string? Module,
    string? Function,
    string? FileName,
    int LineNumber,
    bool InApp);

internal sealed record CapturedException(
    string Type,
    string Value,
    IReadOnlyList<CapturedFrame> Frames);
