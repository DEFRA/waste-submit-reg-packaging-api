using WasteSubmitRegPackagingApi.Obligations.Models;

namespace WasteSubmitRegPackagingApi.Obligations.Services;

public sealed record MappedLeaverJoinerCode(int Code, EventAction Action);

/// <summary>
/// Maps the single leaver_code value from the registration data to its code number and whether
/// the organisation joined or left. Accepts the number with or without a leading zero ("03" or "3")
/// or the enum name. Anything that isn't a known joiner or leaver code maps to null.
/// </summary>
public static class LeaverJoinerCodeMapper
{
    public static MappedLeaverJoinerCode? Map(string? rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
        {
            return null;
        }

        var value = rawCode.Trim();

        // Enum.TryParse also accepts comma-separated lists, which are never a valid code.
        if (value.Contains(','))
        {
            return null;
        }

        if (Enum.TryParse<JoinerCode>(value, ignoreCase: true, out var joinerCode) && Enum.IsDefined(joinerCode))
        {
            return new MappedLeaverJoinerCode((int)joinerCode, EventAction.Joined);
        }

        if (Enum.TryParse<LeaverCode>(value, ignoreCase: true, out var leaverCode) && Enum.IsDefined(leaverCode))
        {
            return new MappedLeaverJoinerCode((int)leaverCode, EventAction.Left);
        }

        return null;
    }
}
