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

        if (Refuse(state, offer, party) is ExpeditionRefusal refusal)
        {
            throw new InvalidOperationException($"Ekip sefere gönderilemez: {refusal}.");
        }

        BattleSetup setup = new([.. party.Select(e => e.Warrior)], offer.Enemies)
        {
            Tuning = tuning ?? CombatTuning.Default,
            RetreatPolicy = retreat,
            CollectEvents = collectEvents,
        };

        BattleResult battle = new Battle(setup, random).Run();
        AftermathReport aftermath = _aftermath.Apply(state, battle);

        int reward = state.Quartermaster.RewardFor(setup, battle.Outcome);
        state.Resources = state.Resources with { Gold = state.Resources.Gold + reward };

        DayReport day = state.AdvanceDay();
        return new ExpeditionResult(battle, aftermath, reward, day);
    }
}

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
