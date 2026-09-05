using Domina.Core.Combat;
using Domina.Core.Honor;
using Domina.Core.Model;

namespace Domina.Core.Dojo;

/// <summary>Dövüşün sonucunu kadroya işler.</summary>
/// <remarks>
/// <para>
/// Çekirdek kalıcı hale <b>dokunmaz</b>: ne olduğunu söyler, ne olacağını değil. Ölüm,
/// uzuv kaybı, dağılan zırh ve biriken yıpranma dövüş özetinde birer <b>rapor</b>dur;
/// onları geri dönüşsüz hale çevirmek bu sınıfın işi. Ayrımın sebebi mimari kural —
/// toplu simülasyon aynı kadroyu on binlerce kez koşturur ve hiçbirinde savaşçının
/// kalıcı hali bozulmamalıdır.
/// </para>
/// <para>
/// Yalnızca <b>dojo tarafının</b> özetleri işlenir. Yokai'ler kendi kimliklerini taşır
/// ve bu kimlikler kadrodakilerle çakışabilir; takım filtresi olmasaydı düşmanın
/// kaybettiği kol dojo'daki bir savaşçıya yazılabilirdi.
/// </para>
/// </remarks>
public sealed class BattleAftermath(HonorEngine? honor = null)
{
    private readonly HonorEngine _honor = honor ?? new HonorEngine();

    /// <summary>Dövüş sonucunu kadroya uygular ve ne değiştiğini döndürür.</summary>
    public AftermathReport Apply(DojoState state, BattleResult result)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(result);

        List<WarriorAftermath> lines = [];
        foreach (WarriorBattleSummary summary in result.Summaries)
        {
            if (summary.Team != Battle.PlayerTeam)
            {
                continue;
            }

            RosterEntry? entry = state.Roster.Find(summary.Id);
            if (entry is null || !entry.Warrior.IsAlive)
            {
                continue;
            }

            lines.Add(ApplyTo(state, entry, summary));
        }

        return new AftermathReport(result.Outcome, lines);
    }

    private WarriorAftermath ApplyTo(DojoState state, RosterEntry entry, WarriorBattleSummary summary)
    {
        Warrior warrior = entry.Warrior;

        List<BodyPart> lost = [];
        foreach (BodyPart part in summary.LostParts.Parts())
        {
            if (warrior.AddDisability(part))
            {
                lost.Add(part);
            }
        }

        WearArmor(warrior, summary);

        List<HitLocation> shattered = [];
        foreach (HitLocation slot in summary.DestroyedArmor.Slots())
        {
            shattered.Add(slot);
            StripSlot(warrior, slot);
        }

        if (summary.Died)
        {
            state.Roster.Kill(warrior.Id);
            return new WarriorAftermath(warrior.Id, Died: true, lost, shattered, RecoveryDays: 0, HonorDelta: 0);
        }

        double honorDelta = _honor.PerformanceDelta(summary) + _honor.RetreatDelta(summary);
        warrior.Honor = HonorScale.Clamp(warrior.Honor + honorDelta);

        int days = RecoveryDays(state.Tuning, warrior, summary, lost.Count);
        entry.Injure(days);

        return new WarriorAftermath(warrior.Id, Died: false, lost, shattered, days, honorDelta);
    }

    /// <summary>
    /// Dövüşte emilen hasarı savaşçının kalıcı yıpranma defterine ekler.
    /// </summary>
    private static void WearArmor(Warrior warrior, WarriorBattleSummary summary)
    {
        ArmorWearSet total = warrior.ArmorWear;
        foreach (HitLocation slot in ArmorSlots.All)
        {
            double added = summary.ArmorWear.At(slot);
            if (added > 0)
            {
                total = total.With(slot, total.At(slot) + added);
            }
        }

        warrior.ArmorWear = total;
    }

    /// <summary>
    /// Dağılan parçayı kuşamdan çıkarır ve o yuvanın yıpranmasını sıfırlar.
    /// </summary>
    /// <remarks>
    /// Sıfırlama şart: yıpranma <b>parçaya</b> aittir, yuvaya değil. Sayaç kalsaydı
    /// yerine takılan yepyeni parça, dağılan parçanın defterini devralır ve ilk
    /// darbede dağılırdı.
    /// </remarks>
    private static void StripSlot(Warrior warrior, HitLocation slot)
    {
        warrior.Armor = warrior.Armor.With(slot, ArmorPiece.Bare);
        warrior.ArmorWear = warrior.ArmorWear.With(slot, 0);
    }

    /// <summary>Savaşçının kaç gün sefere çıkamayacağı.</summary>
    /// <remarks>
    /// İki kalemden gelir: yenen hasarın payı ve kaybedilen uzuv sayısı. Sayılar
    /// <b>kilitli değil</b> — GDD §7 yalnızca "yara ağırlığına göre" diyor, süre
    /// ekonomi turunda ölçülecek (Açık Karar #5).
    /// </remarks>
    private static int RecoveryDays(
        DojoTuning tuning,
        Warrior warrior,
        WarriorBattleSummary summary,
        int lostLimbs)
    {
        double maxHealth = warrior.EffectiveStats.MaxHealth;
        double lostShare = maxHealth <= 0
            ? 0
            : Math.Clamp(1 - (summary.HealthRemaining / maxHealth), 0, 1);

        double free = Math.Clamp(tuning.RecoveryFreeDamageShare, 0, 0.99);
        double paid = Math.Max(0, lostShare - free) / (1 - free);

        int fromWounds = (int)Math.Ceiling(paid * tuning.RecoveryDaysAtFullDamage);
        return fromWounds + (lostLimbs * tuning.RecoveryDaysPerLostLimb);
    }
}

/// <summary>Bir dövüşün kadroya yazılmış hâli.</summary>
/// <param name="Outcome">Dövüşün sonucu.</param>
/// <param name="Warriors">Dojo tarafındaki her savaşçının bilançosu.</param>
public sealed record AftermathReport(BattleOutcome Outcome, IReadOnlyList<WarriorAftermath> Warriors)
{
    public IEnumerable<WarriorAftermath> Dead => Warriors.Where(w => w.Died);

    /// <summary>Revire yatan savaşçılar.</summary>
    public IEnumerable<WarriorAftermath> Wounded => Warriors.Where(w => !w.Died && w.RecoveryDays > 0);
}

/// <param name="Id">Savaşçı.</param>
/// <param name="Died">Dövüşten sağ çıkamadı mı — geri dönüşü yoktur.</param>
/// <param name="LostParts">Bu dövüşte kalıcı olarak kaybedilen uzuvlar.</param>
/// <param name="ShatteredArmor">Dağılan ve kuşamdan çıkarılan zırh yuvaları.</param>
/// <param name="RecoveryDays">Kaç gün sefere çıkamayacağı.</param>
/// <param name="HonorDelta">Dövüşün onura etkisi.</param>
public sealed record WarriorAftermath(
    WarriorId Id,
    bool Died,
    IReadOnlyList<BodyPart> LostParts,
    IReadOnlyList<HitLocation> ShatteredArmor,
    int RecoveryDays,
    double HonorDelta);
