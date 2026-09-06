using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// Günün ekranı: teklif, sözleşme, ekip seçimi ve günü kapatan tuşlar (GDD §10).
/// </summary>
/// <remarks>
/// <para>
/// Ne görüneceğine ve seferin reddedilip reddedilmeyeceğine <see cref="OfferModel"/>
/// karar verir (motorsuz, testli). Dövüşü kurup günü kapatan taraf
/// <see cref="Expedition"/> — bu ekran ikinci bir kapı açmıyor.
/// </para>
/// <para>
/// Düşman kadrosu ekranda <b>yok</b>: girmeden önce yalnızca tehdit bandı ve kaba tanım
/// okunur. Model kadroyu hiç taşımıyor, o yüzden buradan yanlışlıkla da basılamaz.
/// </para>
/// <para>
/// Dövüş <b>arenada izleniyor</b>: ekran <c>Expedition.Prepare</c> ile dövüşü kurar ve
/// <see cref="Watcher"/>'a verir; arena onu gerçek zamanla adımlar, biten dövüşün ham
/// sonucu <c>Expedition.Settle</c> ile kadroya yazılır. Muhasebe tek yerde durur —
/// izlenen dövüş ile toplu simülasyonda çözülen dövüş aynı hesabı bırakır.
/// </para>
/// <para>
/// <see cref="Watcher"/> verilmezse (ekran tek başına açıldıysa) dövüş arka planda
/// çözülür. Aynı iki çağrı, yalnızca arada arena yok.
/// </para>
/// </remarks>
public sealed partial class DayScreen : DojoScreen
{
    private readonly HashSet<WarriorId> _party = [];
    private readonly Expedition _expedition = new();

    private DojoState _dojo = null!;
    private Label _offerLabel = null!;
    private Label _bountyLabel = null!;
    private VBoxContainer _partyList = null!;
    private Label _verdictLabel = null!;
    private Button _sendButton = null!;
    private Button _bountyButton = null!;
    private Button _acceptButton = null!;
    private Button _restButton = null!;
    private Label _log = null!;

    /// <summary>
    /// Kurulan dövüşü izlettirecek taraf; <c>null</c> ise dövüş arka planda çözülür.
    /// </summary>
    /// <remarks>
    /// Dövüşü bu ekran oynatmıyor: arena ayrı bir sahne ve gün ekranı kapanıp yerine o
    /// geliyor. Devralan taraf <c>true</c> döner; dönmezse ekran dövüşü kendi çözer, yani
    /// ekran tek başına da çalışır.
    /// </remarks>
    public Func<PendingBattle, bool>? Watcher { get; set; }

    /// <summary>Arenadan dönerken basılacak bilanço; ekran kurulunca bir kez yazılır.</summary>
    public string? Report { get; set; }

    /// <summary>Ekranı kurar ve günü basar.</summary>
    public override void Build(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        _dojo = dojo;

        VBoxContainer page = BuildPage();

        _offerLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        page.AddChild(_offerLabel);

        _bountyLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        page.AddChild(_bountyLabel);

        _acceptButton = new Button { Text = "Sözleşmeyi kabul et" };
        _acceptButton.Pressed += AcceptBounty;
        page.AddChild(_acceptButton);

        page.AddChild(new Label { Text = "Sefere kimler gidiyor?" });

        ScrollContainer scroll = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        page.AddChild(scroll);

        _partyList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_partyList);

        _verdictLabel = new Label();
        page.AddChild(_verdictLabel);

        HBoxContainer buttons = new();
        buttons.AddThemeConstantOverride("separation", 12);
        page.AddChild(buttons);

        _sendButton = new Button { Text = "Teklife gir" };
        _sendButton.Pressed += SendToOffer;
        buttons.AddChild(_sendButton);

        _bountyButton = new Button { Text = "Kelle avına çık" };
        _bountyButton.Pressed += SendToBounty;
        buttons.AddChild(_bountyButton);

        _restButton = new Button { Text = "Günü dojoda geçir" };
        _restButton.Pressed += Rest;
        buttons.AddChild(_restButton);

