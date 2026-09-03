using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Presentation;

/// <summary>Bir savaşçının tek seferlik görsel tepkisi.</summary>
public enum RigReactionKind
{
    /// <summary>Vuruş yedi.</summary>
    Flinch,

    /// <summary>Kaçındı — bedeli stamina, o yüzden ekranda da görünmeli.</summary>
    Dodge,

    /// <summary>Boşa savurdu; kılıç hedefi bulamadan geçti.</summary>
    Overswing,

    /// <summary>Kaçanın arkasından bedava vuruş yaptı.</summary>
    OpportunitySwing,

    /// <summary>Gelen silahı yakaladı — jitte/sai'nin tek seferlik hamlesi.</summary>
    /// <remarks>
    /// <see cref="Dodge"/>'dan ayrı tutulur: kaçınmada savunan yana çekilir, yakalamada
    /// <b>öne</b> gider. İkisi aynı tepkiye bağlansaydı ekranda jitte'nin yaptığı iş
    /// kaçınmadan ayırt edilemezdi.
    /// </remarks>
    Catch,

    /// <summary>Uzvunu kaybetti — kalıcı.</summary>
    Dismember,

    /// <summary>Mermi fırlattı.</summary>
    Throw,

    /// <summary>
    /// Kaçarken sendeledi — kimsenin vurmadığı yara.
    /// </summary>
    /// <remarks>
    /// <see cref="Flinch"/>'ten ayrı tutulur: ortada vuran biri yok, o yüzden ekranda
    /// darbe yönü de yok. Sendeleme kendi başına okunmalı.
    /// </remarks>
    Stumble,

    /// <summary>
    /// Zehir işledi — kimsenin vurmadığı ikinci yara.
    /// </summary>
    /// <remarks>
    /// <see cref="Flinch"/>'ten ayrı tutulur: ortada darbe de, darbenin yönü de yok.
    /// <see cref="Stumble"/>'ın eğrilerini ödünç alır ama kendi türü olarak kalır —
    /// sendeleme kaçarken olur, zehir dövüşün ortasında.
    /// </remarks>
    PoisonThroe,
}

/// <param name="Part">Yalnızca <see cref="RigReactionKind.Dismember"/> için doludur.</param>
public readonly record struct RigReaction(WarriorId Warrior, RigReactionKind Kind, BodyPart? Part = null);

/// <summary>
/// Olay akışını görsel tepkilere çevirir.
/// </summary>
/// <remarks>
/// <para>
/// Görsel iki kanaldan sürülür: <b>sürekli</b> hal anlık görüntülerden (duruş, konum,
/// can), <b>anlık</b> tepkiler buradan. Ayrım korunmalı — olaylar tek seferliktir,
/// tekrar oynatılamaz; bu yüzden okunan yer sayaçla takip edilir ve liste yalnızca büyür.
/// </para>
/// <para>
/// <b>Iskalayan ve kaçınılan vuruşların da karşılığı var.</b> Yalnızca isabetler
/// bağlandığında üç saldırı sonucu (isabet, ıska, kaçınma) ekranda birbirinin aynısı
/// görünüyordu: kılıç aynı şekilde iniyor, biri sarsılıyor ya da hiçbir şey olmuyordu.
/// Kaçınma stamina harcayan bir çekirdek mekaniği; ekranda karşılığı yoksa oyuncu
/// staminanın nereye gittiğini göremez.
/// </para>
/// <para>
/// Karşılığı olmayan olaylar sessizce atlanır — çekirdek yeni bir olay eklemek için
/// görselleştirmeyi beklemek zorunda değil.
/// </para>
/// </remarks>
public sealed class ReactionReader
{
    private readonly List<RigReaction> _buffer = [];

    /// <summary>Şimdiye kadar okunan olay sayısı.</summary>
    public int Consumed { get; private set; }

    /// <summary>
    /// Son çağrıdan beri üretilen olayların tepkilerini döndürür.
    /// </summary>
    /// <remarks>
    /// Dönen liste bir sonraki çağrıda yeniden kullanılır; çağıran tarafın onu
    /// saklamaması gerekir.
    /// </remarks>
    public IReadOnlyList<RigReaction> Drain(IReadOnlyList<BattleEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        _buffer.Clear();

        for (; Consumed < events.Count; Consumed++)
        {
            Translate(events[Consumed], _buffer);
        }

        return _buffer;
    }

    private static void Translate(BattleEvent battleEvent, List<RigReaction> into)
    {
        switch (battleEvent)
        {
            case AttackLanded landed:
                into.Add(new RigReaction(landed.Defender, RigReactionKind.Flinch));
                break;

            case AttackMissed missed:
                into.Add(new RigReaction(missed.Attacker, RigReactionKind.Overswing));
                break;

            case AttackCaught caught:
                // Yalnızca yakalayan tepki üretir. Saldıranın karşılığı tek seferlik
                // değil: silahı yakalanan savaşçı CombatState.WeaponBound durumuna
                // geçer ve duruşu oradan sürülür — buraya ikinci bir tepki eklemek
                // aynı işi bir kez daha, üstelik yalnızca bir an için yapmak olurdu.
                into.Add(new RigReaction(caught.Defender, RigReactionKind.Catch));
                break;

            case AttackDodged dodged:
                // İkisi birden: saldıran boşa savurur, savunan yana kaçar.
                into.Add(new RigReaction(dodged.Attacker, RigReactionKind.Overswing));
                into.Add(new RigReaction(dodged.Defender, RigReactionKind.Dodge));
                break;

            case OpportunityAttack opportunity:
                // Kaçışın bedeli. Kendi başına vuruş sonucu üretmez — hemen ardından
                // gelen isabet/ıska olayı onu tamamlar; buradaki tek iş, bedava
                // vuruşun kimden geldiğini göstermek.
                into.Add(new RigReaction(opportunity.Attacker, RigReactionKind.OpportunitySwing));
                break;

            case WarriorDismembered lost:
                into.Add(new RigReaction(lost.Warrior, RigReactionKind.Dismember, lost.Part));
                break;

            case PoisonTicked poison:
                // Doz bir kez daha işledi. Zehirlenme anının kendisi (WarriorPoisoned)
                // tepki üretmez: onun ekrandaki karşılığı vuruşun kendisidir, ve orada
                // zaten bir irkilme var.
                into.Add(new RigReaction(poison.Warrior, RigReactionKind.PoisonThroe));
                break;

            case ProjectileLaunched launched:
                // Merminin uçuşu bir tepki değil, ayrı bir sahne nesnesi (bkz.
                // ProjectileView). Buradaki tek iş atan savaşçının hamlesini göstermek.
                into.Add(new RigReaction(launched.Attacker, RigReactionKind.Throw));
                break;

            case ProjectileHit hit:
                into.Add(new RigReaction(hit.Defender, RigReactionKind.Flinch));
                break;

            case EscapeMishap mishap:
                into.Add(new RigReaction(mishap.Warrior, RigReactionKind.Stumble));
                break;

            // Ölümün ve arenadan çıkışın burada karşılığı YOK, çünkü ikisi de tek
            // seferlik değil kalıcı hal: durum <see cref="CombatState.Dead"/> olarak
            // gelir ve yığılma oradan sürülür, cesedin nerede kalacağına ise
            // <see cref="ArenaChoreography"/> karar verir. Buraya bir tepki eklemek
            // aynı işi ikinci kez, üstelik yalnızca bir kez tetiklenerek yapmak olurdu.
            default:
                break;
        }
    }
}
