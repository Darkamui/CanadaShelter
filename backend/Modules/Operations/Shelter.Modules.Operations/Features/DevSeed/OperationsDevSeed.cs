using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.DevSeed;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Search;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Operations.Domain;
using Shelter.Modules.Operations.Features.Locations;

namespace Shelter.Modules.Operations.Features.DevSeed;

/// <summary>
/// Development seed (ADR 0023): a main shelter with a dog building and a cat building, a Québec City branch, a partner
/// clinic and a community cat colony. Kennel and cage counts grow with the factor. Locations are matched by name among
/// active siblings first, so running it again on the same organization reuses the tree instead of failing.
/// </summary>
internal sealed class OperationsDevSeed(ShelterDbContext db, ITenantContext tenant) : IDevSeedContributor
{
    private const string Fallback = "room";

    private IReadOnlyDictionary<string, MergedLocationKind> _kinds = null!;
    private DevSeedContext _context = null!;

    public int Order => 10;

    public async Task SeedAsync(DevSeedContext context, CancellationToken cancellationToken)
    {
        _context = context;
        await LocationTree.LockAsync(db, tenant.RequireTenantId(), cancellationToken);
        _kinds = await LocationKindCatalog.LoadAsync(db, tenant, cancellationToken);

        var units = 4 + (2 * context.Factor);

        var main = await PlaceAsync(null, "shelter", "Refuge principal — Montréal", null, house: false, cancellationToken);
        var dogs = await PlaceAsync(main, "building", "Pavillon des chiens", null, house: false, cancellationToken);
        var dogRoom = await PlaceAsync(dogs, "room", "Chenil", null, house: false, cancellationToken);
        for (var i = 1; i <= units; i++)
        {
            await PlaceAsync(dogRoom, "kennel", $"Enclos {i}", 1, house: true, cancellationToken);
        }

        await PlaceAsync(dogs, "isolation", "Isolement canin", 4, house: true, cancellationToken);

        var cats = await PlaceAsync(main, "building", "Pavillon des chats", null, house: false, cancellationToken);
        var catRoom = await PlaceAsync(cats, "room", "Chatterie", null, house: false, cancellationToken);
        for (var i = 1; i <= units; i++)
        {
            await PlaceAsync(catRoom, "cage", $"Cage {i}", 1, house: true, cancellationToken);
        }

        await PlaceAsync(cats, "room", "Salle des petits animaux", 12, house: true, cancellationToken);
        await PlaceAsync(cats, "isolation", "Isolement félin", 6, house: true, cancellationToken);

        var branch = await PlaceAsync(null, "shelter", "Succursale de Québec", null, house: false, cancellationToken);
        await PlaceAsync(branch, "room", "Salle commune", 10, house: true, cancellationToken);
        for (var i = 1; i <= 2 + (context.Factor / 2); i++)
        {
            await PlaceAsync(branch, "cage", $"Cage Q{i}", 1, house: true, cancellationToken);
        }

        await PlaceAsync(null, "external_clinic", "Clinique vétérinaire partenaire", null, house: true, cancellationToken);
        await PlaceAsync(null, "field", "Colonie du parc La Fontaine", null, house: true, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The active location named <paramref name="name"/> under <paramref name="parentId"/>, created when missing.
    /// Housing locations (where animals go) are published for Movements.
    /// </summary>
    private async Task<Guid> PlaceAsync(
        Guid? parentId, string kindCode, string name, int? capacity, bool house, CancellationToken cancellationToken)
    {
        var normalized = SearchNormalizer.Text(name);
        var existing = await db.Set<Location>()
            .Where(l => l.ParentId == parentId && l.NormalizedName == normalized && !l.IsArchived)
            .Select(l => new { l.Id, l.KindCode })
            .FirstOrDefaultAsync(cancellationToken);

        Guid id;
        string kind;
        if (existing is not null)
        {
            (id, kind) = (existing.Id, existing.KindCode);
        }
        else
        {
            kind = Visible(kindCode);
            var location = new Location(parentId, kind, name, capacity, _context.Start);
            db.Set<Location>().Add(location);
            await db.SaveChangesAsync(cancellationToken);
            _context.Count("locations");
            id = location.Id;
        }

        if (house && _kinds.TryGetValue(kind, out var merged) && merged.HoldsAnimals)
        {
            _context.Publish(DevSeedKeys.HousingLocations, new DevSeedRecord(id, _context.Start, kind));
        }

        return id;
    }

    /// <summary><paramref name="code"/> when the organization can still pick it, otherwise a room.</summary>
    private string Visible(string code) =>
        _kinds.TryGetValue(code, out var kind) && !kind.IsHidden ? code : Fallback;
}