        _log = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _log.Text = Report ?? string.Empty;
        page.AddChild(_log);

        Refresh();
    }

    /// <summary>Günü, ekibi ve tuşları yeniden basar.</summary>
    public void Refresh()
    {
        OfferCard offer = OfferModel.Describe(_dojo);
        Resources purse = _dojo.Resources;

        _offerLabel.Text = string.Join(
            '\n',
            $"Gün {_dojo.Day}  ·  Kasa {purse.Gold} altın  ·  Yiyecek {purse.Food}" +
            $"  ·  Su {purse.Water}  ·  İlaç {purse.Medicine}",
            $"Teklif: {offer.Sighting}",
            $"Tehdit: {ThreatName(offer.Threat)}  ·  Söz verilen ödül {offer.PromisedReward} altın",
            offer.RequiredPartySize is int size
                ? $"Bu iş tam {size} kişi istiyor."
                : $"Ekip en çok {offer.MaxPartySize} kişi.");

        BuildPartyList();
        ShowBounty(OfferModel.DescribeBounty(_dojo));
        UpdateButtons();
    }

    private void BuildPartyList()
    {
        Clear(_partyList);
        IReadOnlyList<PartyCandidate> candidates = OfferModel.Candidates(_dojo);

        // Kadrodan düşenler seçili kalmasın: ölen ya da yaralanan savaşçı seçimde
        // durursa hüküm "kadroda yok" der ve tuş sebepsiz kapanmış görünür.
        _party.IntersectWith(candidates.Where(c => c.Fit).Select(c => c.Id));

        if (candidates.Count == 0)
        {
            _partyList.AddChild(new Label { Text = "Kadroda kimse kalmadı." });
            return;
        }

        foreach (PartyCandidate candidate in candidates)
        {
            CheckBox box = new()
            {
                Text = candidate.Fit
                    ? $"{candidate.Name}  —  güç {candidate.Score:0}"
                    : $"{candidate.Name}  —  revirde {candidate.RecoveryDaysRemaining} gün",
                Disabled = !candidate.Fit,
                ButtonPressed = _party.Contains(candidate.Id),
            };
            box.AddThemeColorOverride("font_color", candidate.Fit ? InkColor : MutedColor);

            WarriorId id = candidate.Id;
            box.Toggled += pressed =>
            {
                if (pressed)
                {
                    _party.Add(id);
                }
                else
                {
                    _party.Remove(id);
                }

                UpdateButtons();
            };

            _partyList.AddChild(box);
        }
    }

    private void ShowBounty(BountyCard? card)
    {
        if (card is not BountyCard bounty)
        {
            _bountyLabel.Text = "Tahtada bugün sözleşme yok.";
            _bountyLabel.AddThemeColorOverride("font_color", MutedColor);
            return;
        }

        _bountyLabel.Text = string.Join(
            '\n',
            $"Sözleşme: {bounty.TargetName}  ·  {bounty.Patron}",
            $"Tehdit: {ThreatName(bounty.Threat)}  ·  Ödül {bounty.Reward} altın" +
            $"  ·  Süre {bounty.DaysLeft} gün",
            bounty.Accepted
                ? $"Söz verildi. Dönülmezse kadro {bounty.BrokenHonorPenalty:0} onur kaybeder."
                : $"Kabul gün yemez, süre satın alır. Kelleyi getiren ekip {bounty.HonorReward:0} onur kazanır.");
        _bountyLabel.AddThemeColorOverride(
            "font_color",
            bounty.Accepted ? PendingColor : InkColor);
    }

    private void UpdateButtons()
    {
        List<WarriorId> party = [.. _party];
        PartyVerdict offer = OfferModel.Judge(_dojo, party);
        BountyContract? contract = _dojo.Bounty;
        PartyVerdict bounty = contract is null
            ? new PartyVerdict(ExpeditionRefusal.StaleOffer, party.Count)
            : OfferModel.JudgeBounty(_dojo, contract, party);

        _sendButton.Disabled = !offer.CanSend;
        _bountyButton.Disabled = !bounty.CanSend;
        _bountyButton.Visible = contract is not null;
        _acceptButton.Visible = contract is not null;
        _acceptButton.Disabled = contract is null || _dojo.AcceptedBountyDay is not null;
        _restButton.Disabled = false;

        _verdictLabel.Text = offer.Refusal is ExpeditionRefusal refusal
            ? RefusalText(refusal)
            : $"Ekip hazır: {party.Count} kişi.";
        _verdictLabel.AddThemeColorOverride(
            "font_color",
            offer.CanSend ? GoodColor : WarningColor);
    }

    private void SendToOffer()
    {
        List<WarriorId> chosen = [.. _party];
        if (!OfferModel.Judge(_dojo, chosen).CanSend)
        {
            return;
        }

        DojoState dojo = _dojo;
        EncounterOffer offer = dojo.Offer;
        List<RosterEntry> party = [.. OfferModel.Party(dojo, chosen)];
        BattleSetup setup = Expedition.Prepare(dojo, offer, party, collectEvents: true);

        Fight(new PendingBattle(
            setup,
            BattleSeed(),
            battle => Log(new Expedition().Settle(dojo, setup, battle), dojo)));
    }

    private void SendToBounty()
    {
        if (_dojo.Bounty is not BountyContract contract)
        {
            return;
        }

        List<WarriorId> chosen = [.. _party];
        if (!OfferModel.JudgeBounty(_dojo, contract, chosen).CanSend)
        {
            return;
        }

        DojoState dojo = _dojo;
        List<RosterEntry> party = [.. OfferModel.Party(dojo, chosen)];
        BattleSetup setup = Expedition.PrepareBounty(dojo, contract, party, collectEvents: true);

        Fight(new PendingBattle(
            setup,
            BattleSeed(),
            battle => Log(
                new Expedition().SettleBounty(dojo, contract, party, setup, battle),
                contract,
                dojo)));
    }

    /// <summary>
    /// Dövüşü izlettirir; izleyecek kimse yoksa burada çözer.
    /// </summary>
    /// <remarks>
    /// İzlenen dövüşte bu ekran kapanır ve bilanço arenadan dönen ekranda basılır; o
    /// yüzden hesabı kapatan geri çağrı <b>bu düğüme dokunmuyor</b>, yalnızca metin
    /// üretiyor.
    /// </remarks>
    private void Fight(PendingBattle bout)
    {
        if (Watcher?.Invoke(bout) == true)
        {
            return;
        }

        _log.Text = bout.Settle(new Battle(bout.Setup, new SeededRandom(bout.Seed)).Run());
        AfterDay();
    }

    private static string Log(ExpeditionResult result, DojoState dojo) => string.Join(
        '\n',
        $"{OutcomeText(result.Aftermath.Outcome)}  ·  Kasaya {result.Reward} altın girdi.",
        AftermathText(result.Aftermath, dojo),
        DayText(result.Day));

    private static string Log(BountyResult result, BountyContract contract, DojoState dojo) =>
        string.Join(
            '\n',
            $"{OutcomeText(result.Aftermath.Outcome)}  ·  Kasaya {result.Reward} altın girdi." +
            (result.Claimed ? $"  Kelle alındı: {contract.Target.Name}." : "  Kelle alınamadı."),
            AftermathText(result.Aftermath, dojo),
            DayText(result.Day));

    private void AcceptBounty()
    {
        if (_dojo.AcceptBounty() is BountyContract contract)
        {
            _log.Text = $"Söz verildi: {contract.Target.Name}, son gün {contract.Deadline}.";
            Persist();
        }

        Refresh();
    }

    private void Rest()
    {
        DayReport report = _dojo.Decline();
        _log.Text = $"Gün dojoda geçti.\n{DayText(report)}";
        AfterDay();
    }

    private void AfterDay()
    {
        _party.Clear();
        Persist();
        Refresh();
    }

    /// <summary>
    /// Seferin akışı — gün ve tohumdan türetilir.
    /// </summary>
    /// <remarks>
    /// Aynı kayıt aynı günde aynı dövüşü versin diye saat değil <b>gün</b>
    /// karıştırılıyor: rastgele bir tohum, "kaydı yükleyip dövüşü yeniden çevirme"
    /// kapısını açardı.
    /// </remarks>
    private ulong BattleSeed() => _dojo.Seed ^ ((ulong)_dojo.Day * 0x9E3779B97F4A7C15);

    private static string DayText(DayReport report)
    {
        List<string> lines =
        [
            $"Gün {report.Day} kapandı. Stok için {report.Upkeep.GoldSpent} altın ödendi.",
        ];

        if (report.Event is DayEvent happening)
        {
            lines.Add($"Aksilik: {happening.Description}");
        }

        if (report.Upkeep.Hungry.Count > 0)
        {
            lines.Add($"{report.Upkeep.Hungry.Count} savaşçı aç kaldı — o gün ilerlemediler.");
        }

        if (report.Recovered.Count > 0)
        {
            lines.Add($"{report.Recovered.Count} savaşçı revirden çıktı.");
        }

        if (report.BountyBroken)
        {
            lines.Add("Söz kırıldı: kadro onur kaybetti.");
        }

        return string.Join('\n', lines);
    }

    private static string AftermathText(AftermathReport aftermath, DojoState dojo)
    {
        List<string> lines = [];

        foreach (WarriorAftermath warrior in aftermath.Warriors)
        {
            string name = dojo.Roster.Find(warrior.Id)?.Name ?? "Savaşçı";
            if (warrior.Died)
            {
                lines.Add($"{name} öldü.");
                continue;
            }

            if (warrior.LostParts.Count > 0)
            {
                lines.Add($"{name} kalıcı sakatlıkla döndü.");
            }

            if (warrior.RecoveryDays > 0)
            {
                lines.Add($"{name} revirde {warrior.RecoveryDays} gün kalacak.");
            }
        }

        return lines.Count == 0 ? "Kadro çizilmeden döndü." : string.Join('\n', lines);
    }

    private static string OutcomeText(BattleOutcome outcome) => outcome switch
    {
        BattleOutcome.PlayerVictory => "Zafer.",
        BattleOutcome.PlayerWithdrawal => "Ekip sahayı terk etti.",
        BattleOutcome.PlayerWipe => "Ekip kırıldı.",
        _ => "Süre doldu; kimse bitiremedi.",
    };

    private static string RefusalText(ExpeditionRefusal refusal) => refusal switch
    {
        ExpeditionRefusal.EmptyParty => "Kimse seçilmedi.",
        ExpeditionRefusal.StaleOffer => "Bu teklif bugünün teklifi değil.",
        ExpeditionRefusal.WrongPartySize => "Ekip büyüklüğü bu işe uymuyor.",
        ExpeditionRefusal.Unfit => "Seçilenlerden biri sefere çıkacak durumda değil.",
        _ => "Seçilenlerden biri kadroda yok.",
    };

    private static string ThreatName(ThreatBand threat) => threat switch
    {
        ThreatBand.Faint => "devriye işi",
        ThreatBand.Rising => "sıradan gün",
        ThreatBand.Heavy => "kadro hazırlanmalı",
        _ => "ölüm riski yüksek",
    };
}

/// <summary>Kurulmuş ama henüz koşturulmamış bir dövüş.</summary>
/// <remarks>
/// Üç parça birlikte taşınmalı: dövüşün girdileri, tohumu ve <b>hesabı kapatan</b>
/// çağrı. Ayrı ayrı verilseydi arena bitmiş bir dövüşü yanlış sefere yazabilirdi.
/// </remarks>
/// <param name="Setup">Dövüşün girdileri — <c>Expedition.Prepare</c> kurdu.</param>
/// <param name="Seed">Dövüşün tohumu; aynı gün aynı dövüşü verir.</param>
/// <param name="Settle">
/// Biten dövüşün hesabını kapatır ve ekranda basılacak bilançoyu döndürür.
/// </param>
public sealed record PendingBattle(BattleSetup Setup, ulong Seed, Func<BattleResult, string> Settle);
