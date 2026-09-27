using Shelter.ArchitectureTests.Rules;

namespace Shelter.ArchitectureTests
{
    /// <summary>Deliberate violations: types that would be illegal if they lived in Shelter.Host.</summary>
    public sealed class HostTypeRulesTests
    {
        [Fact]
        public void Domain_and_feature_types_are_violations()
        {
            var violations = HostTypeRules.FindViolations(
                [typeof(Shelter.Host.Domain.Animal), typeof(Shelter.Host.Features.Adoptions.AdoptEndpoint), typeof(Shelter.Modules.People.Person)]);

            Assert.Equal(
                ["Shelter.Host.Domain.Animal", "Shelter.Host.Features.Adoptions.AdoptEndpoint", "Shelter.Modules.People.Person"],
                violations);
        }

        [Fact]
        public void Composition_types_are_allowed()
        {
            Assert.Empty(HostTypeRules.FindViolations([typeof(Program), typeof(Shelter.Host.Composition.FakeRegistration)]));
        }
    }
}

// Synthetic types for the tests above (static so they need no instances).
namespace Shelter.Host.Domain
{
    internal static class Animal;
}

namespace Shelter.Host.Features.Adoptions
{
    internal static class AdoptEndpoint;
}

namespace Shelter.Host.Composition
{
    internal static class FakeRegistration;
}

namespace Shelter.Modules.People
{
    internal static class Person;
}
