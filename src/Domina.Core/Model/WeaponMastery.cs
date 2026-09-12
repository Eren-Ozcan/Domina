namespace Domina.Core.Model;

/// <summary>
/// What a warrior has learned about the weapon in his hand (docs/GDD.md §10).
/// </summary>
/// <remarks>
/// <para>
/// This is the weapon master's system, and the only thing in the game that is learned <b>per weapon</b>
/// rather than per warrior. GDD §10's wording is the rule: the mastery "stays with the warrior and does
/// not go if the master is cut" — the post buys the <b>rate</b>, never the mastery already earned.
/// </para>
/// <para>
/// It is deliberately not a stat. A stat is trained toward a ceiling and carries over to whatever the
/// warrior picks up; mastery is tied to the name of the weapon, so putting a katana man behind a yari
/// costs him everything he built. That is the decision the system exists for — the dojo's kit is a
/// commitment, not a shopping list.
/// </para>
/// <para>
/// The value is a share (0-1) of the distance to full mastery, closed the same way training closes the
/// gap to a ceiling, so a weapon becomes good quickly and perfect never.
/// </para>
/// </remarks>
public sealed class WeaponMastery
{
    private readonly Dictionary<string, double> _learned = new(StringComparer.Ordinal);

    /// <summary>What has been learned, weapon by weapon — the pairs that go into the save.</summary>
    public IReadOnlyDictionary<string, double> Learned => _learned;

    /// <summary>The share mastered of this weapon (0-1); 0 for one never carried.</summary>
    public double Of(string weapon) =>
        _learned.TryGetValue(weapon, out double value) ? value : 0;

    /// <summary>
    /// Closes a share of the distance left to full mastery and returns the new value.
    /// </summary>
    /// <remarks>
    /// The same share-of-the-gap shape as <see cref="Dojo.TrainingGround"/>: the first days with a new
    /// weapon are worth far more than the fiftieth, so a warrior who changes weapon is not starting a
    /// long grind — he is giving up the last, thin part of what he had.
    /// </remarks>
    public double Grow(string weapon, double share)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(weapon);

        double current = Of(weapon);
        double gained = current + ((1 - current) * Math.Clamp(share, 0, 1));
        _learned[weapon] = gained;
        return gained;
    }

    /// <summary>Writes a value straight in — for the save, and for building a test bed.</summary>
    public void Set(string weapon, double value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(weapon);

        _learned[weapon] = Math.Clamp(value, 0, 1);
    }
}

/// <summary>What full mastery of a weapon is worth in the fight.</summary>
/// <param name="AccuracyAtFull">The share added to Accuracy at mastery 1.0.</param>
/// <remarks>
/// <para>
/// Mastery lands on <b>one</b> stat on purpose. Morale already showed what a multiplier across six
/// stats does — it compounds far harder than it reads — and mastery is the second such multiplier in
/// the game. Accuracy is the stat that says "he knows where this weapon lands", and it is the one axis
/// a heavy weapon needs most, so the bonus also leans where the kit is weakest.
/// </para>
/// <para>
/// A balance number, so it is carried on the warrior like <see cref="MoraleBand"/> and never written to
/// a save (docs/GDD.md §2).
/// </para>
/// </remarks>
public readonly record struct MasteryBand(double AccuracyAtFull)
{
    /// <summary>The default band — not locked until it is swept in <c>Domina.Sim</c>.</summary>
    public static MasteryBand Default { get; } = new(0.10);

    /// <summary>The multiplier this much mastery puts on Accuracy.</summary>
    public double FactorFor(double mastery) =>
        1 + (AccuracyAtFull * Math.Clamp(mastery, 0, 1));
}
