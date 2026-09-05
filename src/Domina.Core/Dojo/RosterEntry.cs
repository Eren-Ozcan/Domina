using Domina.Core.Model;

namespace Domina.Core.Dojo;

/// <summary>Savaşçının dojo'daki günlük hâli — dövüşün bilmediği her şey.</summary>
/// <remarks>
/// <para>
/// <see cref="Model.Warrior"/> dövüşün okuduğu kalıcı hâldir; bu kayıt onun etrafındaki
/// <b>meta</b> durumu taşır: kaç gün revirde, bugün ne yapıyor, kaç gün antrenman görmüş.
/// Ayrı tutulmasının sebebi mimari kural: dövüş çözümleyicisi gün döngüsünü bilmez ve
/// toplu simülasyon aynı savaşçıyı on binlerce kez koşturur — takvim orada anlamsızdır.
/// </para>
/// </remarks>
public sealed class RosterEntry
{
    public RosterEntry(Warrior warrior)
    {
        ArgumentNullException.ThrowIfNull(warrior);
        Warrior = warrior;
    }

    public Warrior Warrior { get; }

    public WarriorId Id => Warrior.Id;

    public string Name => Warrior.Name;

    /// <summary>Savaşçının sefere çıkabilmesi için geçmesi gereken gün sayısı.</summary>
    /// <remarks>
    /// Yara ağırlığına göre dolar (bkz. docs/GDD.md §7 "İyileşme"). Doğal iyileşme
    /// günde bir gün eritir; revir ve ilaç bunu hızlandırır.
    /// </remarks>
    public int RecoveryDaysRemaining { get; internal set; }

    /// <summary>Bugünkü uğraş. Revirdeki savaşçı antrenman yapamaz.</summary>
    public DojoActivity Activity { get; internal set; } = DojoActivity.Resting;

    /// <summary>Bugüne kadar tamamlanmış antrenman günü.</summary>
    /// <remarks>
    /// Statlara <b>henüz dokunmuyor</b>: antrenmanın etkisi ölçülüp kilitlenmeden
    /// (ROADMAP Faz 3, "Antrenman alanları + antrenman süresi/etkisi") sayıyı uydurmak,
    /// sonradan sökülmesi zor bir denge borcu olurdu. Şimdilik yalnızca sayaç.
    /// </remarks>
    public int TrainingDays { get; internal set; }

    /// <summary>Sefere gönderilebilir mi?</summary>
    public bool IsFitForCampaign => Warrior.IsAlive && RecoveryDaysRemaining == 0;

    /// <summary>Savaşçıyı revire yatırır. Daha uzun süre kısayı ezer, tersi olmaz.</summary>
    public void Injure(int days)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(days);

        if (days > RecoveryDaysRemaining)
        {
            RecoveryDaysRemaining = days;
        }

        if (RecoveryDaysRemaining > 0)
        {
            Activity = DojoActivity.Recovering;
        }
    }

    /// <summary>Bugün antrenmana yazar. Revirdeki savaşçı kabul edilmez.</summary>
    public bool Train()
    {
        if (!IsFitForCampaign)
        {
            return false;
        }

        Activity = DojoActivity.Training;
        return true;
    }

    public void Rest() => Activity = RecoveryDaysRemaining > 0
        ? DojoActivity.Recovering
        : DojoActivity.Resting;
}

/// <summary>Bir savaşçının o günkü uğraşı.</summary>
public enum DojoActivity
{
    /// <summary>Boşta — ne antrenman ne revir.</summary>
    Resting,

    /// <summary>Antrenman alanında.</summary>
    Training,

    /// <summary>Revirde; sefere çıkamaz, antrenman yapamaz.</summary>
    Recovering,
}
