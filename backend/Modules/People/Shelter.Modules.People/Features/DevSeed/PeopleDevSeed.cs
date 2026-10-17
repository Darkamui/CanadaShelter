using System.Globalization;
using System.Text;
using Shelter.BuildingBlocks.DevSeed;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.People.Domain;
using Shelter.Modules.People.Features.People;

namespace Shelter.Modules.People.Features.DevSeed;

/// <summary>
/// Development seed (ADR 0023): 20 people per factor unit, through the same validation as the API. Mostly French
/// speakers, some organizations, a few archived, and a few sharing an email or phone with an earlier person so the
/// duplicate hints have something to find. Active people are published by role for Movements (adopters, owners,
/// finders, surrenderers). All values are fictional (<see cref="SampleData"/>).
/// </summary>
internal sealed class PeopleDevSeed(ShelterDbContext db) : IDevSeedContributor
{
    private const int BatchSize = 200;

    private static readonly (string Role, int Weight)[] RoleWeights =
    [
        (PersonRoles.Adopter, 30),
        (PersonRoles.Owner, 20),
        (PersonRoles.Finder, 12),
        (PersonRoles.Volunteer, 12),
        (PersonRoles.Foster, 10),
        (PersonRoles.Donor, 10),
        (PersonRoles.Surrenderer, 10),
    ];

    public int Order => 20;

    public async Task SeedAsync(DevSeedContext context, CancellationToken cancellationToken)
    {
        var count = context.Scaled(20);
        var created = new List<Person>(count);
        for (var i = 0; i < count; i++)
        {
            // The first people cover every role once, so even the smallest run can record every outcome.
            var roles = i < RoleWeights.Length ? [RoleWeights[i].Role] : Roles(context);
            var request = context.Chance(0.05) ? Organization(context, roles) : Individual(context, roles, i);
            if (created.Count > 10 && context.Chance(0.03))
            {
                request = SharingContactWith(context, request, context.Pick(created));
            }

            var (details, errors) = PersonRequestValidator.Validate(request);
            if (details is null)
            {
                throw new InvalidOperationException("Dev seed generated an invalid person: " + string.Join(", ", errors.Keys));
            }

            var person = new Person(details, context.Between(context.Start, context.Now.AddDays(-1)));
            if (i >= RoleWeights.Length && context.Chance(0.04))
            {
                person.Archive(context.Now.AddDays(-context.Random.Next(1, 60)));
                context.Count("people.archived");
            }
            else
            {
                foreach (var role in person.RoleTags)
                {
                    context.Publish(DevSeedKeys.People, new DevSeedRecord(person.Id, person.CreatedAt, role));
                }
            }

            db.Set<Person>().Add(person);
            created.Add(person);
            context.Count("people");
            if (created.Count % BatchSize == 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                db.ChangeTracker.Clear();
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    private static List<string> Roles(DevSeedContext context)
    {
        var roles = new List<string> { context.Weighted(RoleWeights) };
        if (context.Chance(0.25))
        {
            roles.Add(context.Weighted(RoleWeights));
        }

        return roles;
    }

    private static PersonRequest Individual(DevSeedContext context, List<string> roles, int sequence)
    {
        var english = context.Chance(0.15);
        var first = context.Pick(english ? SampleData.EnglishFirstNames : SampleData.FrenchFirstNames);
        var last = context.Pick(english ? SampleData.EnglishLastNames : SampleData.FrenchLastNames);
        var place = context.Pick(SampleData.Places);
        var random = context.Random;
        return new PersonRequest(
            FirstName: first,
            LastName: last,
            DisplayName: null,
            Email: context.Chance(0.85) ? Email(first, last, sequence) : null,
            Phone: context.Chance(0.9) ? SampleData.Phone(random, place.AreaCode) : null,
            SecondaryPhone: context.Chance(0.15) ? SampleData.Phone(random, place.AreaCode) : null,
            AddressLine: context.Chance(0.7) ? SampleData.AddressLine(random, place.Street) : null,
            City: place.City,
            Province: "QC",
            PostalCode: context.Chance(0.7) ? SampleData.PostalCode(random, place.PostalPrefix) : null,
            PreferredLanguage: english ? PersonRequestValidator.English : PersonRequestValidator.French,
            RoleTags: roles,
            Notes: context.Chance(0.2) ? context.Pick(SampleData.PersonNotes) : null);
    }

    private static PersonRequest Organization(DevSeedContext context, List<string> roles)
    {
        var name = context.Pick(SampleData.OrganizationNames);
        var place = context.Pick(SampleData.Places);
        return new PersonRequest(
            FirstName: null,
            LastName: null,
            DisplayName: name,
            Email: "info@" + Ascii(name).Replace('.', '-') + ".example.org",
            Phone: SampleData.Phone(context.Random, place.AreaCode),
            SecondaryPhone: null,
            AddressLine: SampleData.AddressLine(context.Random, place.Street),
            City: place.City,
            Province: "QC",
            PostalCode: SampleData.PostalCode(context.Random, place.PostalPrefix),
            PreferredLanguage: PersonRequestValidator.French,
            RoleTags: roles,
            Notes: null);
    }

    /// <summary>The same person entered twice, or a couple sharing a phone: same email or phone as <paramref name="earlier"/>.</summary>
    private static PersonRequest SharingContactWith(DevSeedContext context, PersonRequest request, Person earlier) =>
        context.Chance(0.5) && earlier.Email is not null
            ? request with { Email = earlier.Email }
            : request with { Phone = earlier.Phone ?? request.Phone };

    private static string Email(string first, string last, int sequence) =>
        string.Create(CultureInfo.InvariantCulture, $"{Ascii(first)}.{Ascii(last)}{sequence}@example.com");

    /// <summary>Lower-case ASCII letters, digits and dots: accents dropped, other characters removed.</summary>
    private static string Ascii(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value.Normalize(NormalizationForm.FormD))
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
            else if (c is ' ' or '-' && builder.Length > 0 && builder[^1] != '.')
            {
                builder.Append('.');
            }
        }

        return builder.ToString().Trim('.');
    }
}
