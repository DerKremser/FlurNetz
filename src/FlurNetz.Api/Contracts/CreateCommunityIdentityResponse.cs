namespace FlurNetz.Api.Contracts;

/// <summary>
/// Enthält die öffentliche HTTP-Repräsentation einer neu erzeugten Community-Identität.
/// </summary>
/// <param name="Id">Die erzeugte interne Community-Identity-ID.</param>
public sealed record CreateCommunityIdentityResponse(Guid Id);
