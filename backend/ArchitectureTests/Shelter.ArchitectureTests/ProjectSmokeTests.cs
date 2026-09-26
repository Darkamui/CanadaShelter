namespace Shelter.ArchitectureTests;

/// <summary>Placeholder until the boundary rules land in M0-6; proves the test project runs.</summary>
public sealed class ProjectSmokeTests
{
    [Fact]
    public void Host_assembly_loads() =>
        Assert.Equal("Shelter.Host", typeof(Program).Assembly.GetName().Name);
}
