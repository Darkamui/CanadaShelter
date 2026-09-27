using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shelter.BuildingBlocks.Auditing;

/// <summary>How a field's value may appear in audit payloads (architecture §17.2).</summary>
public enum FieldClassification
{
    /// <summary>Not classified: the value is never written, only the field name (fail closed).</summary>
    Unclassified,

    /// <summary>Not personal information: stored in plain form.</summary>
    NonPersonal,

    /// <summary>Personal information (Law 25): never stored in plain form.</summary>
    Personal,
}

/// <summary>
/// Classifies entity properties in their EF configuration, e.g.
/// <c>builder.Property(p => p.Email).IsPersonalData();</c>. Primary keys and <c>TenantId</c> are non-personal
/// by convention; every other property must be classified for its value to be audited.
/// </summary>
public static class FieldClassificationExtensions
{
    /// <summary>Model annotation holding the <see cref="FieldClassification"/>.</summary>
    public const string Annotation = "Shelter:FieldClassification";

    /// <summary>Marks the property as personal information.</summary>
    public static PropertyBuilder<TProperty> IsPersonalData<TProperty>(this PropertyBuilder<TProperty> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasAnnotation(Annotation, nameof(FieldClassification.Personal));
        return builder;
    }

    /// <summary>Marks the property as explicitly non-personal.</summary>
    public static PropertyBuilder<TProperty> IsNonPersonalData<TProperty>(this PropertyBuilder<TProperty> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasAnnotation(Annotation, nameof(FieldClassification.NonPersonal));
        return builder;
    }

    /// <summary>Marks a complex-type property as personal information.</summary>
    public static ComplexTypePropertyBuilder<TProperty> IsPersonalData<TProperty>(this ComplexTypePropertyBuilder<TProperty> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasAnnotation(Annotation, nameof(FieldClassification.Personal));
        return builder;
    }

    /// <summary>Marks a complex-type property as explicitly non-personal.</summary>
    public static ComplexTypePropertyBuilder<TProperty> IsNonPersonalData<TProperty>(this ComplexTypePropertyBuilder<TProperty> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasAnnotation(Annotation, nameof(FieldClassification.NonPersonal));
        return builder;
    }

    /// <summary>The property's classification, with the key and <c>TenantId</c> conventions applied.</summary>
    public static FieldClassification GetFieldClassification(this IReadOnlyProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);

        if (property.FindAnnotation(Annotation)?.Value is string value
            && Enum.TryParse<FieldClassification>(value, out var classification))
        {
            return classification;
        }

        return property.IsPrimaryKey() || property.Name == nameof(Tenancy.ITenantOwned.TenantId)
            ? FieldClassification.NonPersonal
            : FieldClassification.Unclassified;
    }
}
