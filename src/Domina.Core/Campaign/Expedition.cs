using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Core.Campaign;

/// <summary>Teklifi kabul edip dövüşü kuran katman.</summary>
/// <remarks>
/// <para>
/// Dojo ile dövüş çözümleyicisi arasındaki tek köprü burası. <see cref="DojoState"/>
/// dövüşü kurmuyor, <see cref="Battle"/> de günü kapatmıyor — ikisini tek sınıfta
/// birleştirmek, çekirdeği motorsuz koşturulabilir tutan ayrımı bozardı.
/// </para>
/// <para>
/// Sefer <b>bir gün yer</b> (GDD §10) ve bu gün kaçılsa da yenir. Günü kapatmak
/// çağıranın işi değil: <see cref="Send"/> dövüşü koşturur, sonucu kadroya yazar ve
/// günü kendi kapatır, çünkü "gir-bak-kaç" döngüsünü kapatan kalem tam olarak budur.
/// </para>
/// </remarks>
public sealed class Expedition(BattleAftermath? aftermath = null)
{
    private readonly BattleAftermath _aftermath = aftermath ?? new BattleAftermath();

    /// <summary>Ekip sefere gönderilebilir mi — ve gönderilemiyorsa neden?</summary>
    public static ExpeditionRefusal? Refuse(DojoState state, EncounterOffer offer, IReadOnlyList<RosterEntry> party)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(party);

        if (party.Count == 0)
        {
            return ExpeditionRefusal.EmptyParty;
        }

        if (offer.Day != state.Day)
        {
            return ExpeditionRefusal.StaleOffer;
        }

        if (!offer.Accepts(party.Count))
        {
            return ExpeditionRefusal.WrongPartySize;
        }

        foreach (RosterEntry entry in party)
        {
            if (state.Roster.Find(entry.Id) is null)
            {
                return ExpeditionRefusal.NotInRoster;
            }

            if (!entry.IsFitForCampaign)
            {
                return ExpeditionRefusal.Unfit;
            }
        }

