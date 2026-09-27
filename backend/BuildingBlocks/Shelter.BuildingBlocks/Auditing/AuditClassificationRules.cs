using Microsoft.EntityFrameworkCore.Metadata;

namespace Shelter.BuildingBlocks.Auditing;

/// <summary>
/// The model rule of ADR 0011: an entity linked to a person (it has an audit subject, a personal field, or a
/// <c>…PersonId</c> property) must classify every field, and its personal fields need an audit subject to be
/// encrypted rather than dropped. Checked by a test over the composed model.
/// </summary>
public static class AuditClassificationRules
{
    private const string PersonIdSuffix = "PersonId";

    /// <summary>One message per violation; empty when the model follows the rule.</summary>
    public static IReadOnlyList<string> FindViolations(IReadOnlyModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var violations = new List<string>();
        foreach (var entityType in model.GetEntityTypes())
        {
            var fields = Fields(entityType).ToList();
            var hasSubject = entityType.FindAuditSubjectProperty() is not null;
            var hasPersonal = fields.Any(f => f.Property.GetFieldClassification() == FieldClassification.Personal);
            var linked = hasSubject || hasPersonal || fields.Any(f => f.Property.Name.EndsWith(PersonIdSuffix, StringComparison.Ordinal));
            if (!linked)
            {
                continue;
            }

            violations.AddRange(fields
                .Where(f => f.Property.GetFieldClassification() == FieldClassification.Unclassified)
                .Select(f => $"{entityType.DisplayName()}.{f.Name} is not classified (IsPersonalData / IsNonPersonalData)."));

            if (hasPersonal && !hasSubject)
            {
                violations.Add($"{entityType.DisplayName()} has personal fields but no audit subject (HasAuditSubject).");
            }
        }

        return violations;
    }

    private static IEnumerable<(string Name, IReadOnlyProperty Property)> Fields(IReadOnlyTypeBase type, string prefix = "")
    {
        foreach (var property in type.GetProperties())
        {
            yield return (prefix + property.Name, property);
        }

        foreach (var complex in type.GetComplexProperties())
        {
            foreach (var field in Fields(complex.ComplexType, prefix + complex.Name + "."))
            {
                yield return field;
            }
        }
    }
}
