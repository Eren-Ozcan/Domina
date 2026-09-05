using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// Günün karşılaşma teklifi (GDD §10). Korunan üç karar: günde <b>tek</b> teklif gelir,
/// teklif gün ve tohumun <b>saf</b> bir fonksiyonudur (kaydı yeniden yükleyerek teklif
/// değiştirilemez), ve sefer kaçılsa da <b>bir gün</b> yer.
/// </summary>
public class EncounterTests
{
    private static DojoState Funded(ulong seed = 7, int gold = 2000, EncounterTuning? encounters = null)
    {
        DojoState state = new(seed: seed, encounters: encounters);
        state.Resources = new Resources(Gold: gold);
        return state;
    }

    [Fact]
    public void TheSameDayAndSeedAlwaysGiveTheSameOffer()
    {
        EncounterGenerator generator = new();

        EncounterOffer first = generator.Offer(day: 12, campaignSeed: 99);
        EncounterOffer second = generator.Offer(day: 12, campaignSeed: 99);

        Assert.Equal(first.Threat, second.Threat);
        Assert.Equal(first.Sighting, second.Sighting);
        Assert.Equal(first.EnemyHealth, second.EnemyHealth);
        Assert.Equal(
            first.Enemies.Select(e => e.Name),
            second.Enemies.Select(e => e.Name));
    }

    [Fact]
    public void DifferentSeedsGiveDifferentCampaigns()
    {
        EncounterGenerator generator = new();

        List<string> one = [.. Enumerable.Range(1, 30).Select(d => generator.Offer(d, 1).Sighting)];
        List<string> other = [.. Enumerable.Range(1, 30).Select(d => generator.Offer(d, 2).Sighting)];

        Assert.NotEqual(one, other);
    }

    /// <summary>Zorluk tek eğri üzerinde artar (GDD §10) — boss takvimi yok.</summary>
    [Fact]
    public void LaterDaysBringHeavierEncounters()
    {
        EncounterGenerator generator = new();

        double early = Enumerable.Range(1, 40).Average(d => generator.Offer(d, 5).EnemyHealth);
        double late = Enumerable.Range(80, 40).Average(d => generator.Offer(d, 5).EnemyHealth);

        Assert.True(late > early * 1.5, $"eğri düz kalmış: {early:F0} → {late:F0}");
    }

    /// <summary>Dalgalanma olmasaydı "bırak" diye bir karar kalmazdı.</summary>
    [Fact]
    public void TheSameDayNumberIsNotTheSameEveryCampaign()
    {
        EncounterGenerator generator = new();

        List<ThreatBand> bands = [.. Enumerable.Range(1, 60).Select(d => generator.Offer(d, 3).Threat)];

        Assert.True(bands.Distinct().Count() > 1);
    }

    [Fact]
    public void DuelsDemandExactlyOneWarrior()
    {
        EncounterOffer duel = new(1, [Bestiary.Kappa.Spawn(new WarriorId(1), 1)], ThreatBand.Faint, "Kappa", 1);

        Assert.True(duel.Accepts(1));
        Assert.False(duel.Accepts(2));

        EncounterOffer open = duel with { RequiredPartySize = null };
        Assert.True(open.Accepts(4));
        Assert.False(open.Accepts(5));
        Assert.False(open.Accepts(0));
    }

    /// <summary>Kuvvetli yokai'ler eğrinin başında sahaya çıkmaz.</summary>
    [Fact]
    public void HeavyKindsWaitForTheirPlaceOnTheCurve()
    {
        Assert.DoesNotContain(Bestiary.Oni, Bestiary.AvailableAt(1.0));
        Assert.Contains(Bestiary.Kappa, Bestiary.AvailableAt(1.0));
        Assert.Contains(Bestiary.Oni, Bestiary.AvailableAt(2.0));
    }

    /// <summary>Güç canı ve hasarı doğrudan, isabet/kaçınmayı yumuşak büyütür.</summary>
    [Fact]
    public void PowerScalesTheBodyHarderThanTheSkill()
    {
        Warrior weak = Bestiary.Kappa.Spawn(new WarriorId(1), 1.0);
        Warrior strong = Bestiary.Kappa.Spawn(new WarriorId(2), 2.25);

        Assert.Equal(weak.BaseStats.MaxHealth * 2.25, strong.BaseStats.MaxHealth, 3);
        Assert.Equal(weak.BaseStats.Strength * 2.25, strong.BaseStats.Strength, 3);
        Assert.Equal(weak.BaseStats.Accuracy * 1.5, strong.BaseStats.Accuracy, 3);

        // Statlar 0-100 ölçeğinde; eğri büyüdükçe taşmamalı.
        Warrior extreme = Bestiary.Kappa.Spawn(new WarriorId(3), 100);
        Assert.InRange(extreme.BaseStats.Accuracy, 0, 95);
        Assert.InRange(extreme.BaseStats.Evasion, 0, 95);
    }

