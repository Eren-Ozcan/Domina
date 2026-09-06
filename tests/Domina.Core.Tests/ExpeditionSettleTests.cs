using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Seferin iki yolu: dövüşü arka planda çözen <see cref="Expedition.Send"/> ve arenanın
/// kullandığı <c>Prepare</c> + <c>Settle</c>. Korunan karar: <b>ikisi aynı sonucu
/// vermeli</b>. Arena kendi muhasebesini yazsaydı izlenen dövüş ile simüle edilen dövüş
/// ayrışır ve denge ölçümü ekrandakini ölçmemiş olurdu.
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

    /// <summary>Teklifin istediği büyüklükte ekip — bazı teklifler tam sayı dayatıyor.</summary>
    private static List<RosterEntry> Party(DojoState dojo) =>
        [.. dojo.Roster.Living.Take(dojo.Offer.RequiredPartySize ?? 2)];

    /// <summary>Aynı tohum, aynı ekip, aynı teklif: iki yol tek sonuç.</summary>
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

    /// <summary>Kurulum dövüşü koşturmaz: gün de kadro da olduğu yerde kalır.</summary>
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

    /// <summary>Uygun olmayan ekip kurulumda durur — dövüş hiç kurulmaz.</summary>
    [Fact]
    public void PrepareRefusesAnUnfitParty()
    {
        DojoState dojo = Dojo();
        RosterEntry wounded = dojo.Roster.Living.First();
        wounded.Injure(3);

        Assert.Throws<InvalidOperationException>(
            () => Expedition.Prepare(dojo, dojo.Offer, [wounded]));
    }

    /// <summary>Arena çekilme komutunu kullanabilsin diye kurulum olay akışını taşıyabilir.</summary>
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
