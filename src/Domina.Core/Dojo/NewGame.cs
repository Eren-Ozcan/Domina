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
/// The numbers are the same as the measured setup (GDD §11): <b>600 gold</b>, an empty store — the
/// first day's food is bought by the day's closing — and a roster of four. The roster size is the same
/// as the measurement's <c>RosterTarget</c>; another number would cut the measured economy loose from
/// the game being played.
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

    /// <summary>The size of the starting roster.</summary>
    public const int StartingWarriors = 4;

    /// <summary>The mixer that separates the starting roster's stream from the day's market.</summary>
    private const ulong RosterSalt = 0xA5A5_5A5A_C3C3_3C3C;

    /// <summary>Verilen tohumdan yeni bir dojo kurar.</summary>
    /// <param name="seed">The expedition's seed; the same seed gives the same start.</param>
    /// <param name="tuning">The day-loop settings; the default if not given.</param>
    public static DojoState Create(ulong seed, DojoTuning? tuning = null)
    {
        DojoState dojo = new(tuning, seed: seed)
        {
            Resources = new Resources(Gold: StartingGold),
        };

        SeededRandom random = new(seed ^ RosterSalt);
        IReadOnlyList<RecruitOffer> stock = dojo.Market.Stock(
            random,
            WarriorStats.Recruit(),
            dojo.Economy.RecruitPrice);

        for (int i = 0; i < StartingWarriors && i < stock.Count; i++)
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
