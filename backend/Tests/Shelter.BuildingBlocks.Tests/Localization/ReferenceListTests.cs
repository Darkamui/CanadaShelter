using Shelter.BuildingBlocks.Localization;

namespace Shelter.BuildingBlocks.Tests.Localization;

public sealed class ReferenceListTests
{
    private static readonly ReferenceEntry[] Globals =
    [
        new("cat", new LocalizedText("Chat", "Cat"), 20),
        new("dog", new LocalizedText("Chien", "Dog"), 10),
        new("ferret", new LocalizedText("Furet", "Ferret"), 40),
    ];

    [Fact]
    public void Without_overrides_globals_are_sorted_by_order_then_code()
    {
        var items = ReferenceList.Merge([.. Globals, new("bird", new LocalizedText("Oiseau", "Bird"), 20)], []);

        Assert.Equal(["dog", "bird", "cat", "ferret"], items.Select(i => i.Code));
    }

    [Fact]
    public void Overrides_relabel_reorder_hide_and_add()
    {
        var items = ReferenceList.Merge(
            Globals,
            [
                new("dog", new LocalizedText("Chien (adulte)", "Dog (adult)"), 30),
                new("ferret", new LocalizedText("Furet", "Ferret"), 40, IsHidden: true),
                new("goat", new LocalizedText("Chèvre", "Goat"), 5),
            ]);

        Assert.Equal(["goat", "cat", "dog"], items.Select(i => i.Code));
        Assert.Equal(new LocalizedText("Chien (adulte)", "Dog (adult)"), items[2].Label);
    }

    [Fact]
    public void Hiding_an_unknown_code_changes_nothing()
    {
        var items = ReferenceList.Merge(Globals, [new("llama", new LocalizedText("Lama", "Llama"), 1, IsHidden: true)]);

        Assert.Equal(["dog", "cat", "ferret"], items.Select(i => i.Code));
    }
}
