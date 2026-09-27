namespace Shelter.BuildingBlocks.Tenancy;

/// <summary>A tenant-owned operation ran without a tenant. A bug, never a user error.</summary>
public sealed class TenantContextMissingException : InvalidOperationException
{
    /// <summary>Creates the exception.</summary>
    public TenantContextMissingException()
        : base("No tenant context: tenant-owned data cannot be written or read.")
    {
    }

    /// <summary>Creates the exception.</summary>
    public TenantContextMissingException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    public TenantContextMissingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>A write targeted a row of another tenant. A bug, never a user error; the message carries no IDs.</summary>
public sealed class TenantIsolationException : InvalidOperationException
{
    /// <summary>Creates the exception.</summary>
    public TenantIsolationException()
        : base("Cross-tenant write rejected.")
    {
    }

    /// <summary>Creates the exception.</summary>
    public TenantIsolationException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    public TenantIsolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// A tenant-scoped database command ran outside a transaction, where <c>SET LOCAL</c> cannot apply. A bug: the
/// work must run inside a unit of work.
/// </summary>
public sealed class TenantTransactionRequiredException : InvalidOperationException
{
    /// <summary>Creates the exception.</summary>
    public TenantTransactionRequiredException()
        : base("Tenant-scoped database work must run inside a unit-of-work transaction.")
    {
    }

    /// <summary>Creates the exception.</summary>
    public TenantTransactionRequiredException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    public TenantTransactionRequiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
