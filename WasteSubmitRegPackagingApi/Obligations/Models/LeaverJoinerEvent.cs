using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace WasteSubmitRegPackagingApi.Obligations.Models;

[ExcludeFromCodeCoverage]
public class LeaverJoinerEvent
{
    [Description("The code from the registration file, numbered as in the GOV.UK registration file guidance. Joiner codes: 1, 2, 3, 7, 9, 15, 17, 19, 20. Leaver codes: 4, 5, 6, 8, 10, 11, 12, 13, 14, 16, 21.")]
    public required int Code { get; init; }

    [Description("Whether the organisation joined or left. Worked out from the code.")]
    public required EventAction Action { get; init; }

    [Description("When the organisation joined or left.")]
    public required DateOnly Date { get; init; }
}
