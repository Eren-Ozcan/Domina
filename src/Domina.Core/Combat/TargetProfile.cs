namespace Domina.Core.Combat;

/// <summary>
/// How one warrior weighs the field when he chooses whom to strike (docs/GDD.md §4, Open Decision #3).
/// </summary>
/// <remarks>
/// <para>
/// The scoring itself lives in one place (<c>Battle.TargetScore</c>) and every warrior runs the same
/// five terms: the road to be walked, the wounded man, the bare region, the teammate already on that
/// target, and the cost of turning away from his own. What a profile changes is only <b>how much each
/// term is worth to this man</b>. So an enemy's character is not a second code path — it is the same
/// decision taken with different appetites, which is what GDD §4 asked for when it left the kinds'
/// behaviour open.
/// </para>
/// <para>
/// The fields are <b>multipliers over <see cref="CombatTuning"/>'s weights</b>, not absolute values.
/// That keeps the balance in one place: a round that retunes what a bare region is worth moves every
/// kind with it, and a profile stays a statement about character rather than a second set of numbers
/// to maintain. <see cref="Default"/> is all ones and therefore reproduces the behaviour every figure
/// measured before this type existed.
/// </para>
/// </remarks>
/// <param name="Distance">
/// How dearly he holds the road he must walk. Above 1 he will not cross the arena for an opportunity —
/// the slow man with the heavy weapon swings at whoever is in front of him.
/// </param>
/// <param name="Wounded">How much a wounded enemy draws him.</param>
/// <param name="Exposed">How much a broken armour region draws him.</param>
/// <param name="Crowd">
/// How much a teammate already on that target puts him off. Below 1 he piles on (a gang), above 1 he
/// spreads out (a man who wants his own opponent).
/// </param>
/// <param name="Stickiness">
/// What it costs him to turn away from the man he is already fighting. Above 1 he sees his fight
/// through; below 1 he goes wherever the opportunity is.
/// </param>
public sealed record TargetProfile(
    double Distance = 1,
    double Wounded = 1,
    double Exposed = 1,
    double Crowd = 1,
    double Stickiness = 1)
{
    /// <summary>Every term at its measured weight — the behaviour of every fight before profiles existed.</summary>
    public static TargetProfile Default { get; } = new();
}
