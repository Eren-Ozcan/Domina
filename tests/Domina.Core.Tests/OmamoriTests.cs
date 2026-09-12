using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Core.Tests;

/// <summary>
/// The temple charms and the funeral rite — the monk's system (docs/GDD.md §10). The decisions
/// protected here: no shrine, no charms; a charm is transferable and sellable at a loss; a dead man's
/// charms come home only if the field was won; and the rite softens a death without erasing it.
/// </summary>
public class OmamoriTests
{
    /// <summary>A lost fight in which one man of the two came back.</summary>
    private static BattleResult Loss(WarriorId survivor, WarriorId fallen) =>
        new(
            BattleOutcome.PlayerWipe,
            ElapsedSeconds: 20,
            [
                Line(survivor, died: false),
                Line(fallen, died: true),
            ]);

    private static WarriorBattleSummary Line(WarriorId id, bool died) =>
        new(
            id,
            "Test",
            Battle.PlayerTeam,
            died ? CombatState.Dead : CombatState.Idle,
            died ? 0 : 50,
            AttacksMade: 6,
            HitsLanded: 3,
            TimesHit: 2,
            DodgesPerformed: 0,
            DamageDealt: 0,
            DamageTaken: 0,
            LostLimb: false);

    private static DojoState Dojo(bool shrine = true, bool monk = false, int gold = 2000)
    {
        DojoState state = new(
            events: new EventTuning { ChancePerDay = 0 },
            school: new SchoolTuning { BuildDaysFactor = 0 })
        {
            Resources = new Resources(Gold: gold, Food: 200, Water: 200),
        };

        if (shrine)
        {
            state.BuySchoolNode(SchoolNodeId.Shrine);
        }

        if (monk)
        {
            state.Hire(StaffRole.Monk);
        }

        return state;
    }

    /// <summary>The omamori is the temple's supply: without the shrine there is nothing to buy.</summary>
    [Fact]
    public void WithoutTheShrineNoCharmCanBeBought()
    {
        DojoState state = Dojo(shrine: false);

        Assert.False(state.BuyCharm(OmamoriKind.SteadyHand));
        Assert.Empty(state.CharmStore);
        Assert.Equal(0, state.OmamoriSlots);
    }

    /// <summary>The empty shrine keeps one slot open; the monk opens the second.</summary>
    [Fact]
    public void TheMonkOpensTheSecondSlot()
    {
        Assert.Equal(1, Dojo().OmamoriSlots);
        Assert.Equal(2, Dojo(monk: true).OmamoriSlots);
    }

    /// <summary>A charm is fitted out of the store, and the slots are the cap.</summary>
    [Fact]
    public void ACharmLeavesTheStoreAndTheSlotsCapWhatIsWorn()
    {
        DojoState state = Dojo();
        RosterEntry entry = state.Roster.Recruit("Kenji");

        Assert.True(state.BuyCharm(OmamoriKind.SteadyHand));
        Assert.True(state.BuyCharm(OmamoriKind.IronGate));

        Assert.True(state.FitCharm(entry.Id, OmamoriKind.SteadyHand));
        Assert.Equal([OmamoriKind.SteadyHand], entry.Warrior.Charms);
        Assert.False(state.CharmStore.ContainsKey(OmamoriKind.SteadyHand));

        // One slot, and it is taken.
        Assert.False(state.FitCharm(entry.Id, OmamoriKind.IronGate));
    }

    /// <summary>The blessing is points on one stat, and it comes off with the charm.</summary>
    [Fact]
    public void TheBlessingIsAddedAndTakenBackWithTheCharm()
    {
        DojoState state = Dojo();
        RosterEntry entry = state.Roster.Recruit("Kenji");
        double before = entry.Warrior.EffectiveStats.Accuracy;

        state.BuyCharm(OmamoriKind.SteadyHand);
        state.FitCharm(entry.Id, OmamoriKind.SteadyHand);

        Assert.Equal(before + Omamori.StatBonus, entry.Warrior.EffectiveStats.Accuracy, 6);

        Assert.True(state.UnfitCharm(entry.Id, OmamoriKind.SteadyHand));
        Assert.Equal(before, entry.Warrior.EffectiveStats.Accuracy, 6);
        Assert.Equal(1, state.CharmStore[OmamoriKind.SteadyHand]);
    }

    /// <summary>Selling a charm back gives a share of its price, never the whole of it.</summary>
    [Fact]
    public void TheTempleBuysACharmBackAtALoss()
    {
        DojoState state = Dojo();
        OmamoriCharm charm = Omamori.Find(OmamoriKind.QuietMind);

        state.BuyCharm(charm.Kind);
        int afterBuying = state.Resources.Gold;

        int back = state.SellCharm(charm.Kind);

        Assert.True(back > 0);
        Assert.True(back < charm.Price);
        Assert.Equal(afterBuying + back, state.Resources.Gold);
        Assert.Equal(0, state.SellCharm(charm.Kind));
    }

    /// <summary>A man walked out of the gate leaves the dojo's charms behind.</summary>
    [Fact]
    public void AReleasedManLeavesHisCharmsInTheStore()
    {
        DojoState state = Dojo();
        RosterEntry entry = state.Roster.Recruit("Kenji");

        state.BuyCharm(OmamoriKind.SwiftFoot);
        state.FitCharm(entry.Id, OmamoriKind.SwiftFoot);

        Assert.True(state.Release(entry.Id));
        Assert.Empty(entry.Warrior.Charms);
        Assert.Equal(1, state.CharmStore[OmamoriKind.SwiftFoot]);
    }

    /// <summary>A dead man's charms come home only if somebody won the field.</summary>
    [Fact]
    public void ADeadMansCharmsComeHomeOnlyFromAFieldThatWasWon()
    {
        int InStoreAfter(BattleOutcome outcome)
        {
            DojoState state = Dojo();
            RosterEntry entry = state.Roster.Recruit("Kenji");

            state.BuyCharm(OmamoriKind.IronGate);
            state.FitCharm(entry.Id, OmamoriKind.IronGate);

            BattleResult result = new(outcome, ElapsedSeconds: 20, [Line(entry.Id, died: true)]);
            new BattleAftermath().Apply(state, result);

            Assert.False(entry.Warrior.IsAlive);
            Assert.Empty(entry.Warrior.Charms);
            return state.CharmStore.GetValueOrDefault(OmamoriKind.IronGate);
        }

        Assert.Equal(1, InStoreAfter(BattleOutcome.PlayerVictory));
        Assert.Equal(0, InStoreAfter(BattleOutcome.PlayerWipe));
    }

    /// <summary>The rite softens a comrade's death; it never takes the whole blow away.</summary>
    [Fact]
    public void TheFuneralRiteSoftensADeathWithoutErasingIt()
    {
        double MoraleAfterALoss(bool shrine)
        {
            DojoState state = Dojo(shrine, monk: shrine);
            RosterEntry survivor = state.Roster.Recruit("Kenji");
            RosterEntry fallen = state.Roster.Recruit("Goro");

            new BattleAftermath().Apply(state, Loss(survivor.Id, fallen.Id));

            return survivor.Warrior.Morale;
        }

        double bare = MoraleAfterALoss(shrine: false);
        double blessed = MoraleAfterALoss(shrine: true);

        Assert.True(blessed > bare);
        Assert.True(blessed < MoraleScale.Starting);
    }
}
