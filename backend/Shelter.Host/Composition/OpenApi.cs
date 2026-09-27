using Microsoft.OpenApi;

namespace Shelter.Host.Composition;

/// <summary>
/// OpenAPI document setup. XML doc comments become descriptions joined with the OS newline, so the exported
/// <c>openapi.json</c> would differ between Windows and Linux builds; descriptions are normalized to <c>\n</c>.
/// </summary>
internal static class OpenApi
{
    public static IServiceCollection AddShelterOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
        {
            foreach (var schema in document.Components?.Schemas?.Values ?? [])
            {
                NormalizeSchema(schema, depth: 0);
            }

            var operations = document.Paths?.Values.SelectMany(p => p.Operations?.Values.AsEnumerable() ?? []) ?? [];
            foreach (var operation in operations)
            {
                operation.Summary = Normalize(operation.Summary);
                operation.Description = Normalize(operation.Description);
            }

            return Task.CompletedTask;
        }));

    private static void NormalizeSchema(IOpenApiSchema schema, int depth)
    {
        if (schema is not OpenApiSchema concrete || depth > 16)
        {
            return;
        }

        concrete.Description = Normalize(concrete.Description);
        foreach (var property in concrete.Properties?.Values ?? [])
        {
            NormalizeSchema(property, depth + 1);
        }

        if (concrete.Items is { } items)
        {
            NormalizeSchema(items, depth + 1);
        }
    }

    private static string? Normalize(string? text) => text?.Replace("\r\n", "\n", StringComparison.Ordinal);
}
