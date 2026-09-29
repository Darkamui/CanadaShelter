namespace Shelter.Modules.People.Contracts;

/// <summary>
/// Read access to the current organization's people for other modules (ADR 0021): IDs in, summaries out. Runs in the
/// caller's scope and unit of work, so tenant isolation applies; another organization's IDs are simply not found.
/// </summary>
public interface IPersonDirectory
{
    /// <summary>The summaries of the people among <paramref name="ids"/> that exist, archived ones included.</summary>
    Task<IReadOnlyDictionary<Guid, PersonSummary>> GetSummariesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Whether <paramref name="id"/> is a person of the organization who is not archived.</summary>
    Task<bool> ExistsActiveAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>What another module may show about a person. Personal data: never log it or store it elsewhere.</summary>
/// <param name="Id">Person ID.</param>
/// <param name="DisplayName">Name shown in the app.</param>
/// <param name="IsArchived">Whether the record is archived.</param>
public sealed record PersonSummary(Guid Id, string DisplayName, bool IsArchived);
