using Domina.Core.Campaign;
using Domina.Core.Dojo;

namespace Domina.Presentation.Tests;

/// <summary>
/// The opening sheet's model. The decisions protected: the preview is the term itself rather than a
/// second set of rules, the same seed previews the same opening twice, and a province is described by
/// what it costs rather than by being called hard.
/// </summary>
public class NewTermModelTests
{
    [Fact]
    public void ThePreviewIsTheTermItself()
    {
        const ulong Seed = 41_882_06;

        IReadOnlyList<OpeningLine> preview = NewTermModel.Preview(Seed, DifficultyTier.Master);
        DojoState opened = NewGame.Create(Seed);

        string men = preview.Single(line => line.Label == "Men in the yard").Reading;
        string chest = preview.Single(line => line.Label == "In the chest").Reading;

        Assert.StartsWith(
            opened.Roster.Living.Count().ToString(System.Globalization.CultureInfo.InvariantCulture),
            men,
            StringComparison.Ordinal);
        Assert.Equal($"{opened.Resources.Gold} koku", chest);
    }

    [Fact]
    public void TheSameSeedOpensTheSameTerm()
    {
        IReadOnlyList<OpeningLine> once = NewTermModel.Preview(7, DifficultyTier.Master);
        IReadOnlyList<OpeningLine> again = NewTermModel.Preview(7, DifficultyTier.Master);

        Assert.Equal(once, again);
    }

    [Fact]
    public void EveryProvinceSaysWhatItCosts()
    {
        Assert.Equal(3, NewTermModel.Provinces.Count);

        foreach (ProvinceChoice choice in NewTermModel.Provinces)
        {
            Assert.False(string.IsNullOrWhiteSpace(choice.Name));
            Assert.False(string.IsNullOrWhiteSpace(choice.Cost));

            // Never "easy" and never "hard": a province is described by what it does to the term.
            Assert.DoesNotContain("easy", choice.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("hard", choice.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TheSeedIsSpelledSoItCanBeWrittenDown()
    {
        string spelled = NewTermModel.Spell(41_882_06);

        Assert.Matches(@"^\d{2}-\d{3}-\d{2}$", spelled);
    }
}
