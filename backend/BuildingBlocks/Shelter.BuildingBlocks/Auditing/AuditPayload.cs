using System.Text.Json;
using System.Text.Json.Nodes;

namespace Shelter.BuildingBlocks.Auditing;

/// <summary>
/// Builds the <c>before_json</c>/<c>after_json</c> objects: field name → value. Only non-personal values are
/// written in plain form. Personal values are replaced by a marker (M1-6 encrypts them instead); unclassified
/// values are never written, only the field name (fail closed).
/// </summary>
internal sealed class AuditPayload
{
    private readonly JsonObject _fields = [];

    public bool IsEmpty => _fields.Count == 0;

    public void Add(string name, object? value, FieldClassification classification) =>
        _fields[name] = classification switch
        {
            FieldClassification.NonPersonal => value is null ? null : JsonSerializer.SerializeToNode(value, value.GetType()),
            FieldClassification.Personal => Redacted("personal"),
            _ => Redacted("unclassified"),
        };

    public string? ToJson() => IsEmpty ? null : _fields.ToJsonString();

    private static JsonObject Redacted(string reason) => new() { ["$redacted"] = reason };
}
