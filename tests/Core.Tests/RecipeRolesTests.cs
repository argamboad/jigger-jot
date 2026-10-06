using JiggerJot.Core.Catalog;
using JiggerJot.Core.Entities;

namespace JiggerJot.Core.Tests;

/// <summary>
/// AUTHORING-3: the role a recipe line gets from what its ingredient IS (JJ-010, JJ-014 in spirit). The
/// seeded catalog was built with this rule in <c>seed/build_cocktails.py</c>; the write form now
/// suggests the same one, so a drink a household writes reads like one from the books.
/// </summary>
public class RecipeRolesTests
{
    private static RecipeRole[] Suggest(params string?[] categories) => [.. RecipeRoles.Suggest(categories)];

    [Theory]
    [InlineData("Gin")]
    [InlineData("Whisky")]
    [InlineData("Rum")]
    [InlineData("Agave")]
    [InlineData("Brandy")]
    [InlineData("Vodka")]
    [InlineData("Absinthe and pastis")]
    [InlineData("Other spirits")]
    public void ASpiritOnItsOwn_IsTheBase(string category)
    {
        Assert.Equal([RecipeRole.Base], Suggest(category));
    }

    [Fact]
    public void TheFirstSpirit_IsTheBase_AndEveryLaterOneModifiesIt()
    {
        // A Vieux Carré's rye leads; the cognac after it is a modifier, not a second base.
        Assert.Equal([RecipeRole.Base, RecipeRole.Modifier, RecipeRole.Modifier], Suggest("Whisky", "Brandy", "Gin"));
    }

    [Fact]
    public void ASpiritAfterAModifier_IsStillTheBase()
    {
        // Order of writing is not order of importance: vermouth first does not make the gin a modifier.
        Assert.Equal([RecipeRole.Modifier, RecipeRole.Base], Suggest("Vermouth", "Gin"));
    }

    [Theory]
    [InlineData("Juice", RecipeRole.Juice)]
    [InlineData("Syrup", RecipeRole.Syrup)]
    [InlineData("Bitters", RecipeRole.Bitters)]
    [InlineData("Soda and mixer", RecipeRole.Mixer)]
    [InlineData("Garnish", RecipeRole.Garnish)]
    [InlineData("Fruit", RecipeRole.Garnish)]
    [InlineData("Herb and spice", RecipeRole.Garnish)]
    [InlineData("Vermouth", RecipeRole.Modifier)]
    [InlineData("Fortified wine", RecipeRole.Modifier)]
    [InlineData("Liqueur", RecipeRole.Modifier)]
    [InlineData("Amaro and bitter", RecipeRole.Modifier)]
    [InlineData("Wine", RecipeRole.Modifier)]
    [InlineData("Beer and cider", RecipeRole.Modifier)]
    [InlineData("Sweetener", RecipeRole.Other)]
    [InlineData("Dairy and egg", RecipeRole.Other)]
    [InlineData("Coffee and tea", RecipeRole.Other)]
    [InlineData("Savoury", RecipeRole.Other)]
    public void EveryOtherCategory_HasTheRoleTheCatalogWasBuiltWith(string category, RecipeRole role)
    {
        Assert.Equal([role], Suggest(category));
    }

    [Fact]
    public void AnUnknownOrMissingCategory_IsOther_AndIsNotASpirit()
    {
        // Null is an ingredient the caller cannot see. It says nothing, and it does not take the base.
        Assert.Equal([RecipeRole.Other, RecipeRole.Other, RecipeRole.Base], Suggest(null, "Nonsense", "Gin"));
    }

    [Fact]
    public void CategoryNames_AreMatchedWithoutRegardToCase()
    {
        Assert.Equal([RecipeRole.Base, RecipeRole.Juice], Suggest("gin", "JUICE"));
    }

    [Theory]
    [InlineData(RecipeRole.Garnish, false)]
    [InlineData(RecipeRole.Base, true)]
    [InlineData(RecipeRole.Juice, true)]
    [InlineData(RecipeRole.Other, true)]
    public void AGarnishIsOptional_AndEverythingElseRequired(RecipeRole role, bool required)
    {
        // A garnish is just an optional line, and optional lines never block makeability (JJ-009).
        Assert.Equal(required, RecipeRoles.IsRequired(role));
    }
}
