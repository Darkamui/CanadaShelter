using Microsoft.EntityFrameworkCore;

namespace Shelter.BuildingBlocks.Persistence;

/// <summary>
/// A module's share of the composed <see cref="ShelterDbContext"/> model (ADR 0005). Each module registers one
/// (singleton) from <c>IModule.AddServices</c>; it applies the module's <c>internal</c> entity configurations,
/// every one mapped into the module's own schema.
/// </summary>
public interface IModelContributor
{
    /// <summary>Applies the module's entity configurations.</summary>
    void ConfigureModel(ModelBuilder modelBuilder);
}
