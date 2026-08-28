using Domina.Core.Model;

namespace Domina.Core.Combat;

/// <summary>
/// Dövüş sırasında olan biteni anlatan olay akışı.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bu tasarımın can alıcı noktası:</b> Dövüş çözümleyici animasyon hakkında
/// hiçbir şey bilmez — yalnızca bu olayları üretir. Godot katmanı olayları alıp
/// oynatır. Ayrım bozulup çözümleyici animasyona bağlanırsa, motor açmadan toplu
/// simülasyon yapmak imkânsızlaşır ve denge çalışması ölür
/// (bkz. CLAUDE.md → "Mimari kuralı").
/// </para>
/// </remarks>
public abstract record BattleEvent(double AtSeconds);

public sealed record BattleStarted(double AtSeconds) : BattleEvent(AtSeconds);

public sealed record AttackStarted(double AtSeconds, WarriorId Attacker, WarriorId Defender)
    : BattleEvent(AtSeconds);

public sealed record AttackMissed(double AtSeconds, WarriorId Attacker, WarriorId Defender)
    : BattleEvent(AtSeconds);

/// <summary>Kaçınma başarılı — hasar yok ama stamina gitti.</summary>
public sealed record AttackDodged(double AtSeconds, WarriorId Attacker, WarriorId Defender)
    : BattleEvent(AtSeconds);

public sealed record AttackLanded(
    double AtSeconds,
    WarriorId Attacker,
    WarriorId Defender,
    double Damage,
    double DefenderHealthRemaining) : BattleEvent(AtSeconds);

/// <summary>
/// Ağır darbe geldi ve savaşçı çekilmekte olduğu için <b>yaşadı ama uzvunu kaybetti</b>.
/// Oyuncu zamanında müdahale etmeseydi bu olay <see cref="WarriorDied"/> olurdu.
/// </summary>
public sealed record WarriorDismembered(double AtSeconds, WarriorId Warrior, BodyPart Part)
    : BattleEvent(AtSeconds);

/// <summary>
/// Tuşa temastan önce basıldı ve komut reddedildi (bkz. docs/GDD.md §5).
/// </summary>
/// <remarks>
/// <para>
/// Kaçış yalnızca ilk isabetten sonra açılır: kimse dokunmadan çekilmek yok. Reddedilen
/// basış yine de olay üretir, çünkü arayüzün söyleyecek bir şeyi var — kural ilk kez
/// görülüyorsa öğretilmeli, ısrarla tekrarlanıyorsa cevaplanmalı.
/// </para>
/// <para>
/// <paramref name="ConsecutivePresses"/> bu dövüşteki üst üste reddedilen basış
/// sayısıdır; ilk kabul edilen komutta anlamını yitirir. Metni çekirdek üretmez —
/// sayıyı verir, ne yazılacağına sunum katmanı karar verir.
/// </para>
/// </remarks>
public sealed record RetreatRefused(double AtSeconds, int ConsecutivePresses) : BattleEvent(AtSeconds);

/// <summary>Oyuncu "çek" tuşuna bastı. Henüz kaçış başlamamış olabilir (bkz. buffer).</summary>
public sealed record RetreatCommanded(double AtSeconds, WarriorId Warrior) : BattleEvent(AtSeconds);

/// <summary>
/// Komut buffer'landı: savaşçı saldırı vuruşuna kilitliydi, mevcut hareketi bitince
/// kaçış başlayacak (bkz. docs/GDD.md §5).
/// </summary>
public sealed record RetreatBuffered(double AtSeconds, WarriorId Warrior) : BattleEvent(AtSeconds);

/// <summary>Kaçış başladı. Bu andan itibaren savaşçı kaçınamaz/bloklayamaz.</summary>
public sealed record RetreatStarted(double AtSeconds, WarriorId Warrior) : BattleEvent(AtSeconds);

