using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Authorization;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Persistence.Paging;
using Shelter.BuildingBlocks.Search;
using Shelter.Modules.People.Authorization;
using Shelter.Modules.People.Domain;

namespace Shelter.Modules.People.Features.People;

/// <summary>
/// <c>/api/people</c>: the organization's people (M3-2). Every change is an audited update of a tenant-owned row whose
/// personal fields are encrypted in the audit trail. IDs from another organization are not found (RLS and filter).
/// Creating a person never blocks on a possible duplicate: it returns the matches so staff can check.
/// </summary>
internal static class PeopleEndpoints
{
    private const int MaxDuplicates = 10;

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("", List)
            .WithName("ListPeople")
            .RequirePermission(PeoplePermissions.Read);

        endpoints.MapGet("/{personId:guid}", Get)
            .WithName("GetPerson")
            .RequirePermission(PeoplePermissions.Read);

        endpoints.MapPost("", Create)
            .WithName("CreatePerson")
            .RequirePermission(PeoplePermissions.Write);

        endpoints.MapPut("/{personId:guid}", Update)
            .WithName("UpdatePerson")
            .RequirePermission(PeoplePermissions.Write);

        endpoints.MapPost("/{personId:guid}/archive", Archive)
            .WithName("ArchivePerson")
            .RequirePermission(PeoplePermissions.Write);

