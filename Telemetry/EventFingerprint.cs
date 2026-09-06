using System.Security.Cryptography;
using System.Text;

namespace Telemetry;

internal static class EventFingerprint
{
    private const int MaxFramesConsidered = 20;
    private const int IdentifierLength = 16;
    private const char Separator = '\n';
    private const char MemberSeparator = '.';

    public static string Compute(IReadOnlyList<CapturedException> captured)
    {
        ArgumentNullException.ThrowIfNull(captured);

        var builder = new StringBuilder();
        var appended = 0;

        foreach (var exception in captured)
        {
            builder.Append(exception.Type).Append(Separator);
            appended += Append(builder, exception.Frames, true, appended);
        }

        if (appended == 0)
        {
            foreach (var exception in captured)
            {
                appended += Append(builder, exception.Frames, false, appended);
            }
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexStringLower(hash)[..IdentifierLength];
    }

    private static int Append(StringBuilder builder, IReadOnlyList<CapturedFrame> frames, bool inAppOnly, int alreadyAppended)
    {
        var appended = 0;

        foreach (var frame in frames)
        {
            if (alreadyAppended + appended >= MaxFramesConsidered)
            {
                break;
            }

            if (inAppOnly && !frame.InApp)
            {
                continue;
            }

            builder.Append(frame.Module).Append(MemberSeparator).Append(frame.Function).Append(Separator);
            appended++;
        }

        return appended;
    }
}