/// <summary>
/// Mermi havalandı. Görselleştirme uçuşu bu olaydan sürer.
/// </summary>
/// <remarks>
/// Mermi <b>anında çözülmez</b>: uçuş süresi boyunca havada durur ve varış anında
/// <see cref="ProjectileHit"/> veya <see cref="ProjectileMissed"/> ile sonuçlanır.
/// Anında çözülseydi ekrandaki uçuş ile hasarın anı birbirini tutmazdı.
/// </remarks>
/// <param name="FlightSeconds">Varışa kalan süre — görselleştirme hızını buradan alır.</param>
public sealed record ProjectileLaunched(
    double AtSeconds,
    WarriorId Attacker,
    WarriorId Defender,
    string Weapon,
    ArenaPoint From,
    ArenaPoint To,
    double FlightSeconds) : BattleEvent(AtSeconds);

/// <summary>Mermi hedefe ulaştı.</summary>
public sealed record ProjectileHit(
    double AtSeconds,
    WarriorId Attacker,
    WarriorId Defender,
    double Damage,
    double DefenderHealthRemaining) : BattleEvent(AtSeconds);

/// <summary>
/// Mermi boşa gitti — ıskalandı ya da hedef varmadan sahadan çıktı.
/// </summary>
public sealed record ProjectileMissed(double AtSeconds, WarriorId Attacker, WarriorId Defender)
    : BattleEvent(AtSeconds);

/// <summary>Kaçan avın arkasından gelen bedava vuruş.</summary>
public sealed record OpportunityAttack(double AtSeconds, WarriorId Attacker, WarriorId Defender)
    : BattleEvent(AtSeconds);

/// <summary>
/// Arenayı terk ederken alınan kaza yarası — kimsenin vurmadığı tek yara.
/// </summary>
/// <remarks>
/// Görselleştirme bunu vuruş olarak değil <b>sendeleme</b> olarak oynatmalı; ortada
/// vuran biri yok.
/// </remarks>
public sealed record EscapeMishap(
    double AtSeconds,
    WarriorId Warrior,
    double Damage,
    double HealthRemaining) : BattleEvent(AtSeconds);

/// <summary>Savaşçı arenadan sağ çıktı.</summary>
public sealed record WarriorEscaped(double AtSeconds, WarriorId Warrior) : BattleEvent(AtSeconds);

public sealed record WarriorDied(double AtSeconds, WarriorId Warrior, DeathCause Cause)
    : BattleEvent(AtSeconds);

public sealed record BattleEnded(double AtSeconds, BattleOutcome Outcome) : BattleEvent(AtSeconds);

public enum DeathCause
{
    /// <summary>Can sıfırlandı.</summary>
    Wounds,

    /// <summary>Ağır darbe geldi ve kimse çekmedi — uzuv kopmasıyla ölüm.</summary>
    GrievousBlow,
}

public enum BattleOutcome
{
    /// <summary>Dojo tarafı ayakta kaldı.</summary>
    PlayerVictory,

    /// <summary>
    /// Dojo tarafı sahayı <b>sağ</b> terk etti — en az bir savaşçı kaçarak kurtuldu.
    /// </summary>
    /// <remarks>
    /// Bozgundan ayrı tutulur. Oyun açısından ikisi de "dövüş kazanılmadı" demek ama
    /// bedelleri taban tabana zıt: çekilmek seferi ve ödülü harcar, bozgun savaşçıları
    /// harcar. Tek kutuya konursa hiçbir denge sorusu cevaplanamaz — "kaçan oyuncu
    /// kaybediyor" gibi yanlış bir okuma çıkar.
    /// </remarks>
    PlayerWithdrawal,

    /// <summary>Dojo tarafında kimse kalmadı ve kimse kaçamadı — ekip kırıldı.</summary>
    PlayerWipe,

    /// <summary>Süre doldu — iki taraf da bitiremedi.</summary>
    TimeLimit,
}
