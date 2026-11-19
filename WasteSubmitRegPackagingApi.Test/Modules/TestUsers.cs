using WasteSubmitRegPackaging.SharedKernel.Users;

namespace WasteSubmitRegPackagingApi.Test.Modules;

internal static class TestUsers
{
    public static readonly Guid ProducerOrganisationId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    public static readonly ProducerUser Producer = new(Guid.Parse("22222222-2222-4222-8222-222222222222"), ProducerOrganisationId);

    public static readonly RegulatorUser Regulator = new(Guid.Parse("33333333-3333-4333-8333-333333333333"));
}

/// <summary>A clock that stays where you put it.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
