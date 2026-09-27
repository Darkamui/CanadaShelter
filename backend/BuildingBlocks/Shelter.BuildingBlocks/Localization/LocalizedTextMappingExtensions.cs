using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shelter.BuildingBlocks.Auditing;

namespace Shelter.BuildingBlocks.Localization;

/// <summary>EF Core mapping of <see cref="LocalizedText"/> (ADR 0013).</summary>
public static class LocalizedTextMappingExtensions
{
    /// <summary>
    /// Maps <paramref name="property"/> as a complex type to two required columns, <c>{column}_fr</c> and
    /// <c>{column}_en</c>. Values are classified non-personal: use this for reference data and configuration, not
    /// for text about a person.
    /// </summary>
    public static EntityTypeBuilder<TEntity> MapLocalizedText<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, LocalizedText?>> property,
        string column,
        int maxLength)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(column);

        builder.ComplexProperty(property, text =>
        {
            text.IsRequired();
            text.Property(t => t.Fr).HasColumnName(column + "_fr").HasMaxLength(maxLength).IsRequired().IsNonPersonalData();
            text.Property(t => t.En).HasColumnName(column + "_en").HasMaxLength(maxLength).IsRequired().IsNonPersonalData();
        });
        return builder;
    }
}
