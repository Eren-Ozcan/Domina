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
/// Savunan, gelen silahı yakaladı: hasar yok ve saldıran <paramref name="BindSeconds"/>
/// boyunca kilitli kalır.
/// </summary>
/// <remarks>
/// Kaçınmadan ayrı bir olaydır çünkü ekranda anlatacağı şey ayrı: kaçınmada savunan
/// çekilir, yakalamada iki savaşçı bir an <b>birbirine kenetlenir</b>. Sunum katmanı
/// bunu iki tarafı da içeren tek bir duruş olarak oynatmalı.
/// </remarks>
public sealed record AttackCaught(
    double AtSeconds,
    WarriorId Attacker,
    WarriorId Defender,
    double BindSeconds) : BattleEvent(AtSeconds);

/// <summary>Ağır darbe savaşçıyı sersemletti: <paramref name="Seconds"/> boyunca donar.</summary>
public sealed record WarriorStunned(
    double AtSeconds,
    WarriorId Attacker,
    WarriorId Defender,
    double Seconds) : BattleEvent(AtSeconds);

/// <summary>
/// Vuruş zehir taşıyordu: savunanın kanına doz girdi.
/// </summary>
/// <remarks>
/// Hasarın kendisi ayrı bir olaydır (<see cref="PoisonTicked"/>). İkisi ayrı durur çünkü
/// ekranda anlatacakları da ayrı: zehirlenme <b>bir kez</b> olur ve silahı ele verir,
/// hasar süre boyunca tekrar eder.
/// </remarks>
/// <param name="Dose">Vuruştan sonra savunanın kanındaki toplam doz.</param>
/// <param name="Seconds">Dozun yenilenen ömrü.</param>
public sealed record WarriorPoisoned(
    double AtSeconds,
    WarriorId Attacker,
    WarriorId Defender,
    double Dose,
    double Seconds) : BattleEvent(AtSeconds);

/// <summary>
/// Zehir bir kez daha işledi. Vuran kimse yok; hasar zırhtan da savunmadan da geçmez.
/// </summary>
public sealed record PoisonTicked(
    double AtSeconds,
    WarriorId Warrior,
    double Damage,
    double HealthRemaining) : BattleEvent(AtSeconds);

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

/// <summary>Savaşçı hücuma kalktı: yerinde güç topluyor, kıpırdamıyor.</summary>
public sealed record ChargeStarted(double AtSeconds, WarriorId Warrior, WarriorId Target)
    : BattleEvent(AtSeconds);

/// <summary>Birikme tamamlandı, koşu başladı.</summary>
/// <remarks>
/// Görselleştirme için ayrı duruyor: birikme ile koşu aynı hamlenin iki farklı anıdır ve
/// ekranda aynı görünemezler.
/// </remarks>
public sealed record ChargeLaunched(double AtSeconds, WarriorId Warrior, WarriorId Target)
    : BattleEvent(AtSeconds);

/// <summary>
/// Birikme bir isabetle dağıldı — koşu hiç başlamadı, bonus alınmadı.
/// </summary>
public sealed record ChargeBroken(double AtSeconds, WarriorId Warrior) : BattleEvent(AtSeconds);

/// <summary>
/// Hücum hedefe vardı; bunu takip eden vuruş hasar çarpanı taşır.
/// </summary>
public sealed record ChargeConnected(double AtSeconds, WarriorId Warrior, WarriorId Target)
    : BattleEvent(AtSeconds);

/// <summary>
/// Hücum boşa gitti: hedef öldü, kaçtı, sahadan çıktı ya da süre doldu.
/// </summary>
/// <remarks>
/// Hücumun taahhüdünün ekranda görünen karşılığı bu — koşan savaşçı kimseye varamadan
/// açıkta kalır.
/// </remarks>
public sealed record ChargeMissed(double AtSeconds, WarriorId Warrior) : BattleEvent(AtSeconds);

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

    /// <summary>
    /// Zehir bitirdi. Vuruşla ölümün arasında saniyeler var; ölümü <b>kimse</b> indirmedi.
    /// </summary>
    /// <remarks>
    /// Ayrı bir sebep olarak durur çünkü onur hesabı ile ekranın söyleyeceği söz de ayrı:
    /// zehirle düşen savaşçı savaş meydanında değil, ondan sonra ölür.
    /// </remarks>
    Poison,
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
