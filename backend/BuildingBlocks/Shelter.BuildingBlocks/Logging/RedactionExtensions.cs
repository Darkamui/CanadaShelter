using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Shelter.BuildingBlocks.Logging;

/// <summary>Log-scrubbing hook (CLAUDE.md hard rule 4).</summary>
public static class RedactionExtensions
{
    /// <summary>
    /// Enables redaction of classified log values: <see cref="ShelterDataClassifications.Personal"/>
    /// and any unknown classification are erased; <see cref="ShelterDataClassifications.NonPersonal"/> passes through.
    /// Applies to source-generated <c>[LoggerMessage]</c> methods whose parameters carry a classification attribute.
    /// </summary>
    public static ILoggingBuilder AddShelterRedaction(this ILoggingBuilder logging)
    {
        ArgumentNullException.ThrowIfNull(logging);

        logging.EnableRedaction();
        logging.Services.AddRedaction(redaction =>
        {
            redaction.SetRedactor<ErasingRedactor>(new DataClassificationSet(ShelterDataClassifications.Personal));
            redaction.SetRedactor<NullRedactor>(new DataClassificationSet(ShelterDataClassifications.NonPersonal));
            redaction.SetFallbackRedactor<ErasingRedactor>();
        });

        return logging;
    }
}