        return null;
    }

    /// <summary>
    /// Ekibi teklifin üstüne gönderir: dövüşü koşturur, sonucu kadroya yazar, ödülü öder
    /// ve günü kapatır.
    /// </summary>
    /// <remarks>
    /// Dövüşü <b>izlenmeden</b> çözen yol budur (toplu simülasyon, testler). Arena
    /// dövüşü kendi adımlıyor; o zaman <see cref="Prepare"/> ile kurulup
    /// <see cref="Settle"/> ile kapatılır. İkisi de aynı üç adımı yapar — kurulum,
    /// dövüş, muhasebe — ve muhasebe tek bir yerde durur.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Ekip sefere uygun değilse.</exception>
    public ExpeditionResult Send(
        DojoState state,
        EncounterOffer offer,
        IReadOnlyList<RosterEntry> party,
        Rng.IRandomSource random,
        CombatTuning? tuning = null,
        IRetreatPolicy? retreat = null,
        bool collectEvents = false)
    {
        ArgumentNullException.ThrowIfNull(random);

        BattleSetup setup = Prepare(state, offer, party, tuning, retreat, collectEvents);
        return Settle(state, setup, new Battle(setup, random).Run());
    }

    /// <summary>
    /// Seferin dövüşünü kurar ama <b>koşturmaz</b>.
    /// </summary>
    /// <remarks>
    /// Arena için gerekli: oyuncu dövüşü izlerken müdahale edebildiği (çekilme komutu)
    /// için dövüş gerçek zamanla adımlanmalı, önceden koşturulup kaydı oynatılmamalı.
    /// Kurulumun burada durması, izlenen dövüş ile arka planda çözülen dövüşün
    /// <b>aynı</b> girdilerden çıkmasını garanti eder.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Ekip sefere uygun değilse.</exception>
    public static BattleSetup Prepare(
        DojoState state,
        EncounterOffer offer,
        IReadOnlyList<RosterEntry> party,
        CombatTuning? tuning = null,
        IRetreatPolicy? retreat = null,
        bool collectEvents = false)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(party);

        if (Refuse(state, offer, party) is ExpeditionRefusal refusal)
        {
            throw new InvalidOperationException($"Ekip sefere gönderilemez: {refusal}.");
        }

        return new BattleSetup([.. party.Select(e => e.Warrior)], offer.Enemies)
        {
            Tuning = tuning ?? CombatTuning.Default,
            RetreatPolicy = retreat,
            CollectEvents = collectEvents,
        };
    }

    /// <summary>
    /// Bitmiş bir dövüşün hesabını kapatır: kadroya yazar, ödülü öder, günü kapatır.
    /// </summary>
    /// <remarks>
    /// Dövüşün <b>nerede</b> koştuğu burayı ilgilendirmez — arenada izlenmiş de olabilir,
    /// <see cref="Send"/> içinde çözülmüş de. Muhasebenin tek yerde durması şart: arena
    /// kendi muhasebesini yazsaydı izlenen dövüş ile simüle edilen dövüş farklı sonuçlar
    /// bırakır ve denge ölçümü ekrandakini ölçmemiş olurdu.
    /// </remarks>
    public ExpeditionResult Settle(DojoState state, BattleSetup setup, BattleResult battle)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(battle);

        AftermathReport aftermath = _aftermath.Apply(state, battle);

        int reward = state.Quartermaster.RewardFor(setup, battle.Outcome);
        state.Resources = state.Resources with { Gold = state.Resources.Gold + reward };

        DayReport day = state.AdvanceDay();
        return new ExpeditionResult(battle, aftermath, reward, day);
    }

    /// <summary>
    /// Ekibi kelle avı sözleşmesinin üstüne gönderir.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sıradan seferden ayrılan üç kalem burada: ödül sözleşmenin <b>söz verdiği</b>
    /// rakamdır (dövüşün canından değil), sözleşme tamamlanınca ekip onur kazanır, ve
    /// dönülemezse söz kırılır. Dövüşün kendisi aynı yoldan geçer — hedef tek bir düşman
    /// olarak sefer katmanına verilir, çünkü dövüşe giden ikinci bir kapı açmak
    /// çözümleyiciyi ikiye bölerdi.
    /// </para>
    /// <para>
    /// Kabul edilmemiş sözleşmeye de girilebilir: tahtadan işi görüp aynı gün gitmek
    /// meşru. Kabul, gün kazandırmaz — <b>süre</b> satın alır.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Sözleşme bugün açık değilse ya da ekip sefere uygun değilse.
    /// </exception>
    public BountyResult SendToBounty(
        DojoState state,
        BountyContract contract,
        IReadOnlyList<RosterEntry> party,
        Rng.IRandomSource random,
        CombatTuning? tuning = null,
        IRetreatPolicy? retreat = null,
        bool collectEvents = false)
    {
        ArgumentNullException.ThrowIfNull(random);

        BattleSetup setup = PrepareBounty(state, contract, party, tuning, retreat, collectEvents);
        return SettleBounty(state, contract, party, setup, new Battle(setup, random).Run());
    }

    /// <summary>Sözleşmenin dövüşünü kurar ama koşturmaz — bkz. <see cref="Prepare"/>.</summary>
    /// <exception cref="InvalidOperationException">
    /// Sözleşme bugün açık değilse ya da ekip sefere uygun değilse.
    /// </exception>
    public static BattleSetup PrepareBounty(
        DojoState state,
        BountyContract contract,
        IReadOnlyList<RosterEntry> party,
        CombatTuning? tuning = null,
        IRetreatPolicy? retreat = null,
        bool collectEvents = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(contract);

        if (!contract.IsOpenOn(state.Day))
        {
            throw new InvalidOperationException("Sözleşme bugün açık değil.");
        }

        return Prepare(state, contract.AsOffer(state.Day), party, tuning, retreat, collectEvents);
    }

    /// <summary>Bitmiş bir kelle avının hesabını kapatır — bkz. <see cref="Settle"/>.</summary>
    public BountyResult SettleBounty(
        DojoState state,
        BountyContract contract,
        IReadOnlyList<RosterEntry> party,
        BattleSetup setup,
        BattleResult battle)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(battle);

        AftermathReport aftermath = _aftermath.Apply(state, battle);

        bool claimed = battle.Outcome == BattleOutcome.PlayerVictory;
        int reward = claimed ? contract.Reward : state.Quartermaster.Economy.LostBattleGold;
        state.Resources = state.Resources with { Gold = state.Resources.Gold + reward };

        if (claimed)
        {
            // Onur sefere <b>giden</b> ekibe yazılır, kadronun tamamına değil: kelleyi
            // getiren onlar. Kırılan sözün cezası ise tüm kadroya yazılıyor, çünkü sözü
            // dojo veriyor — kazanç kişisel, borç ortak.
            foreach (RosterEntry entry in party)
            {
                if (state.Roster.Find(entry.Id) is RosterEntry alive && alive.Warrior.IsAlive)
                {
                    alive.Warrior.Honor =
                        HonorScale.Clamp(alive.Warrior.Honor + contract.HonorReward);
                }
            }

            state.CloseBounty(contract.PostedDay);
        }

        DayReport day = state.AdvanceDay();
        return new BountyResult(contract, battle, aftermath, reward, claimed, day);
    }
}

/// <summary>Bir kelle avının dojo'ya dönmüş hâli.</summary>
/// <param name="Contract">Girilen sözleşme.</param>
/// <param name="Battle">Dövüşün ham sonucu.</param>
/// <param name="Aftermath">Kadroya yazılanlar.</param>
/// <param name="Reward">Kasaya giren altın.</param>
/// <param name="Claimed">Kelle alındı mı — sözleşme tamamlandı mı.</param>
/// <param name="Day">Seferin yediği günün özeti.</param>
public sealed record BountyResult(
    BountyContract Contract,
    BattleResult Battle,
    AftermathReport Aftermath,
    int Reward,
    bool Claimed,
    DayReport Day);

/// <summary>Bir seferin dojo'ya dönmüş hâli.</summary>
/// <param name="Battle">Dövüşün ham sonucu.</param>
/// <param name="Aftermath">Kadroya yazılanlar.</param>
/// <param name="Reward">Kasaya giren altın (çekilme ve bozgunda 0).</param>
/// <param name="Day">Seferin yediği günün özeti.</param>
public sealed record ExpeditionResult(
    BattleResult Battle,
    AftermathReport Aftermath,
    int Reward,
    DayReport Day);

/// <summary>Seferin reddedilme sebebi.</summary>
public enum ExpeditionRefusal
{
    /// <summary>Kimse seçilmedi.</summary>
    EmptyParty,

    /// <summary>Teklif bugünün teklifi değil — dün kabul edilmiş bir teklife girilemez.</summary>
    StaleOffer,

    /// <summary>Encounter başka bir sayı dayatıyor (düello gibi) ya da üst sınır aşıldı.</summary>
    WrongPartySize,

    /// <summary>Savaşçı bu kadroda değil.</summary>
    NotInRoster,

    /// <summary>Savaşçı revirde ya da ölü.</summary>
    Unfit,
}
