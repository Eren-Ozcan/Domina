using Domina.Core.Campaign;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Dojo;

/// <summary>A new game's first day.</summary>
/// <remarks>
/// <para>
/// The starting state is built <b>in the core</b>, not on screen: the same construction must be used by
/// the measurement run, the game and the tests. Built on screen, the dojo being played and the dojo
/// whose balance is measured would quietly drift apart.
/// </para>
/// <para>
/// The numbers are the same as the measured setup (GDD §11): <b>600 gold</b> and an empty store — the
/// first day's food is bought by the day's closing. The roster is <b>drawn</b> rather than fixed:
/// docs/STORY.md gives the dead master 3-5 men, and no two seasons should open the same way. The
/// measurement's <c>RosterTarget</c> of four sits in the middle of that range, so the economy that was
/// measured is still the economy being played — what changes is how much of a cushion the run starts
/// with, which is one of the few things a fixed opening was quietly deciding for the player.
/// </para>
/// <para>
/// The starting roster is drawn from the market's own generator rather than from hand-written warriors:
/// so that the same statistical distribution feeds both the stall and the first roster. The stream is
/// derived from <b>a separate seed</b>, or the candidates standing at the stall on the first day would
/// be an exact copy of the roster.
/// </para>
/// </remarks>
public static class NewGame
{
    /// <summary>The starting purse — GDD §11.</summary>
    public const int StartingGold = 600;

    /// <summary>The smallest starting roster the master can have left behind.</summary>
    public const int FewestWarriors = 3;

    /// <summary>The largest — and the dojo's own ceiling in the story.</summary>
    public const int MostWarriors = 5;

    /// <summary>The middle of the range; it is what the economy was measured on.</summary>
    public const int StartingWarriors = 4;

    /// <summary>The mixer that separates the starting roster's stream from the day's market.</summary>
    private const ulong RosterSalt = 0xA5A5_5A5A_C3C3_3C3C;

    /// <summary>Builds a new dojo from the given seed.</summary>
    /// <param name="seed">The expedition's seed; the same seed gives the same start.</param>
    /// <param name="tuning">The day-loop settings; the default if not given.</param>
    /// <param name="tier">
    /// The difficulty tier. Master is the measured one and the default — every number in the docs is
    /// literally the number the game runs at Master.
    /// </param>
    public static DojoState Create(
        ulong seed,
        DojoTuning? tuning = null,
        DifficultyTier tier = DifficultyTier.Master)
    {
        Difficulty difficulty = Difficulty.Of(tier);

        DojoState dojo = new(
            tuning,
            difficulty.Apply(new EconomyTuning()),
            seed,
            difficulty.Apply(new EncounterTuning()),
            difficulty: tier)
        {
            Resources = new Resources(Gold: StartingGold),
        };

        SeededRandom random = new(seed ^ RosterSalt);

        // How many the master left is the first thing the season decides, and it is decided before the
        // men themselves are drawn: the same seed must always open the same dojo.
        int men = FewestWarriors + random.NextInt(MostWarriors - FewestWarriors + 1);

        IReadOnlyList<RecruitOffer> stock = dojo.Market.Stock(
            random,
            WarriorStats.Recruit(),
            dojo.Economy.RecruitPrice);

        for (int i = 0; i < men && i < stock.Count; i++)
        {
            RecruitOffer offer = stock[i];
            string name = offer.Name;
            for (int suffix = 2; dojo.Roster.IsNameTaken(name); suffix++)
            {
                name = $"{offer.Name} {suffix}";
            }

            // The starting roster is free: the 600 gold stands for the first day's decisions, not for the
            // roster itself.
            dojo.Roster.Recruit(name, offer.Stats, Weapon.Katana(), Armor.Light(), offer.Talent);
        }

        return dojo;
    }
}
