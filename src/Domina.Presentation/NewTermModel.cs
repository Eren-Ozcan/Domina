using System.Globalization;
using Domina.Core.Campaign;
using Domina.Core.Dojo;

namespace Domina.Presentation;

/// <summary>One line of the opening: what the term starts with, and the figure for it.</summary>
/// <param name="Label">What is being counted — "men in the yard", "in the chest".</param>
/// <param name="Reading">The figure and its unit, as it is printed.</param>
public readonly record struct OpeningLine(string Label, string Reading);

/// <summary>A tier of the province, as the sheet offers it.</summary>
/// <param name="Tier">The tier itself.</param>
/// <param name="Name">What it is called on the sheet — never "easy" or "hard".</param>
/// <param name="Cost">What choosing it does, in the terms the term is played in.</param>
public readonly record struct ProvinceChoice(DifficultyTier Tier, string Name, string Cost);

/// <summary>
/// What a seed opens with, read before the term is opened.
/// </summary>
/// <remarks>
/// <para>
/// A seed the player cannot read anything off is a seed he has no reason to draw again, and the
/// opening the design's sheet promises — four men, the chest, the store in days, the first summons —
/// is exactly the set of figures that decide whether a term is worth starting (design canvas → 6b).
/// </para>
/// <para>
/// It is built by opening the term and reading it, not by a second set of rules: a preview that
/// predicts the opening rather than running it is a preview that will one day be wrong, and the run
/// the player then plays is the one the preview lied about.
/// </para>
/// </remarks>
public static class NewTermModel
{
    /// <summary>The three provinces, in the order the sheet prints them.</summary>
    public static IReadOnlyList<ProvinceChoice> Provinces { get; } =
    [
        new(
            DifficultyTier.Apprentice,
            "A quiet province",
            "The enemy comes lighter and the work pays better. The ledger's own figures do not hold here."),
        new(
            DifficultyTier.Master,
            "As it is written",
            "The term the ledger assumes: every number in the design was measured on this one."),
        new(
            DifficultyTier.Legend,
            "A bad year",
            "The enemy comes heavier and the same work pays less. Nothing else is taken away."),
    ];

    /// <summary>Opens the term the seed describes and reads what it starts with.</summary>
    public static IReadOnlyList<OpeningLine> Preview(ulong seed, DifficultyTier tier)
    {
        DojoState dojo = NewGame.Create(seed, tier: tier);
        Resources purse = dojo.Resources;
        Resources draw = dojo.DailyDraw();

        return
        [
            new OpeningLine("Men in the yard", Count(dojo.Roster.Living.Count(), "man", "men")),
            new OpeningLine("In the chest", $"{purse.Gold.ToString(CultureInfo.InvariantCulture)} koku"),
            new OpeningLine("The store", Store(purse, draw)),
            new OpeningLine(
                "Heads to the gate",
                Count(dojo.Season.Tuning.BountyGate, "head", "heads")),
            new OpeningLine(
                "The term",
                $"{dojo.Season.Tuning.Days.ToString(CultureInfo.InvariantCulture)} days, one save"),
        ];
    }

    /// <summary>The seed as the sheet writes it: three groups, so it can be read aloud and typed back.</summary>
    public static string Spell(ulong seed)
    {
        ulong shown = seed % 1_000_000_000UL;

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:00}-{1:000}-{2:00}",
            shown / 10_000_000UL,
            shown / 10_000UL % 1_000UL,
            shown % 100UL);
    }

    /// <summary>The store in days rather than in units — a stock says nothing until it is set against the men.</summary>
    private static string Store(Resources purse, Resources draw)
    {
        if (draw.Food <= 0 || draw.Water <= 0)
        {
            return "empty, and nothing drawing on it";
        }

        int rice = purse.Food / draw.Food;
        int water = purse.Water / draw.Water;

        return $"rice {rice.ToString(CultureInfo.InvariantCulture)} days · "
            + $"water {water.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string Count(int many, string one, string more) =>
        $"{many.ToString(CultureInfo.InvariantCulture)} {(many == 1 ? one : more)}";
}
