using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Platform.Domain;

namespace Shelter.Modules.Platform.Persistence;

/// <summary>The Platform module's share of the composed model, in schema <c>platform</c>.</summary>
internal sealed class PlatformModelContributor : IModelContributor
{
    public const string Schema = "platform";

    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OrganizationConfiguration());
        modelBuilder.ApplyConfiguration(new TenantSettingConfiguration());
        modelBuilder.ApplyConfiguration(new UserAccountConfiguration());
        modelBuilder.ApplyConfiguration(new UserClaimConfiguration());
        modelBuilder.ApplyConfiguration(new UserLoginConfiguration());
        modelBuilder.ApplyConfiguration(new UserTokenConfiguration());
        modelBuilder.ApplyConfiguration(new SecurityEventConfiguration());
        modelBuilder.ApplyConfiguration(new StaffMembershipConfiguration());
    }
}

/// <summary>Global table: the tenant registry itself.</summary>
internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organization", PlatformModelContributor.Schema);
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Slug).HasMaxLength(63).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.HasIndex(o => o.Slug).IsUnique();
    }
}

/// <summary>
/// Tenant configuration values: codes and switches, never personal data. Key and Value are audited in plain form,
/// so a setting must never hold free text about a person (a contact email belongs on its own classified entity).
/// </summary>
internal sealed class TenantSettingConfiguration : IEntityTypeConfiguration<TenantSetting>
{
    public void Configure(EntityTypeBuilder<TenantSetting> builder)
    {
        builder.ToTable("tenant_setting", PlatformModelContributor.Schema);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Key).HasMaxLength(100).IsRequired().IsNonPersonalData();
        builder.Property(s => s.Value).HasMaxLength(4000).IsRequired().IsNonPersonalData();
        builder.HasIndex(s => new { s.TenantId, s.Key }).IsUnique();
    }
}

/// <summary>
/// Global accounts (ADR 0018): Identity's user table. Global because one account belongs to many organizations;
/// every column is classified, and personal columns name the account as their audit subject.
/// </summary>
internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("user_account", PlatformModelContributor.Schema);
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.HasAuditSubject(u => u.Id);

        builder.Property(u => u.UserName).HasMaxLength(256).IsPersonalData();
        builder.Property(u => u.NormalizedUserName).HasMaxLength(256).IsPersonalData();
        builder.Property(u => u.Email).HasMaxLength(256).IsPersonalData();
        builder.Property(u => u.NormalizedEmail).HasMaxLength(256).IsPersonalData();
        builder.Property(u => u.PhoneNumber).HasMaxLength(50).IsPersonalData();
        builder.Property(u => u.DisplayName).HasMaxLength(200).IsRequired().IsPersonalData();

        // A credential: never shown or logged, and never audited in plain form.
        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsPersonalData();

        builder.Property(u => u.SecurityStamp).HasMaxLength(100).IsNonPersonalData();
        builder.Property(u => u.ConcurrencyStamp).HasMaxLength(100).IsConcurrencyToken().IsNonPersonalData();
        builder.Property(u => u.EmailConfirmed).IsNonPersonalData();
        builder.Property(u => u.PhoneNumberConfirmed).IsNonPersonalData();
        builder.Property(u => u.TwoFactorEnabled).IsNonPersonalData();
        builder.Property(u => u.LockoutEnd).IsNonPersonalData();
        builder.Property(u => u.LockoutEnabled).IsNonPersonalData();
        builder.Property(u => u.AccessFailedCount).IsNonPersonalData();
        builder.Property(u => u.PreferredLanguage).HasMaxLength(2).IsRequired().IsNonPersonalData();
        builder.Property(u => u.IsPlatformOperator).IsNonPersonalData();
        builder.Property(u => u.CreatedAt).IsRequired().IsNonPersonalData();

        // The user name is the email: one account per email address.
        builder.HasIndex(u => u.NormalizedUserName).IsUnique();
        builder.HasIndex(u => u.NormalizedEmail).IsUnique();

        builder.HasMany<IdentityUserClaim<Guid>>().WithOne().HasForeignKey(c => c.UserId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.HasMany<IdentityUserLogin<Guid>>().WithOne().HasForeignKey(l => l.UserId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.HasMany<IdentityUserToken<Guid>>().WithOne().HasForeignKey(t => t.UserId).IsRequired().OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Identity account claims (unused by M2 but read by the sign-in pipeline). Global, like the account.</summary>
internal sealed class UserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        builder.ToTable("user_claim", PlatformModelContributor.Schema);
        builder.HasKey(c => c.Id);
        builder.HasAuditSubject(c => c.UserId);
        builder.Property(c => c.UserId).IsNonPersonalData();
        builder.Property(c => c.ClaimType).HasMaxLength(256).IsNonPersonalData();
        builder.Property(c => c.ClaimValue).HasMaxLength(1000).IsPersonalData();
        builder.HasIndex(c => c.UserId);
    }
}

/// <summary>External sign-in links (none in M2; SSO later, architecture §9.4). Global, like the account.</summary>
internal sealed class UserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        builder.ToTable("user_login", PlatformModelContributor.Schema);
        builder.HasKey(l => new { l.LoginProvider, l.ProviderKey });
        builder.HasAuditSubject(l => l.UserId);
        builder.Property(l => l.LoginProvider).HasMaxLength(128).IsNonPersonalData();
        builder.Property(l => l.ProviderKey).HasMaxLength(128).IsPersonalData();
        builder.Property(l => l.ProviderDisplayName).HasMaxLength(256).IsNonPersonalData();
        builder.Property(l => l.UserId).IsNonPersonalData();
        builder.HasIndex(l => l.UserId);
    }
}

/// <summary>
/// Identity tokens: the authenticator (TOTP) key and hashed recovery codes. Secrets, classified personal so they
/// never appear in plain form anywhere. Global, like the account.
/// </summary>
internal sealed class UserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable("user_token", PlatformModelContributor.Schema);
        builder.HasKey(t => new { t.UserId, t.LoginProvider, t.Name });
        builder.HasAuditSubject(t => t.UserId);
        builder.Property(t => t.LoginProvider).HasMaxLength(128).IsNonPersonalData();
        builder.Property(t => t.Name).HasMaxLength(128).IsNonPersonalData();
        builder.Property(t => t.Value).HasMaxLength(2000).IsPersonalData();
    }
}

/// <summary>
/// Global, append-only account security events (ADR 0018). The runtime role may only insert. No email or IP.
/// </summary>
internal sealed class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        builder.ToTable("security_event", PlatformModelContributor.Schema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.UserId).IsNonPersonalData();
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(40).IsRequired().IsNonPersonalData();
        builder.Property(e => e.OccurredAt).IsRequired().IsNonPersonalData();
        builder.Property(e => e.CorrelationId).HasMaxLength(64).IsNonPersonalData();
        builder.HasIndex(e => new { e.UserId, e.OccurredAt });
    }
}

/// <summary>
/// Staff memberships: tenant-owned, RLS plus a self-read policy (ADR 0018). No personal data; the account ID is a
/// plain column (no foreign key), so every index starts with the tenant.
/// </summary>
internal sealed class StaffMembershipConfiguration : IEntityTypeConfiguration<StaffMembership>
{
    public void Configure(EntityTypeBuilder<StaffMembership> builder)
    {
        builder.ToTable("staff_membership", PlatformModelContributor.Schema);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.UserId).IsNonPersonalData();
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20).IsRequired().IsNonPersonalData();
        builder.Property(m => m.CreatedAt).IsRequired().IsNonPersonalData();
        builder.HasIndex(m => new { m.TenantId, m.UserId }).IsUnique();
    }
}
