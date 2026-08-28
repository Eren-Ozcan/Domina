using System.Globalization;
using Domina.Core.Combat;

namespace Domina.Presentation;

/// <summary>"Çek" tuşunun o andaki hali.</summary>
/// <param name="Text">Tuşta yazan.</param>
/// <param name="Enabled">Basılabilir mi?</param>
/// <param name="Locked">Komutun en az bir savaşçıda gecikeceğini vurgulamalı mı?</param>
/// <param name="Shut">
/// Savaş henüz başlamadığı için basış <b>reddedilecek</b> mi? Tuş bu hâlde de basılabilir
/// kalır — reddedilen basış kuralı öğreten metni doğurur; basılamayan tuş hiçbir şey
/// söylemezdi.
/// </param>
public readonly record struct RetreatPrompt(
    string Text,
    bool Enabled,
    bool Locked,
    bool Shut = false);

/// <summary>Temastan önceki basışa verilecek cevabın türü.</summary>
public enum RetreatNoticeKind
{
    /// <summary>Söylenecek bir şey yok.</summary>
    None,

    /// <summary>Kuralı öğreten bilgi metni. Oyun başında sayılı kez gösterilir.</summary>
    Teaching,

    /// <summary>Israrla basana verilen cevap. Başarımı da bu açar.</summary>
    Taunt,
}

/// <summary>Reddedilen basışa arayüzün vereceği cevap.</summary>
/// <param name="Kind">Cevabın türü.</param>
/// <param name="Text">Gösterilecek metin; <see cref="RetreatNoticeKind.None"/> ise boş.</param>
/// <param name="AchievementId">Açılan başarım, yoksa null.</param>
public readonly record struct RetreatRefusalNotice(
    RetreatNoticeKind Kind,
    string Text,
    string? AchievementId);

/// <summary>
/// Arayüzde ne yazacağını hesaplar. Metin üretir, çizim yapmaz.
/// </summary>
public static class HudModel
{
    /// <summary>
    /// Tek tuşun hali.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pes etme dövüşteki tek müdahale noktasıdır (GDD §5) ve <b>tek tuştur</b>: komut
    /// ekibin tamamını çeker, savaşçı seçilmez. Savaşçı bazlı olsaydı doğru oynanış
    /// "yara alanı çek, kalanla devam et" olurdu; tek tuş kararı nadir ve ağır yapar.
    /// </para>
    /// <para>
    /// Tuş komutun <b>kaç savaşçıda anında işleyeceğini</b> gösterir: vuruşa kilitli
    /// savaşçıların komutu buffer'lanır ve kaçış ancak vuruş bitince başlar. Oyuncu bunu
    /// basmadan önce görebilmeli, yoksa gecikme hata gibi hissedilir.
    /// </para>
    /// </remarks>
    public static RetreatPrompt DescribeRetreat(
        IReadOnlyList<CombatantSnapshot> snapshots,
        bool contactMade)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        int standing = 0;
        int locked = 0;
        int leaving = 0;

        foreach (CombatantSnapshot snapshot in snapshots)
        {
            if (snapshot.Team != Battle.PlayerTeam || !snapshot.IsActive)
            {
                continue;
            }

            if (snapshot.RetreatRequested || snapshot.State == CombatState.Retreating)
            {
                leaving++;
                continue;
            }

            standing++;

            if (!snapshot.CanCancel)
            {
                locked++;
            }
        }

        if (!contactMade)
        {
            // Savaş başlamadan çekilmek yok (§5). Tuş görünür kalır ama basılamaz:
            // gizlenseydi kuralın varlığı hiç öğrenilmezdi.
            return new RetreatPrompt(
                "SAVAŞ BAŞLAMADI", Enabled: true, Locked: false, Shut: true);
        }

        if (standing == 0)
        {
            return new RetreatPrompt(leaving > 0 ? "EKİP ÇEKİLİYOR" : "—", Enabled: false, Locked: false);
        }

        string text = locked == 0
            ? $"EKİBİ ÇEK ({standing})"
            : $"EKİBİ ÇEK ({standing}) · {locked} kilitli";

