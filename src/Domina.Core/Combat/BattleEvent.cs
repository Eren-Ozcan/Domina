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

/// <summary>Oyuncu "çek" tuşuna bastı. Henüz kaçış başlamamış olabilir (bkz. buffer).</summary>
public sealed record RetreatCommanded(double AtSeconds, WarriorId Warrior) : BattleEvent(AtSeconds);

/// <summary>
/// Komut buffer'landı: savaşçı saldırı vuruşuna kilitliydi, mevcut hareketi bitince
/// kaçış başlayacak (bkz. docs/GDD.md §5).
/// </summary>
public sealed record RetreatBuffered(double AtSeconds, WarriorId Warrior) : BattleEvent(AtSeconds);

/// <summary>Kaçış başladı. Bu andan itibaren savaşçı kaçınamaz/bloklayamaz.</summary>
public sealed record RetreatStarted(double AtSeconds, WarriorId Warrior) : BattleEvent(AtSeconds);

/// <summary>Kaçan avın arkasından gelen bedava vuruş.</summary>
public sealed record OpportunityAttack(double AtSeconds, WarriorId Attacker, WarriorId Defender)
    : BattleEvent(AtSeconds);

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

    /// <summary>Dojo tarafında dövüşen kalmadı (ölü veya kaçmış).</summary>
    PlayerDefeat,

    /// <summary>Süre doldu — iki taraf da bitiremedi.</summary>
    TimeLimit,
}
