using WasteSubmitRegPackagingApi.Obligations.Models;

namespace WasteSubmitRegPackagingApi.Obligations.Services;

public sealed record MappedLeaverJoinerCode(JoinerCode? JoinerCode, LeaverCode? LeaverCode)
{
    public static MappedLeaverJoinerCode None { get; } = new(null, null);
}

/// <summary>
/// Maps the single leaver_code value from the registration data to a joiner or leaver code.
/// Accepts the number with or without a leading zero ("03" or "3") or the enum name.
/// Anything that isn't a known joiner or leaver code maps to <see cref="MappedLeaverJoinerCode.None"/>.
/// </summary>
public static class LeaverJoinerCodeMapper
{
    public static MappedLeaverJoinerCode Map(string? rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
        {
            return MappedLeaverJoinerCode.None;
        }

        var value = rawCode.Trim();

        // Enum.TryParse also accepts comma-separated lists, which are never a valid code.
        if (value.Contains(','))
        {
            return MappedLeaverJoinerCode.None;
        }

        if (Enum.TryParse<JoinerCode>(value, ignoreCase: true, out var joinerCode) && Enum.IsDefined(joinerCode))
        {
            return new MappedLeaverJoinerCode(joinerCode, null);
        }

        if (Enum.TryParse<LeaverCode>(value, ignoreCase: true, out var leaverCode) && Enum.IsDefined(leaverCode))
        {
            return new MappedLeaverJoinerCode(null, leaverCode);
        }

        return MappedLeaverJoinerCode.None;
    }
}