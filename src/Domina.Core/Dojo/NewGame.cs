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

    /// <summary>
    /// The days of food and water the master left in the store.
    /// </summary>
    /// <remarks>
    /// GDD §11 (decision round, 2026-09-07): the store does <b>not</b> open empty. The first
    /// expedition is planned without going hungry and the supply pressure arrives on day 4 — opening
    /// day one with a supply crisis did not make the game harder, it made it <b>confusing</b>. The
    /// stock is counted in days against the roster actually left behind, not as a flat number, so a
    /// dojo of three and a dojo of five both open with the same amount of <b>time</b>.
    /// </remarks>
    public const int StartingStoreDays = 3;

    /// <summary>The smallest starting roster the master can have left behind.</summary>
    public const int FewestWarriors = 3;

    /// <summary>The largest — and the dojo's own ceiling in the story.</summary>
    public const int MostWarriors = 5;

    /// <summary>The middle of the range; it is what the economy was measured on.</summary>
    public const int StartingWarriors = 4;

    /// <summary>The mixer that separates the starting roster's stream from the day's market.</summary>
    private const ulong RosterSalt = 0xA5A5_5A5A_C3C3_3C3C;

    /// <summary>And the one that separates the province's deal from both.</summary>
    private const ulong ProvinceSalt = 0x3C3C_C3C3_5A5A_A5A5;

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

        // The province is dealt before the roster, off the run's own seed: how much of the map he
        // already holds is the other half of the run-to-run variety the design allows (docs/GDD.md §10).
        dojo.Province.Deal(new SeededRandom(seed ^ ProvinceSalt), difficulty.RivalShare);

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

        // The store is filled last, because what it holds is counted against the roster that was
        // actually left behind: three days of eating, whether the master left three men or five.
        int mouths = dojo.Roster.Living.Count();
        dojo.Resources = dojo.Resources with
        {
            Food = StartingStoreDays * mouths * dojo.Economy.FoodPerWarriorPerDay,
            Water = StartingStoreDays * mouths * dojo.Economy.WaterPerWarriorPerDay,
        };

        // The journal's opening line. Everything above this point follows from the seed and the tier, so
        // those two are the only things a replay needs to rebuild the dojo the player started with; the
        // starting roster is deliberately not written down, because writing it would let an old file
        // override a later draw (docs/GDD.md §2, the save's own rule).
        dojo.Journal.Start(seed, tier);

        return dojo;
    }
}
