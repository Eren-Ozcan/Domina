namespace Domina.Core.Dojo;

/// <summary>The dojo's treasury and store.</summary>
/// <remarks>
/// <para>
/// The resource <b>kinds</b> come from GDD §11 (gold, food/water, medicine); the <b>numbers</b> do not
/// — prices, daily consumption and the starting stock sit in Open Decision #5. So there is not a single
/// constant here: the type is a value carrier, and its arithmetic and the question "is it enough" can
/// be written without waiting for a locked number.
/// </para>
/// <para>
/// The numbers are integers: a resource is something countable, and fractional gold is lost to one
/// side's rounding and does not match exactly across save and load.
/// </para>
/// </remarks>
/// <param name="Sake">
/// The fourth stock (Open Decision #14, closed 2026-09-10). It is not consumed daily like food: it is
/// bought and it sits there until the player calls a <b>feast</b>. That is what makes it a decision —
/// a resource with a daily drain would only be a second food, and one with no cost would be a button.
/// </param>
public readonly record struct Resources(
    int Gold = 0,
    int Food = 0,
    int Water = 0,
    int Medicine = 0,
    int Sake = 0)
{
    public static Resources Empty { get; }

    public static Resources operator +(Resources a, Resources b) => new(
        a.Gold + b.Gold,
        a.Food + b.Food,
        a.Water + b.Water,
        a.Medicine + b.Medicine,
        a.Sake + b.Sake);

    public static Resources operator -(Resources a, Resources b) => new(
        a.Gold - b.Gold,
        a.Food - b.Food,
        a.Water - b.Water,
        a.Medicine - b.Medicine,
        a.Sake - b.Sake);

    /// <summary>Can it cover the given cost?</summary>
    public bool Covers(Resources cost) =>
        Gold >= cost.Gold
        && Food >= cost.Food
        && Water >= cost.Water
        && Medicine >= cost.Medicine
        && Sake >= cost.Sake;

    /// <summary>Has any item gone negative?</summary>
    public bool AnyNegative => Gold < 0 || Food < 0 || Water < 0 || Medicine < 0 || Sake < 0;

    /// <summary>Pulls negatives to zero — reporting which items went short is the caller's job.</summary>
    public Resources ClampedToZero() => new(
        Math.Max(0, Gold),
        Math.Max(0, Food),
        Math.Max(0, Water),
        Math.Max(0, Medicine),
        Math.Max(0, Sake));
}