        endpoints.MapPost("/{personId:guid}/unarchive", Unarchive)
            .WithName("UnarchivePerson")
            .RequirePermission(PeoplePermissions.Write);
    }

    /// <summary>
    /// A page of people. <c>q</c> matches names and email without regard to accents or case, and phone numbers by
    /// their digits (3 or more). Active people unless <c>archived=true</c>.
    /// </summary>
    internal static async Task<Results<Ok<PagedResult<PersonListItem>>, ValidationProblem>> List(
        [AsParameters] ListPeopleQuery query, ShelterDbContext db, CancellationToken cancellationToken)
    {
        if (query.Role is not null && !PersonRoles.All.Contains(query.Role))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Unknown role."] });
        }

        var people = db.Set<Person>().AsNoTracking().Where(p => p.IsArchived == (query.Archived ?? false));
        if (query.Role is not null)
        {
            people = people.Where(p => p.RoleTags.Contains(query.Role));
        }

        people = Search(people, query.Q);

        IOrderedQueryable<Person> ordered = query.Sort switch
        {
            "-name" => people.OrderByDescending(p => p.DisplayName).ThenByDescending(p => p.Id),
            "createdAt" => people.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id),
            "-createdAt" => people.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id),
            _ => people.OrderBy(p => p.DisplayName).ThenBy(p => p.Id),
        };

        var page = await ordered.ToPagedResultAsync(
            p => new PersonListItem(p.Id, p.DisplayName, p.Email, p.Phone, p.City, p.RoleTags, p.IsArchived),
            new PageRequest(query.Page, query.PageSize),
            cancellationToken);
        return TypedResults.Ok(page);
    }

    internal static async Task<Results<Ok<PersonResponse>, NotFound>> Get(Guid personId, ShelterDbContext db, CancellationToken cancellationToken)
    {
        var person = await db.Set<Person>().AsNoTracking().SingleOrDefaultAsync(p => p.Id == personId, cancellationToken);
        return person is null ? TypedResults.NotFound() : TypedResults.Ok(PersonResponse.From(person));
    }

    internal static async Task<Results<Created<CreatePersonResponse>, ValidationProblem>> Create(
        PersonRequest request, ShelterDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var (details, errors) = PersonRequestValidator.Validate(request);
        if (details is null)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var person = new Person(details, timeProvider.GetUtcNow());
        var duplicates = await FindDuplicatesAsync(db, person, cancellationToken);

        db.Set<Person>().Add(person);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/people/{person.Id:D}", new CreatePersonResponse(PersonResponse.From(person), duplicates));
    }

    internal static async Task<Results<Ok<PersonResponse>, NotFound, ValidationProblem>> Update(
        Guid personId, PersonRequest request, ShelterDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var (details, errors) = PersonRequestValidator.Validate(request);
        if (details is null)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var person = await db.Set<Person>().SingleOrDefaultAsync(p => p.Id == personId, cancellationToken);
        if (person is null)
        {
            return TypedResults.NotFound();
        }

        person.Update(details, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(PersonResponse.From(person));
    }

    internal static Task<Results<NoContent, NotFound>> Archive(
        Guid personId, ShelterDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken) =>
        ChangeAsync(personId, db, p => p.Archive(timeProvider.GetUtcNow()), cancellationToken);

    internal static Task<Results<NoContent, NotFound>> Unarchive(
        Guid personId, ShelterDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken) =>
        ChangeAsync(personId, db, p => p.Unarchive(timeProvider.GetUtcNow()), cancellationToken);

    private static async Task<Results<NoContent, NotFound>> ChangeAsync(
        Guid personId, ShelterDbContext db, Action<Person> change, CancellationToken cancellationToken)
    {
        var person = await db.Set<Person>().SingleOrDefaultAsync(p => p.Id == personId, cancellationToken);
        if (person is null)
        {
            return TypedResults.NotFound();
        }

        change(person);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static IQueryable<Person> Search(IQueryable<Person> people, string? q)
    {
        var text = SearchNormalizer.Text(q);
        var digits = PhoneNumbers.Key(q);
        var textPattern = text.Length > 0 ? SearchNormalizer.ContainsPattern(text) : null;
        var phonePattern = digits is { Length: >= 3 } ? SearchNormalizer.ContainsPattern(digits) : null;

        return (textPattern, phonePattern) switch
        {
            (null, null) => people,
            (not null, null) => people.Where(p => EF.Functions.Like(p.SearchText, textPattern)),
            (null, not null) => people.Where(p =>
                EF.Functions.Like(p.PhoneDigits, phonePattern) || EF.Functions.Like(p.SecondaryPhoneDigits, phonePattern)),
            _ => people.Where(p =>
                EF.Functions.Like(p.SearchText, textPattern)
                || EF.Functions.Like(p.PhoneDigits, phonePattern)
                || EF.Functions.Like(p.SecondaryPhoneDigits, phonePattern)),
        };
    }

    /// <summary>People with the same email or a shared phone number, archived ones included.</summary>
    private static async Task<IReadOnlyList<PersonMatch>> FindDuplicatesAsync(ShelterDbContext db, Person person, CancellationToken cancellationToken)
    {
        var email = person.NormalizedEmail;
        List<string> phones = [.. new[] { person.PhoneDigits, person.SecondaryPhoneDigits }.OfType<string>().Distinct()];
        if (email is null && phones.Count == 0)
        {
            return [];
        }

        var people = db.Set<Person>().AsNoTracking();
        people = (email, phones.Count) switch
        {
            (not null, 0) => people.Where(p => p.NormalizedEmail == email),
            (null, _) => people.Where(p => phones.Contains(p.PhoneDigits!) || phones.Contains(p.SecondaryPhoneDigits!)),
            _ => people.Where(p =>
                p.NormalizedEmail == email || phones.Contains(p.PhoneDigits!) || phones.Contains(p.SecondaryPhoneDigits!)),
        };

        return await people
            .OrderBy(p => p.DisplayName)
            .ThenBy(p => p.Id)
            .Take(MaxDuplicates)
            .Select(p => new PersonMatch(p.Id, p.DisplayName, p.IsArchived))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Filters, search and page of the people list.</summary>
/// <param name="Page">1-based page; default 1.</param>
/// <param name="PageSize">1 to 100; default 25.</param>
/// <param name="Q">Name, email or phone number, in part.</param>
/// <param name="Role">Only people with this role tag.</param>
/// <param name="Archived"><c>true</c> for archived people only; active people otherwise.</param>
/// <param name="Sort"><c>name</c> (default), <c>-name</c>, <c>createdAt</c> or <c>-createdAt</c>.</param>
internal sealed record ListPeopleQuery(
    [FromQuery(Name = "page")] int? Page,
    [FromQuery(Name = "pageSize")] int? PageSize,
    [FromQuery(Name = "q")] string? Q,
    [FromQuery(Name = "role")] string? Role,
    [FromQuery(Name = "archived")] bool? Archived,
    [FromQuery(Name = "sort")] string? Sort);

/// <summary>A person's editable fields. Blank values are stored as empty.</summary>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="DisplayName">Name shown in the app; "first last" when omitted. A display name or a first or last name is required.</param>
/// <param name="Email">Email address.</param>
/// <param name="Phone">Main phone number.</param>
/// <param name="SecondaryPhone">Other phone number.</param>
/// <param name="AddressLine">Street address.</param>
/// <param name="City">City.</param>
/// <param name="Province">Two-letter province or territory code, such as <c>QC</c>.</param>
/// <param name="PostalCode">Postal code.</param>
/// <param name="PreferredLanguage"><c>fr</c> (default) or <c>en</c>.</param>
/// <param name="RoleTags">Zero or more of <c>owner</c>, <c>adopter</c>, <c>foster</c>, <c>volunteer</c>, <c>donor</c>, <c>finder</c>, <c>surrenderer</c>.</param>
/// <param name="Notes">Free-text notes about the person.</param>
internal sealed record PersonRequest(
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string? Email,
    string? Phone,
    string? SecondaryPhone,
    string? AddressLine,
    string? City,
    string? Province,
    string? PostalCode,
    string? PreferredLanguage,
    IReadOnlyList<string>? RoleTags,
    string? Notes);

/// <summary>A row of the people list.</summary>
/// <param name="Id">Person ID.</param>
/// <param name="DisplayName">Name shown in the app.</param>
/// <param name="Email">Email address.</param>
/// <param name="Phone">Main phone number.</param>
/// <param name="City">City.</param>
/// <param name="RoleTags">Role tags.</param>
/// <param name="IsArchived">Whether the record is archived.</param>
internal sealed record PersonListItem(Guid Id, string DisplayName, string? Email, string? Phone, string? City, IReadOnlyList<string> RoleTags, bool IsArchived);

/// <summary>A person.</summary>
/// <param name="Id">Person ID.</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="DisplayName">Name shown in the app.</param>
/// <param name="Email">Email address.</param>
/// <param name="Phone">Main phone number.</param>
/// <param name="SecondaryPhone">Other phone number.</param>
/// <param name="AddressLine">Street address.</param>
/// <param name="City">City.</param>
/// <param name="Province">Province or territory code.</param>
/// <param name="PostalCode">Postal code.</param>
/// <param name="PreferredLanguage"><c>fr</c> or <c>en</c>.</param>
/// <param name="RoleTags">Role tags.</param>
/// <param name="Notes">Notes.</param>
/// <param name="IsArchived">Whether the record is archived.</param>
/// <param name="CreatedAt">When the record was created.</param>
/// <param name="UpdatedAt">When the record last changed.</param>
internal sealed record PersonResponse(
    Guid Id,
    string? FirstName,
    string? LastName,
    string DisplayName,
    string? Email,
    string? Phone,
    string? SecondaryPhone,
    string? AddressLine,
    string? City,
    string? Province,
    string? PostalCode,
    string PreferredLanguage,
    IReadOnlyList<string> RoleTags,
    string? Notes,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static PersonResponse From(Person p) => new(
        p.Id, p.FirstName, p.LastName, p.DisplayName, p.Email, p.Phone, p.SecondaryPhone, p.AddressLine, p.City, p.Province,
        p.PostalCode, p.PreferredLanguage, p.RoleTags, p.Notes, p.IsArchived, p.CreatedAt, p.UpdatedAt);
}

/// <summary>The created person, and existing people who may be the same one.</summary>
/// <param name="Person">The created person.</param>
/// <param name="PossibleDuplicates">People with the same email or phone number; the person was created anyway.</param>
internal sealed record CreatePersonResponse(PersonResponse Person, IReadOnlyList<PersonMatch> PossibleDuplicates);

/// <summary>An existing person who may be the same one.</summary>
/// <param name="Id">Person ID.</param>
/// <param name="DisplayName">Name shown in the app.</param>
/// <param name="IsArchived">Whether the record is archived.</param>
internal sealed record PersonMatch(Guid Id, string DisplayName, bool IsArchived);
