namespace Domina.Core.Model;

/// <summary>
/// What a warrior is able to <b>do</b> — the layer that opens the special mechanics.
/// </summary>
/// <remarks>
/// <para>
/// The class stands beside <see cref="WarriorPath"/>, it does not replace it (docs/GDD.md §4): the
/// path is a pure stat tendency, the class is a capability. A class is trained once the matching
/// facility stands in the dojo; losing a limb reopens the choice, the path never.
/// </para>
/// <para>
/// The mechanic is a <b>product</b>: <c>chance = base × class × implement</c>. The identity belongs
/// to the warrior and the efficiency to the equipment — a master who drops his weapon weakens but
/// does not become someone else, and putting a jitte in a recruit's hand does not make a master.
/// </para>
/// </remarks>
public enum WarriorClass
{
    /// <summary>No class. He fights, he does not catch (docs/GDD.md §4).</summary>
    None,

    /// <summary>Torite — the catching class. The jitte and the sai are his implements.</summary>
    Torite,

    /// <summary>Dokushi — the poison class. He carries the dose the plate cannot read.</summary>
    Dokushi,

    /// <summary>Kyūdō — the range class. His implement is the throwing slot, and later the yumi.</summary>
    Kyudo,
}

/// <summary>
/// The class side of the <c>class × implement</c> product, and the limb-class fitness matrix.
/// </summary>
/// <remarks>
/// The numbers themselves live in <c>CombatTuning</c> so the sim can sweep them; what lives here is
/// the <b>shape</b> of the rule — which class opens which mechanic, and which class a maimed warrior
/// can still take up.
/// </remarks>
public static class ClassAptitude
{
    /// <summary>
    /// Is catching open to this class at all?
    /// </summary>
    /// <remarks>
    /// This is the one hard zero of the system (decided 2026-09-10). A warrior with no class catches
    /// nothing even holding a jitte: catching is an active skill, not a property of the hook. Poison
    /// and range were deliberately left open to everyone and only <b>scaled</b> by the class — a
    /// poisoned tantō sitting in the store as dead equipment until a facility is built would have
    /// thrown away measurements that are already locked (docs/GDD.md §7).
    /// </remarks>
    public static bool CanCatch(WarriorClass klass) => klass == WarriorClass.Torite;

    /// <summary>The class share of a dose — full for the poison class, reduced for everyone else.</summary>
    public static double PoisonFactor(WarriorClass klass, double unclassedFactor) =>
        klass == WarriorClass.Dokushi ? 1.0 : unclassedFactor;

    /// <summary>The class share of a throw — full for the range class, reduced for everyone else.</summary>
    public static double RangeFactor(WarriorClass klass, double unclassedFactor) =>
        klass == WarriorClass.Kyudo ? 1.0 : unclassedFactor;

    /// <summary>
    /// Can a warrior carrying these losses take up this class?
    /// </summary>
    /// <remarks>
    /// The fitness matrix of docs/GDD.md §4. An arm ends both the catching implement (it is held in
    /// the off hand while the sword hand works) and the bow (it is two-handed); the dose is carried
    /// on the blade, so poison survives every loss. This is what turns limb loss from a leak into a
    /// second career: the maimed warrior chooses again among what is left.
    /// </remarks>
    public static bool IsPossibleFor(WarriorClass klass, IReadOnlyList<Disability> disabilities)
    {
        ArgumentNullException.ThrowIfNull(disabilities);

        bool armLost = false;
        foreach (Disability d in disabilities)
        {
            if (d.Part.IsArm())
            {
                armLost = true;
            }
        }

        return klass switch
        {
            WarriorClass.Torite => !armLost,
            WarriorClass.Kyudo => !armLost,
            _ => true,
        };
    }

    /// <summary>The classes a warrior with these losses may choose from.</summary>
    public static IReadOnlyList<WarriorClass> ChoicesFor(IReadOnlyList<Disability> disabilities)
    {
        List<WarriorClass> open = [];
        foreach (WarriorClass klass in Enum.GetValues<WarriorClass>())
        {
            if (klass != WarriorClass.None && IsPossibleFor(klass, disabilities))
            {
                open.Add(klass);
            }
        }

        return open;
    }
}
