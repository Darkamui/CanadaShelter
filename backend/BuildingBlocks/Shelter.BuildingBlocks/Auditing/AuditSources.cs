namespace Shelter.BuildingBlocks.Auditing;

/// <summary>Well-known values of <see cref="AuditContext.Source"/>.</summary>
public static class AuditSources
{
    /// <summary>An HTTP request.</summary>
    public const string Api = "api";

    /// <summary>The platform itself, with no request or job (the default).</summary>
    public const string System = "system";

    /// <summary>A background job: <c>job:&lt;job type name&gt;</c>.</summary>
    public static string Job(string jobName) => "job:" + jobName;
}
