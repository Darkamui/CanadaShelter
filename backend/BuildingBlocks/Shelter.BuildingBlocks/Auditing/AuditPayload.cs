using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Shelter.BuildingBlocks.Auditing;

/// <summary>
/// Builds the <c>before_json</c>/<c>after_json</c> objects: field name → value. Only non-personal values are
/// written in plain form. Personal values are held back until <see cref="Protect"/> encrypts them with the
/// subject's data key (or redacts them); unclassified values are never written, only the field name (fail closed).
/// </summary>
internal sealed class AuditPayload
{
    private readonly JsonObject _fields = [];
    private readonly List<KeyValuePair<string, object?>> _personal = [];

    public bool IsEmpty => _fields.Count == 0;

    public bool HasPersonalValues => _personal.Count > 0;

    public void Add(string name, object? value, FieldClassification classification)
    {
        switch (classification)
        {
            case FieldClassification.NonPersonal:
                _fields[name] = value is null ? null : JsonSerializer.SerializeToNode(value, value.GetType());
                break;
            case FieldClassification.Personal:
                // Placeholder keeps field order; replaced by Protect.
                _fields[name] = null;
                _personal.Add(new(name, value));
                break;
            default:
                _fields[name] = PersonalValueProtector.Redacted("unclassified");
                break;
        }
    }

    public void Protect(PersonalValueProtector protector)
    {
        foreach (var (name, value) in _personal)
        {
            _fields[name] = protector.Protect(name, value);
        }

        _personal.Clear();
    }

    public string? ToJson()
    {
        if (HasPersonalValues)
        {
            throw new InvalidOperationException("Personal values must be protected before the payload is written.");
        }

        return IsEmpty ? null : _fields.ToJsonString();
    }
}

/// <summary>Where an audited value lives; authenticated with its ciphertext so it cannot be moved elsewhere.</summary>
internal readonly record struct AuditValueLocation(Guid TenantId, Guid SubjectId, string EntityType, string EntityId)
{
    public byte[] AssociatedData(string field) =>
        Encoding.UTF8.GetBytes($"shelter-audit-v1|{TenantId:D}|{SubjectId:D}|{EntityType}|{EntityId}|{field}");
}

/// <summary>
/// Turns a personal value into its stored form: <c>{"$enc":"v1:…"}</c> (AES-256-GCM under the subject's data key)
/// or a <c>{"$redacted":…}</c> marker when there is no subject or its key was shredded.
/// </summary>
internal sealed class PersonalValueProtector
{
    public const string EncryptedMarker = "$enc";
    public const string RedactedMarker = "$redacted";

    private const string Version = "v1:";

    private readonly byte[]? _dataKey;
    private readonly AuditValueLocation _location;
    private readonly string _redactionReason;

    private PersonalValueProtector(byte[]? dataKey, AuditValueLocation location, string redactionReason)
    {
        _dataKey = dataKey;
        _location = location;
        _redactionReason = redactionReason;
    }

    /// <summary>No audit subject: personal values cannot be encrypted, so they are not recorded.</summary>
    public static PersonalValueProtector NoSubject { get; } = new(null, default, "personal");

    /// <summary>The subject's key was shredded: personal values are no longer recorded.</summary>
    public static PersonalValueProtector Shredded { get; } = new(null, default, "shredded");

    public static PersonalValueProtector Encrypting(byte[] dataKey, AuditValueLocation location) => new(dataKey, location, string.Empty);

    public static JsonObject Redacted(string reason) => new() { [RedactedMarker] = reason };

    /// <summary>Decrypts a stored <c>v1:</c> value back to its JSON.</summary>
    public static JsonNode? Decrypt(byte[] dataKey, AuditValueLocation location, string field, string encrypted)
    {
        if (!encrypted.StartsWith(Version, StringComparison.Ordinal))
        {
            throw new CryptographicException("Unknown encrypted value version.");
        }

        var plaintext = AesGcmEnvelope.Open(dataKey, Convert.FromBase64String(encrypted[Version.Length..]), location.AssociatedData(field));
        return JsonNode.Parse(plaintext);
    }

    public JsonNode Protect(string field, object? value)
    {
        if (_dataKey is null)
        {
            return Redacted(_redactionReason);
        }

        var plaintext = value is null ? "null"u8.ToArray() : JsonSerializer.SerializeToUtf8Bytes(value, value.GetType());
        var envelope = AesGcmEnvelope.Seal(_dataKey, plaintext, _location.AssociatedData(field));
        return new JsonObject { [EncryptedMarker] = Version + Convert.ToBase64String(envelope) };
    }
}
