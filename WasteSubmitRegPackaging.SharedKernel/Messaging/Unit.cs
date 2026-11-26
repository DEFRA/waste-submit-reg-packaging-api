namespace WasteSubmitRegPackaging.SharedKernel.Messaging;

/// <summary>The response of a command that returns nothing.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = new();
}
