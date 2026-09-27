using Shelter.BuildingBlocks.Localization;

namespace Shelter.BuildingBlocks.Tests.Localization;

public sealed class LocalizedTextTests
{
    private static readonly LocalizedText Dog = new("Chien", "Dog");

    [Theory]
    [InlineData("fr-CA", "Chien")]
    [InlineData("en-CA", "Dog")]
    [InlineData("en", "Dog")]
    [InlineData("EN-ca", "Dog")]
    [InlineData("es-MX", "Chien")]
    [InlineData("eng", "Chien")]
    [InlineData(null, "Chien")]
    public void Resolve_uses_the_requested_language_and_falls_back_to_french(string? locale, string expected) =>
        Assert.Equal(expected, Dog.Resolve(locale));

    [Theory]
    [InlineData("", "Dog")]
    [InlineData("Chien", " ")]
    public void Both_languages_are_required(string fr, string en) =>
        Assert.Throws<ArgumentException>(() => new LocalizedText(fr, en));
}
