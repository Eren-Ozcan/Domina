using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Presentation;

/// <summary>Günün teklifi — ekranın okuduğu hâliyle.</summary>
/// <remarks>
/// Düşman kadrosu burada <b>yok</b>. Teklif nesnesi onu taşır (dövüş aynı kadroyla
/// kurulsun diye) ama ekran görmemeli: GDD §10'a göre girmeden önce yalnızca bant ve
/// kaba tanım okunur. Modelin kadroyu hiç taşımaması, ekranın onu yanlışlıkla
/// basmasını da imkânsız kılar.
/// </remarks>
/// <param name="Day">Teklifin geçerli olduğu gün.</param>
/// <param name="Threat">Okunabilen tehdit bandı.</param>
/// <param name="Sighting">Kaba tanım ("üç kappa" gibi).</param>
/// <param name="RequiredPartySize">Dayatılan ekip büyüklüğü; yoksa <c>null</c>.</param>
/// <param name="MaxPartySize">Sefere çıkabilecek azami savaşçı.</param>
/// <param name="PromisedReward">Kazanılırsa ödenecek altın — girmeden önce okunabilir.</param>
public readonly record struct OfferCard(
    int Day,
    ThreatBand Threat,
    string Sighting,
    int? RequiredPartySize,
    int MaxPartySize,
    int PromisedReward);

/// <summary>Tahtadaki sözleşme — ekranın okuduğu hâliyle.</summary>
/// <param name="TargetName">Hedefin adı; sözleşme isimli tek bir yaratığa yazılır.</param>
/// <param name="Patron">Sözleşmeyi veren taraf.</param>
/// <param name="Threat">Okunabilen tehdit bandı.</param>
/// <param name="Reward">Söz verilen altın.</param>
/// <param name="DaysLeft">Son gün dahil kalan gün.</param>
/// <param name="HonorReward">Kelle getirilirse ekibin kazandığı onur.</param>
/// <param name="BrokenHonorPenalty">Söz tutulmazsa <b>kadronun</b> kaybettiği onur.</param>
/// <param name="Accepted">Söz verilmiş mi?</param>
public readonly record struct BountyCard(
    string TargetName,
    string Patron,
    ThreatBand Threat,
    int Reward,
    int DaysLeft,
    double HonorReward,
    double BrokenHonorPenalty,
    bool Accepted);

/// <summary>Sefere gönderilebilecek adayın satırı.</summary>
/// <param name="Id">Savaşçının kimliği; ekran komutu bunu geri verir.</param>
/// <param name="Name">Görünen ad.</param>
/// <param name="Fit">Bugün gönderilebilir mi?</param>
/// <param name="RecoveryDaysRemaining">Revirde kalan gün.</param>
/// <param name="Score">Toplam stat skoru — <b>etkin</b> statlardan, sakatlık dahil.</param>
public readonly record struct PartyCandidate(
    WarriorId Id,
    string Name,
    bool Fit,
    int RecoveryDaysRemaining,
    double Score);

/// <summary>Seçili ekiple sefere çıkma hükmü.</summary>
/// <param name="Refusal">Reddin sebebi; gönderilebiliyorsa <c>null</c>.</param>
/// <param name="Size">Seçili savaşçı sayısı.</param>
public readonly record struct PartyVerdict(ExpeditionRefusal? Refusal, int Size)
{
    /// <summary>Ekip bugün gönderilebilir mi?</summary>
    public bool CanSend => Refusal is null;
}

/// <summary>
/// Günün teklifi ekranının okuduğu model. Ne görüneceğine ve seferin
/// <b>reddedileceğini önceden bilmeye</b> karar verir, çizim yapmaz.
/// </summary>
/// <remarks>
/// <see cref="Expedition.Send"/> uygun olmayan ekipte fırlatır; oyuncu bunu istisnadan
/// değil sönük tuştan öğrenmeli. Hüküm <see cref="Expedition.Refuse"/>'un kendisinden
/// okunuyor — ekran ikinci bir kural kümesi yazsaydı iki taraf ayrışır ve tuş
/// gönderilebilen bir seferi kapatmaya başlardı.
/// </remarks>
public static class OfferModel
{
    /// <summary>Bugünün teklifi.</summary>
    public static OfferCard Describe(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        EncounterOffer offer = dojo.Offer;

        return new OfferCard(
            Day: offer.Day,
            Threat: offer.Threat,
            Sighting: offer.Sighting,
            RequiredPartySize: offer.RequiredPartySize,
            MaxPartySize: EncounterOffer.MaxPartySize,
            PromisedReward: dojo.Quartermaster.PromisedReward(new BattleSetup([], offer.Enemies)));
    }

