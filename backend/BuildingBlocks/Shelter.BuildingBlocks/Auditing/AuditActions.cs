namespace Shelter.BuildingBlocks.Auditing;

/// <summary>Actions recorded by <see cref="AuditSaveChangesInterceptor"/>. Explicit records may use their own.</summary>
public static class AuditActions
{
    /// <summary>A row was inserted.</summary>
    public const string Created = "Created";

    /// <summary>A row was updated.</summary>
    public const string Updated = "Updated";

    /// <summary>A row was deleted.</summary>
    public const string Deleted = "Deleted";
}
