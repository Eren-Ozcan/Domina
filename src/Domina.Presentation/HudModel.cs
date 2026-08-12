using System.Globalization;
using Domina.Core.Combat;

namespace Domina.Presentation;

/// <summary>"Çek" tuşunun o andaki hali.</summary>
/// <param name="Text">Tuşta yazan.</param>
/// <param name="Enabled">Basılabilir mi?</param>
/// <param name="Locked">Komutun en az bir savaşçıda gecikeceğini vurgulamalı mı?</param>
public readonly record struct RetreatPrompt(string Text, bool Enabled, bool Locked);

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
    public static RetreatPrompt DescribeRetreat(IReadOnlyList<CombatantSnapshot> snapshots)
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
        BattleOutcome.PlayerDefeat => "YENİLGİ",
        _ => "SÜRE DOLDU",
    };
}