    /// <summary>Bugün tahtada sözleşme varsa kartı, yoksa <c>null</c>.</summary>
    public static BountyCard? DescribeBounty(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        if (dojo.Bounty is not BountyContract contract)
        {
            return null;
        }

        return new BountyCard(
            TargetName: contract.Target.Name,
            Patron: contract.Patron,
            Threat: contract.Threat,
            Reward: contract.Reward,
            DaysLeft: contract.DaysLeft(dojo.Day),
            HonorReward: contract.HonorReward,
            BrokenHonorPenalty: contract.BrokenHonorPenalty,
            Accepted: dojo.AcceptedBountyDay == contract.PostedDay);
    }

    /// <summary>Sefere gönderilebilecekler önce, sonra revirdekiler.</summary>
    /// <remarks>
    /// Ölüler hiç listelenmez: kadro ekranında kayıt olarak dururlar, burada seçenek
    /// değiller. Revirdekiler <b>görünür ama seçilemez</b> — "kimse yok" ile "herkes
    /// yatakta" aynı ekran olmamalı.
    /// </remarks>
    public static IReadOnlyList<PartyCandidate> Candidates(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        return dojo.Roster.Living
            .Select(entry => new PartyCandidate(
                entry.Id,
                entry.Name,
                entry.IsFitForCampaign,
                entry.RecoveryDaysRemaining,
                MarketModel.Score(entry.Warrior.EffectiveStats)))
            .OrderByDescending(c => c.Fit)
            .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Seçili ekip günün teklifine gönderilebilir mi?</summary>
    public static PartyVerdict Judge(DojoState dojo, IReadOnlyList<WarriorId> party)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        return Judge(dojo, dojo.Offer, party);
    }

    /// <summary>Seçili ekip sözleşmenin üstüne gönderilebilir mi?</summary>
    /// <remarks>
    /// Sözleşmeye <b>kabul etmeden de</b> girilebilir (bkz.
    /// <see cref="Expedition.SendToBounty"/>), o yüzden hüküm söze değil güne bakar:
    /// süresi geçmiş sözleşme gönderilemez.
    /// </remarks>
    public static PartyVerdict JudgeBounty(
        DojoState dojo,
        BountyContract contract,
        IReadOnlyList<WarriorId> party)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(party);

        return contract.IsOpenOn(dojo.Day)
            ? Judge(dojo, contract.AsOffer(dojo.Day), party)
            : new PartyVerdict(ExpeditionRefusal.StaleOffer, party.Count);
    }

    /// <summary>Seçili kimlikleri kadro kayıtlarına çevirir — sefer katmanının istediği biçim.</summary>
    /// <remarks>Kadroda bulunmayan kimlik sessizce düşmez, hüküm onu reddeder.</remarks>
    public static IReadOnlyList<RosterEntry> Party(DojoState dojo, IReadOnlyList<WarriorId> party)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        ArgumentNullException.ThrowIfNull(party);

        return [.. party.Select(dojo.Roster.Find).OfType<RosterEntry>()];
    }

    private static PartyVerdict Judge(
        DojoState dojo,
        EncounterOffer offer,
        IReadOnlyList<WarriorId> party)
    {
        ArgumentNullException.ThrowIfNull(party);

        // Kayıp kimlik listeden düşerse ekip küçülmüş görünür ve hüküm yanlış sebebi
        // yazar; sayı seçilenden okunuyor, kadro kaydı ise bulunabildiği kadarıyla.
        List<RosterEntry> entries = [.. Party(dojo, party)];
        if (entries.Count != party.Count)
        {
            return new PartyVerdict(ExpeditionRefusal.NotInRoster, party.Count);
        }

        return new PartyVerdict(Expedition.Refuse(dojo, offer, entries), party.Count);
    }
}
