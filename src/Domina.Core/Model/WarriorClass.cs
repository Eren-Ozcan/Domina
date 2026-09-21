using Domina.Core.Combat;

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

    /// <summary>Kyūdō — the range class. His implement is the throwing slot, and above all the yumi.</summary>
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

    /// <summary>How a man of this class reads the field, or <see cref="TargetProfile.Default"/>.</summary>
    /// <remarks>
    /// <para>
    /// The class is the one exception to §4's rule that the dojo's own men carry no appetite. The rule
    /// stands for the hired man: the player directs his side and a warrior who picked his opponent by
    /// temperament would be reading the field against him. A class is not temperament — it is a trained
    /// craft the player paid a facility for, and the craft carries its own idea of whom to strike.
    /// </para>
    /// <para>
    /// Only the <b>dokushi</b> has one. Poison ticks on its own clock, so finishing a poisoned man is
    /// work already being done: the dose wants a fresh body, not the one that is already dying. He is
    /// therefore pulled less by a wound, put off more by a teammate already on that target, and holds
    /// his own fight loosely. The torite and the kyūdō keep the default until their own round measures
    /// one for them.
    /// </para>
    /// </remarks>
    public static TargetProfile Targeting(WarriorClass klass) => klass switch
    {
        WarriorClass.Dokushi => DokushiTargeting,
        _ => TargetProfile.Default,
    };

    /// <summary>The poison class's appetite — spread the dose, the dose finishes them.</summary>
    private static TargetProfile DokushiTargeting { get; } =
        new(Wounded: 0.5, Crowd: 1.4, Stickiness: 0.6);

    /// <summary>How much of his charge appetite a man of this class keeps.</summary>
    /// <remarks>
    /// The second half of the same exception: a craft says whom to strike, and it also says how to
    /// close the ground. Only the <b>dokushi</b> has a word here, and it is a measuring knob rather
    /// than a locked number — <paramref name="poisonAppetite"/> is
    /// <see cref="Combat.CombatTuning.PoisonChargeAppetite"/> and defaults to 1.0, which is the
    /// behaviour every figure before it was measured on.
    /// </remarks>
    public static double ChargeAppetite(WarriorClass klass, double poisonAppetite) =>
        klass == WarriorClass.Dokushi ? poisonAppetite : 1.0;

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
