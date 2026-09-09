using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// The expedition's two routes: <see cref="Expedition.Send"/>, which resolves the fight in the
/// background, and the <c>Prepare</c> + <c>Settle</c> the arena uses. The decision protected: <b>the two
/// must give the same result</b>. If the arena wrote its own books, a watched fight and a simulated one
/// would drift apart and balance measurement would not be measuring what is on screen.
/// </summary>
public class ExpeditionSettleTests
{
    private static DojoState Dojo(ulong seed = 5)
    {
        DojoState state = new(seed: seed)
        {
            Resources = new Resources(Gold: 500, Food: 50, Water: 50, Medicine: 5),
        };

        state.Roster.Recruit("Kenji", WarriorStats.Recruit() with { Strength = 55 }, Weapon.Katana(), Armor.Medium());
        state.Roster.Recruit("Hana", weapon: Weapon.Yari(), armor: Armor.Light());
        state.Roster.Recruit("Sora", weapon: Weapon.Katana(), armor: Armor.Light());
        state.Roster.Recruit("Ren", weapon: Weapon.Katana(), armor: Armor.Light());
        return state;
    }

    /// <summary>A party of the size the offer wants — some offers impose an exact number.</summary>
    private static List<RosterEntry> Party(DojoState dojo) =>
        [.. dojo.Roster.Living.Take(dojo.Offer.RequiredPartySize ?? 2)];

    /// <summary>The same seed, the same party, the same offer: two routes, one result.</summary>
    [Fact]
    public void WatchingTheBattleLeavesTheSameDojoAsResolvingIt()
    {
        DojoState resolved = Dojo();
        DojoState watched = Dojo();

        ExpeditionResult direct = new Expedition().Send(
            resolved,
            resolved.Offer,
            Party(resolved),
            new SeededRandom(99));

        BattleSetup setup = Expedition.Prepare(watched, watched.Offer, Party(watched));
        BattleResult battle = new Battle(setup, new SeededRandom(99)).Run();
        ExpeditionResult settled = new Expedition().Settle(watched, setup, battle);

        Assert.Equal(direct.Battle.Outcome, settled.Battle.Outcome);
        Assert.Equal(direct.Reward, settled.Reward);
        Assert.Equal(direct.Day.Day, settled.Day.Day);
        Assert.Equal(resolved.Resources.Gold, watched.Resources.Gold);
        Assert.Equal(
            resolved.Roster.Entries.Select(e => (e.Name, e.Warrior.IsAlive, e.RecoveryDaysRemaining)),
            watched.Roster.Entries.Select(e => (e.Name, e.Warrior.IsAlive, e.RecoveryDaysRemaining)));
    }

    /// <summary>The setup does not run the fight: both the day and the roster stay where they were.</summary>
    [Fact]
    public void PreparingDoesNotTouchTheDojo()
    {
        DojoState dojo = Dojo();
        int day = dojo.Day;
        int gold = dojo.Resources.Gold;

        BattleSetup setup = Expedition.Prepare(dojo, dojo.Offer, Party(dojo));

        Assert.Equal(day, dojo.Day);
        Assert.Equal(gold, dojo.Resources.Gold);
        Assert.Equal(Party(dojo).Count, setup.PlayerSide.Count);
        Assert.Equal(dojo.Offer.Enemies, setup.EnemySide);
    }

    /// <summary>An unfit party stops at the setup — the fight is never built.</summary>
    [Fact]
    public void PrepareRefusesAnUnfitParty()
    {
        DojoState dojo = Dojo();
        RosterEntry wounded = dojo.Roster.Living.First();
        wounded.Injure(3);

        Assert.Throws<InvalidOperationException>(
            () => Expedition.Prepare(dojo, dojo.Offer, [wounded]));
    }

    /// <summary>The setup can carry the event stream so the arena can use the retreat command.</summary>
    [Fact]
    public void PrepareCanBeAskedForTheEventStream()
    {
        DojoState dojo = Dojo();

        BattleSetup setup = Expedition.Prepare(
            dojo,
            dojo.Offer,
            Party(dojo),
            collectEvents: true);

        Assert.True(setup.CollectEvents);
    }
}
