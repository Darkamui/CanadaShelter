using Shelter.Modules.Operations.Domain;

namespace Shelter.Modules.Operations.Tests;

/// <summary>M3-3: the location's own rules, without a database.</summary>
public sealed class LocationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Name_is_trimmed_and_normalized_for_the_sibling_check()
    {
        var location = new Location(null, "room", "  Salle Éclair ", 4, Now);

        Assert.Equal("Salle Éclair", location.Name);
        Assert.Equal("salle eclair", location.NormalizedName);
    }

    [Fact]
    public void A_location_cannot_be_its_own_parent()
    {
        var location = new Location(null, "room", "Salle", null, Now);

        Assert.Throws<ArgumentException>(() => location.Update(location.Id, "room", "Salle", null, Now));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(Location.MaxCapacity + 1)]
    public void Capacity_out_of_range_is_rejected(int capacity) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Location(null, "room", "Salle", capacity, Now));

    [Fact]
    public void Archive_marks_the_location_archived()
    {
        var location = new Location(null, "room", "Salle", null, Now);

        location.Archive(Now.AddHours(1));

        Assert.True(location.IsArchived);
        Assert.Equal(Now.AddHours(1), location.UpdatedAt);
    }
}