    [Fact]
    public void OneOfferPerDayAndItTurnsOverWithTheDay()
    {
        DojoState state = Funded();

        EncounterOffer today = state.Offer;
        Assert.Same(today, state.Offer);
        Assert.Equal(state.Day, today.Day);

        state.Decline();

        Assert.Equal(2, state.Day);
        Assert.Equal(2, state.Offer.Day);
    }

    /// <summary>Teklif kayıtta durmaz; gün ve tohumdan yeniden hesaplanır.</summary>
    [Fact]
    public void TheOfferSurvivesASaveRoundTrip()
    {
        DojoState state = Funded(seed: 4242);
        state.Roster.Recruit("Kenji");
        state.Decline();
        state.Decline();

        EncounterOffer before = state.Offer;
        Dojo.Save.LoadResult loaded = Dojo.Save.DojoSaveFile.Load(Dojo.Save.DojoSaveFile.Write(state));

        Assert.True(loaded.Succeeded);
        Assert.Equal(before.Day, loaded.State!.Offer.Day);
        Assert.Equal(before.Sighting, loaded.State.Offer.Sighting);
        Assert.Equal(before.EnemyHealth, loaded.State.Offer.EnemyHealth);
    }

    [Fact]
    public void AnExpeditionEatsTheDayAndPaysOnlyForVictory()
    {
        DojoState state = Funded(seed: 11);
        RosterEntry fighter = state.Roster.Recruit("Kenji", WarriorStats.Recruit(), Weapon.Katana(), Armor.Medium());

        int gold = state.Resources.Gold;
        EncounterOffer offer = state.Offer with { RequiredPartySize = null };
        ExpeditionResult result = new Expedition().Send(state, offer, [fighter], new SeededRandom(3));

        Assert.Equal(2, state.Day);
        Assert.Equal(1, result.Day.Day);

        if (result.Battle.Outcome == BattleOutcome.PlayerVictory)
        {
            Assert.True(result.Reward > 0);
        }
        else
        {
            Assert.Equal(0, result.Reward);
        }

        // Ödül kasaya girer; günün stok alışverişi ve varsa o günün aksiliği kasadan çıkar.
        int mishap = result.Day.Event?.Gold ?? 0;
        Assert.Equal(gold + result.Reward - result.Day.Upkeep.GoldSpent - mishap, state.Resources.Gold);
    }

    [Fact]
    public void TheInfirmaryAndTheCalendarBothBlockAnExpedition()
    {
        DojoState state = Funded();
        RosterEntry wounded = state.Roster.Recruit("Kenji");
        RosterEntry fit = state.Roster.Recruit("Hana");
        wounded.Injure(3);

        EncounterOffer offer = state.Offer with { RequiredPartySize = null };

        Assert.Equal(ExpeditionRefusal.Unfit, Expedition.Refuse(state, offer, [wounded]));
        Assert.Equal(ExpeditionRefusal.EmptyParty, Expedition.Refuse(state, offer, []));
        Assert.Null(Expedition.Refuse(state, offer, [fit]));

        // Dünün teklifine bugün girilemez.
        Assert.Equal(
            ExpeditionRefusal.StaleOffer,
            Expedition.Refuse(state, offer with { Day = state.Day - 1 }, [fit]));

        Assert.Equal(
            ExpeditionRefusal.WrongPartySize,
            Expedition.Refuse(state, offer with { RequiredPartySize = 3 }, [fit]));
    }

    [Fact]
    public void AWarriorFromAnotherDojoCannotBeSent()
    {
        DojoState state = Funded();
        DojoState other = Funded();
        RosterEntry stranger = other.Roster.Recruit("Kenji");

        EncounterOffer offer = state.Offer with { RequiredPartySize = null };

        Assert.Equal(ExpeditionRefusal.NotInRoster, Expedition.Refuse(state, offer, [stranger]));
    }
}
