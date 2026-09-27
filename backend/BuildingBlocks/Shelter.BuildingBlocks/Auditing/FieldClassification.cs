using System.Linq.Expressions;
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

    /// <summary>Entity annotation naming the property that holds the audit subject (the person).</summary>
    public const string SubjectAnnotation = "Shelter:AuditSubject";

    /// <summary>
    /// Names the person whose data key encrypts this entity's personal values in audit payloads (ADR 0011), e.g.
    /// <c>builder.HasAuditSubject(p => p.Id)</c> on a person, <c>builder.HasAuditSubject(c => c.PersonId)</c> on a
    /// record about one. Without a subject, personal values are redacted instead of encrypted.
    /// </summary>
    public static EntityTypeBuilder<TEntity> HasAuditSubject<TEntity>(
        this EntityTypeBuilder<TEntity> builder, Expression<Func<TEntity, Guid>> subject)
        where TEntity : class => builder.HasAuditSubjectCore(subject);

    /// <inheritdoc cref="HasAuditSubject{TEntity}(EntityTypeBuilder{TEntity}, Expression{Func{TEntity, Guid}})"/>
    public static EntityTypeBuilder<TEntity> HasAuditSubject<TEntity>(
        this EntityTypeBuilder<TEntity> builder, Expression<Func<TEntity, Guid?>> subject)
        where TEntity : class => builder.HasAuditSubjectCore(subject);

    /// <summary>The property holding the entity's audit subject, if it has one.</summary>
    public static IReadOnlyProperty? FindAuditSubjectProperty(this IReadOnlyEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return entityType.FindAnnotation(SubjectAnnotation)?.Value is string name ? entityType.FindProperty(name) : null;
    }

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

    private static EntityTypeBuilder<TEntity> HasAuditSubjectCore<TEntity, TKey>(
        this EntityTypeBuilder<TEntity> builder, Expression<Func<TEntity, TKey>> subject)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(subject);

        if (subject.Body is not MemberExpression { Member.Name: var name })
        {
            throw new ArgumentException("The audit subject must be a property of the entity.", nameof(subject));
        }

        builder.HasAnnotation(SubjectAnnotation, name);
        return builder;
    }
}
