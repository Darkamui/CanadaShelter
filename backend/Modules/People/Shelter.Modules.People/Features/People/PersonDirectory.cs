using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.People.Contracts;
using Shelter.Modules.People.Domain;

namespace Shelter.Modules.People.Features.People;

/// <summary><see cref="IPersonDirectory"/> over the caller's <see cref="ShelterDbContext"/> (ADR 0021). Read-only.</summary>
internal sealed class PersonDirectory(ShelterDbContext db) : IPersonDirectory
{
    public async Task<IReadOnlyDictionary<Guid, PersonSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, PersonSummary>();
        }

        List<Guid> distinct = [.. ids.Distinct()];
        return await db.Set<Person>()
            .AsNoTracking()
            .Where(p => distinct.Contains(p.Id))
            .Select(p => new PersonSummary(p.Id, p.DisplayName, p.IsArchived))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
    }

    public Task<bool> ExistsActiveAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<Person>().AnyAsync(p => p.Id == id && !p.IsArchived, cancellationToken);
}
