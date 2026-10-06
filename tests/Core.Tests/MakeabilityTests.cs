using JiggerJot.Core.Catalog;

namespace JiggerJot.Core.Tests;

/// <summary>
/// The derived rules behind "what can I make" and "what am I one ingredient away from" (JJ-003,
/// JJ-019), tested with no database anywhere near them. Every one of these is a judgement the app
/// makes on someone's behalf, so every one of them gets a name.
/// </summary>
public class MakeabilityTests
{
    private static readonly Guid Gin = Guid.CreateVersion7();
    private static readonly Guid Cointreau = Guid.CreateVersion7();
    private static readonly Guid Curacao = Guid.CreateVersion7();
    private static readonly Guid Triple = Guid.CreateVersion7();
    private static readonly Guid LemonJuice = Guid.CreateVersion7();
    private static readonly Guid Cognac = Guid.CreateVersion7();
    private static readonly Guid Brandy = Guid.CreateVersion7();

    /// <summary>Curaçao and triple sec stand in for Cointreau; cognac stands in for brandy, one way.</summary>
    private static readonly Dictionary<Guid, IReadOnlyList<Guid>> Graph = new()
    {
        [Cointreau] = [Curacao, Triple],
        [Brandy] = [Cognac],
    };

    private static LineMakeability Line(Guid ingredient, params Guid[] shelf) =>
        Makeability.ForLine(ingredient, new HashSet<Guid>(shelf), Graph);

    [Fact]
    public void TheExactBottleOnTheShelf_IsHave()
    {
        Assert.Equal(LineAvailability.Have, Line(Gin, Gin, LemonJuice).Availability);
    }

    [Fact]
    public void NothingThatCovers_IsMissing()
    {
        var line = Line(Cointreau, Gin, LemonJuice);

        Assert.Equal(LineAvailability.Missing, line.Availability);
        Assert.Null(line.SubstituteIngredientId);
    }

    [Fact]
    public void SomethingTheGraphAllows_IsASubstitute_AndSaysWhich()
    {
        var line = Line(Cointreau, Gin, Curacao);

        // Naming the bottle is the point: "you can make this" is useless if the reader still has to
        // work out which of their bottles the app had in mind (FEATURES §9).
        Assert.Equal(LineAvailability.Substitute, line.Availability);
        Assert.Equal(Curacao, line.SubstituteIngredientId);
    }

    [Fact]
    public void TheExactBottle_BeatsASubstitute()
    {
        // Both are on this shelf. Being told to pour Curaçao while the Cointreau sits beside it would
        // read as a bug, and would be one.
        var line = Line(Cointreau, Cointreau, Curacao);

        Assert.Equal(LineAvailability.Have, line.Availability);
        Assert.Null(line.SubstituteIngredientId);
    }

    [Fact]
    public void AmongSeveralSubstitutes_TheFirstOfferedWins()
    {
        // The caller orders the candidates; this only promises to respect that order, so the answer
        // is stable rather than whichever row the database handed over first.
        Assert.Equal(Curacao, Line(Cointreau, Curacao, Triple).SubstituteIngredientId);
        Assert.Equal(Triple, Line(Cointreau, Triple).SubstituteIngredientId);
    }

    [Fact]
    public void TheGraphIsNeverReadBackwards()
    {
        // Cognac stands in for brandy; brandy does not stand in for cognac (JJ-006). A household with
        // only brandy must not be told it can make a drink that asks for cognac, because it cannot.
        Assert.Equal(LineAvailability.Substitute, Line(Brandy, Cognac).Availability);
        Assert.Equal(LineAvailability.Missing, Line(Cognac, Brandy).Availability);
    }

    [Fact]
    public void EveryRequiredLineSatisfied_IsMakeable()
    {
        Assert.Equal(MakeabilityStatus.Makeable, Makeability.Overall(
        [
            (true, LineAvailability.Have),
            (true, LineAvailability.Substitute),
        ]));
    }

    [Fact]
    public void ExactlyOneRequiredLineShort_IsAlmostMakeable()
    {
        Assert.Equal(MakeabilityStatus.AlmostMakeable, Makeability.Overall(
        [
            (true, LineAvailability.Have),
            (true, LineAvailability.Missing),
        ]));
    }

    [Fact]
    public void TwoRequiredLinesShort_IsNotMakeable()
    {
        // MVP fixes N = 1 (JJ-019). Two away is a wish list rather than a shopping list.
        Assert.Equal(MakeabilityStatus.NotMakeable, Makeability.Overall(
        [
            (true, LineAvailability.Missing),
            (true, LineAvailability.Missing),
            (true, LineAvailability.Have),
        ]));
    }

    [Fact]
    public void AnUnstockedOptionalLine_NeverCounts()
    {
        // JJ-009: a garnish is just an optional line. Missing the mint does not stop the drink, and
        // it is never the one thing you are short of.
        Assert.Equal(MakeabilityStatus.Makeable, Makeability.Overall(
        [
            (true, LineAvailability.Have),
            (false, LineAvailability.Missing),
        ]));

        Assert.Equal(MakeabilityStatus.AlmostMakeable, Makeability.Overall(
        [
            (true, LineAvailability.Missing),
            (false, LineAvailability.Missing),
        ]));
    }

    [Fact]
    public void ADrinkWithNoRequiredLines_IsMakeable()
    {
        // Degenerate but real: the rule is "nothing required is missing", not "something is present",
        // and the two differ exactly here.
        Assert.Equal(MakeabilityStatus.Makeable, Makeability.Overall([]));
    }
}