        return new RetreatPrompt(text, Enabled: true, Locked: locked > 0);
    }

    /// <summary>
    /// Savaşçının panelinde yazan durum.
    /// </summary>
    /// <remarks>
    /// Komutu buffer'lanmış savaşçı ayrı yazılır. Tuş basılmadan önce kaçının kilitli
    /// olduğunu söylüyor; basıldıktan sonra da hangisinin hâlâ beklediği okunabilmeli,
    /// yoksa gecikme boyunca panel "saldırıyor" deyip komut yutulmuş gibi görünür.
    /// </remarks>
    public static string DescribeState(in CombatantSnapshot snapshot)
    {
        if (snapshot.RetreatRequested && snapshot.IsActive && snapshot.State != CombatState.Retreating)
        {
            return "çekilecek · vuruş bitince";
        }

        return snapshot.State switch
        {
            CombatState.Idle => "bekliyor",
            CombatState.AttackWindup => "saldırıyor",
            CombatState.AttackRecovery => "toparlanıyor",
            CombatState.Retreating => "çekiliyor",
            CombatState.Escaped => "kurtuldu",
            CombatState.Dead => "öldü",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Üst satır: seed ve süre.
    /// </summary>
    /// <remarks>
    /// Seed sürekli görünür durmalı — bir dövüşü tekrar açmanın ve toplu simülasyondaki
    /// karşılığını bulmanın tek yolu o (bkz. <c>Domina.Sim</c>).
    /// </remarks>
    public static string DescribeStatus(long seed, double elapsedSeconds, BattleOutcome? outcome)
    {
        string head = string.Create(
            CultureInfo.InvariantCulture,
            $"seed {seed}  ·  {elapsedSeconds:F1} sn");

        return outcome is null ? head : $"{head}  ·  {DescribeOutcome(outcome.Value)}";
    }

    public static string DescribeOutcome(BattleOutcome outcome) => outcome switch
    {
        BattleOutcome.PlayerVictory => "ZAFER",
        BattleOutcome.PlayerWithdrawal => "ÇEKİLDİ",
        BattleOutcome.PlayerWipe => "BOZGUN",
        _ => "SÜRE DOLDU",
    };

    /// <summary>Temastan önce ilk isabeti bekleyen basışa gösterilenler.</summary>
    private const string TeachingText =
        "Savaş başlamadan çekilinmez. İlk kan aktığında tuş açılır.";

    private const string TauntText =
        "Kimse sana daha dokunmadı. Bu kadar korkak olma.";

    /// <summary>Israrlı basışa cevap veren başarım.</summary>
    public const string CowardAchievementId = "dereyi-gormeden-pacalari-sivama";

    /// <summary>Öğretici metnin oyun boyunca gösterileceği azami kez.</summary>
    public const int TeachingNoticeLimit = 3;

    /// <summary>Alaycı cevabı tetikleyen üst üste basış sayısı.</summary>
    public const int TauntPressCount = 11;

    /// <summary>
    /// Temastan önce basılan tuşa ne cevap verileceği.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kural her dövüşte tekrar anlatılmaz: <see cref="TeachingNoticeLimit"/> kez
    /// gösterilir, sonra susar. Bir daha söylemek oyuncuya bildiğini tekrarlamaktır.
    /// </para>
    /// <para>
    /// Kaçıncı kez gösterildiği <b>oyun kaydının</b> bilgisidir, dövüşün değil — bu yüzden
    /// dışarıdan verilir. Üst üste basış sayısı ise dövüş içidir ve çekirdekten
    /// <c>RetreatRefused</c> ile gelir.
    /// </para>
    /// </remarks>
    /// <param name="consecutivePresses">Bu dövüşteki üst üste reddedilen basış sayısı.</param>
    /// <param name="teachingNoticesShown">Öğretici metnin bugüne dek gösterilme sayısı.</param>
    public static RetreatRefusalNotice DescribeRefusal(
        int consecutivePresses,
        int teachingNoticesShown)
    {
        if (consecutivePresses == TauntPressCount)
        {
            return new RetreatRefusalNotice(RetreatNoticeKind.Taunt, TauntText, CowardAchievementId);
        }

        if (teachingNoticesShown < TeachingNoticeLimit)
        {
            return new RetreatRefusalNotice(RetreatNoticeKind.Teaching, TeachingText, null);
        }

        return new RetreatRefusalNotice(RetreatNoticeKind.None, string.Empty, null);
    }
}
