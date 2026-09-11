using Domina.Core.Model;

namespace Domina.Core.Campaign;

/// <summary>The day's encounter offer — take it or leave it.</summary>
/// <remarks>
/// <para>
/// GDD §10: <b>one</b> offer arrives a day, there is no list or map screen. Going in eats a day (even
/// if you flee); if you do not go in, the day passes in the dojo.
/// </para>
/// <para>
/// The offer carries the <b>whole</b> enemy roster but the interface does not show it: the player only
/// reads <see cref="Threat"/> and <see cref="Sighting"/>. The roster stands here so that accepting the
/// offer builds the <b>same</b> fight — if one roster were produced at the moment of the offer and
/// another at the moment of the fight, the threat mark would be lying.
/// </para>
/// </remarks>
/// <param name="Day">The day the offer is valid for.</param>
/// <param name="Enemies">The roster that will take the field.</param>
/// <param name="Threat">The difficulty band readable before going in.</param>
/// <param name="Sighting">The rough description readable before going in ("three collectors", say).</param>
/// <param name="RequiredPartySize">
/// The exact number if the encounter imposes one; <c>null</c> if it does not (the upper limit is still 4).
/// </param>
public sealed record EncounterOffer(
    int Day,
    IReadOnlyList<Warrior> Enemies,
    ThreatBand Threat,
    string Sighting,
    int? RequiredPartySize = null)
{
    /// <summary>The raw size the reward scales with (see <c>Quartermaster.PromisedReward</c>).</summary>
    public double EnemyHealth => Enemies.Sum(e => e.EffectiveStats.MaxHealth);

    /// <summary>Can a party of the given size enter this offer?</summary>
    public bool Accepts(int partySize) =>
        partySize > 0
        && partySize <= MaxPartySize
        && (RequiredPartySize is null || partySize == RequiredPartySize);

    /// <summary>The maximum warriors who can be sent on an expedition (GDD §10, Open Decision #1).</summary>
    public const int MaxPartySize = 4;
}

/// <summary>The rough threat mark readable before going in.</summary>
/// <remarks>
/// GDD §10: the full roster and the stats are <b>invisible</b>. So that the choice is informed without
/// killing the surprise, only a band is readable. The band comes from the raw power, <b>independent of
/// the player's roster</b>, not from the enemy's strength — saying "hard for you" would be handing the
/// player the answer the fight is supposed to give.
/// </remarks>
public enum ThreatBand
{
    /// <summary>Patrol work.</summary>
    Faint,

    /// <summary>An ordinary day.</summary>
    Rising,

    /// <summary>The roster should prepare.</summary>
    Heavy,

    /// <summary>A high risk of death.</summary>
    Dire,
}
