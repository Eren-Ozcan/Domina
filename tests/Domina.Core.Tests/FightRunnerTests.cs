using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// The door the fight is being moved through (<see cref="IFightRunner"/>, docs/PLAN-ENGINE-RESOLUTION.md).
/// Two promises are pinned: the engine-free runner fights <b>exactly</b> the fight the resolver fights, so
/// putting the door in changed nothing a measurement can see; and the callers really go through the door
/// they are handed, so a second resolver behind it is a second resolver everywhere and not in half the places.
/// </summary>
public class FightRunnerTests
{
    private static BattleSetup ThreeVsThree() => new(
        [
            TestBuilders.Warrior(1, evasion: 30, defense: 20, armor: Armor.Light()),
            TestBuilders.Warrior(2, evasion: 20, defense: 30, weapon: Weapon.Nodachi(), armor: Armor.Medium()),
            TestBuilders.Warrior(3, evasion: 40, defense: 10, weapon: Weapon.Yari()),
        ],
        [
            TestBuilders.Warrior(101, "Oni", health: 140, aggression: 55, defense: 25, weapon: Weapon.Tetsubo()),
            TestBuilders.Warrior(102, "Kappa", health: 70, aggression: 65, evasion: 35),
            TestBuilders.Warrior(103, "Tengu", health: 80, aggression: 70, evasion: 50),
        ]);

    private static DojoState Dojo()
    {
        DojoState state = new(seed: 5)
        {
            Purse = new Resources(Gold: 500, Food: 50, Water: 50, Medicine: 5),
        };

        state.Roster.Recruit("Kenji", weapon: Weapon.Katana(), armor: Armor.Medium());
        state.Roster.Recruit("Hana", weapon: Weapon.Yari(), armor: Armor.Light());
        state.Roster.Recruit("Sora", weapon: Weapon.Katana(), armor: Armor.Light());
        return state;
    }

    private static List<RosterEntry> Party(DojoState dojo) =>
        [.. dojo.Roster.Living.Take(dojo.Offer.RequiredPartySize ?? 2)];

    [Theory]
    [InlineData(17ul)]
    [InlineData(20260806ul)]
    public void TheCoreRunnerFightsTheResolversFight(ulong seed)
    {
        var battle = new Battle(ThreeVsThree(), new SeededRandom(seed));
        BattleResult direct = battle.Run();

        BattleResult through = CoreFightRunner.Instance.Fight(ThreeVsThree(), new SeededRandom(seed));

        Assert.Equal(direct.Outcome, through.Outcome);
        Assert.Equal(direct.ElapsedSeconds, through.ElapsedSeconds, precision: 9);
        Assert.Equal(direct.Summaries, through.Summaries);
        Assert.Equal(battle.Events.Count, through.Events.Count);
    }

    [Fact]
    public void TheStreamComesBackOnTheResultOnlyWhenItWasAskedFor()
    {
        BattleResult collected = CoreFightRunner.Instance.Fight(ThreeVsThree(), new SeededRandom(17));
        BattleResult silent = CoreFightRunner.Instance.Fight(
            ThreeVsThree() with { CollectEvents = false },
            new SeededRandom(17));

        Assert.NotEmpty(collected.Events);
        Assert.IsType<BattleStarted>(collected.Events[0]);
        Assert.Empty(silent.Events);
    }

    [Fact]
    public void AnExpeditionFightsThroughTheRunnerItIsHanded()
    {
        var counting = new CountingRunner();
        DojoState dojo = Dojo();

        new Expedition(fighter: counting).Send(dojo, dojo.Offer, Party(dojo), 31ul);

        Assert.Equal(1, counting.Fights);
    }

    [Fact]
    public void TheLastNightFightsThroughTheRunnerItIsHanded()
    {
        var counting = new CountingRunner();
        SeasonTuning oneDay = new()
        {
            Days = 1,
            FinalRoundPowers = [0.2, 0.2, 0.2, 0.2, 0.2],
            FinalRoundEnemies = [1, 1, 1, 1, 1],
        };
        DojoState dojo = new(events: new EventTuning { ChancePerDay = 0 }, season: oneDay)
        {
            Purse = new Resources(Gold: 5000, Food: 500, Water: 500, Medicine: 40),
        };
        dojo.Roster.Recruit("Kenji", WarriorStats.Recruit());

        for (int i = 0; i < oneDay.BountyGate; i++)
        {
            dojo.Season.RecordHead();
        }

        while (dojo.Day <= oneDay.Days)
        {
            dojo.AdvanceDay();
        }

        new FinalNight(fighter: counting).Fight(dojo, [.. dojo.Roster.FitForCampaign], new SeededRandom(4));

        Assert.Equal(1, counting.Fights);
    }

    /// <summary>The engine-free resolver, counted: a stand-in for a second resolver behind the door.</summary>
    private sealed class CountingRunner : IFightRunner
    {
        public int Fights { get; private set; }

        public BattleResult Fight(BattleSetup setup, IRandomSource random)
        {
            Fights++;
            return CoreFightRunner.Instance.Fight(setup, random);
        }
    }
}
