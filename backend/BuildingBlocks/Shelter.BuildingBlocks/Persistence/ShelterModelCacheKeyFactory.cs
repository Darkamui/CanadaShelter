using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Shelter.BuildingBlocks.Persistence;

/// <summary>Caches one model per contributor set (tests compose extra test-only contributors).</summary>
internal sealed class ShelterModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        context is ShelterDbContext shelter
            ? (context.GetType(), shelter.ModelKey, designTime)
            : (context.GetType(), designTime);
}
