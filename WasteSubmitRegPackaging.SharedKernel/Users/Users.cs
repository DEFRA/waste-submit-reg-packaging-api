namespace WasteSubmitRegPackaging.SharedKernel.Users;

/// <summary>
/// A user of a producer or compliance scheme organisation, acting for that organisation.
///
/// The calling frontend says which of its users a request is for, in the user headers (see
/// <c>HeaderCurrentUser</c>). Commands carry the user explicitly, and domain methods ask for the kind
/// of user allowed to call them, so a regulator can't be passed where a producer is needed.
/// </summary>
public sealed record ProducerUser(Guid UserId, Guid OrganisationId)
{
    public bool ActsFor(Guid organisationId) => OrganisationId == organisationId;
}

/// <summary>A regulator user, who reviews and decides on registrations.</summary>
public sealed record RegulatorUser(Guid UserId);

/// <summary>Which kind of user did something. Stored in history rows.</summary>
public enum UserType
{
    Producer,
    Regulator
}

/// <summary>
/// The user of the current request. Endpoints that use it call <c>.RequireProducer()</c> or
/// <c>.RequireRegulator()</c>, so the matching property is never null in the endpoint.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The kind of user the request says it is from, or null if it doesn't say.</summary>
    UserType? Type { get; }

    /// <summary>The producer user, or null if the request isn't from one.</summary>
    ProducerUser? Producer { get; }

    /// <summary>The regulator user, or null if the request isn't from one.</summary>
    RegulatorUser? Regulator { get; }
}
