using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shelter.BuildingBlocks.Authorization;

namespace Shelter.BuildingBlocks.Tests.Authorization;

public sealed class MfaEnforcementTests
{
    [Theory]
    [InlineData("Development", "true", false)]
    [InlineData("Development", "false", true)]
    [InlineData("Development", null, true)]
    [InlineData("Production", "true", true)]
    [InlineData("Staging", "true", true)]
    [InlineData("Test", "true", true)]
    public void Bypass_applies_only_in_development(string environment, string? flag, bool enforced)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [MfaEnforcement.DevelopmentBypassKey] = flag })
            .Build();

        Assert.Equal(enforced, MfaEnforcement.From(configuration, new FakeEnvironment(environment)).Enforced);
    }

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Shelter.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
