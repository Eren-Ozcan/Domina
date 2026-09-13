using Domina.Core.Model;

namespace Domina.Core.Dojo;

/// <summary>A warrior's daily state in the dojo — everything the fight does not know.</summary>
/// <remarks>
/// <para>
/// <see cref="Model.Warrior"/> is the persistent state the fight reads; this record carries the
/// <b>meta</b> state around it: how many days in the infirmary, what he is doing today, how many days
/// he has trained. The reason for the separation is the architecture rule: the combat resolver does not
/// know the day loop, and batch simulation runs the same warrior tens of thousands of times — a calendar is meaningless there.
/// </para>
/// </remarks>
public sealed class RosterEntry
{
    public RosterEntry(Warrior warrior)
    {
        ArgumentNullException.ThrowIfNull(warrior);
        Warrior = warrior;
    }

    public Warrior Warrior { get; }

    public WarriorId Id => Warrior.Id;

    public string Name => Warrior.Name;

    /// <summary>The days that must pass before the warrior can go on an expedition.</summary>
    /// <remarks>
    /// It fills according to the severity of the wound (see docs/GDD.md §7 "Recovery"). Natural recovery
    /// burns one day a day; the infirmary and medicine speed that up.
    /// </remarks>
    public int RecoveryDaysRemaining { get; internal set; }

    /// <summary>Today's occupation. A warrior in the infirmary cannot train.</summary>
    public DojoActivity Activity { get; internal set; } = DojoActivity.Resting;

    /// <summary>The training days completed so far.</summary>
    public int TrainingDays { get; internal set; }

    /// <summary>The subject of the training day.</summary>
    /// <remarks>
    /// A separate field from the occupation: so the drill is not forgotten when a warrior goes into the
    /// infirmary and comes out, and so the player can choose the drill <b>in advance</b> — this is what is applied when the day closes.
    /// </remarks>
    public Drill Drill { get; internal set; } = Drill.Strikes;

    /// <summary>
    /// Has his term ended — did he walk out of the dojo free (docs/GDD.md §10)?
    /// </summary>
    /// <remarks>
    /// A released man is <b>neither dead nor on the roster</b>: he eats nothing, trains nothing and
    /// cannot be sent anywhere, but he is not a loss either — he is the season's other score. His record
    /// stays for the same reason a dead man's does, and his name goes back into the pool.
    /// </remarks>
    public bool Released { get; internal set; }

    /// <summary>
    /// Has he left the field for good and become a master of the house (docs/GDD.md §10)?
    /// </summary>
    /// <remarks>
    /// A retired man is the long game's one reward: he draws <b>no wage</b>, he eats nothing more from
    /// the day's bill, and he never takes the field again. He is not gone the way a released man is —
    /// he is still in the dojo, and what he is now good for is a <b>post</b>.
    /// </remarks>
    public bool Retired { get; internal set; }

    /// <summary>The fights he came back from — what earns a man his retirement.</summary>
    /// <remarks>
    /// Counted rather than derived: a warrior's stats say what he is worth today and nothing says what
    /// he has already given. It is the number the retirement gate reads, and it is written to the save
    /// because it is state the season produced.
    /// </remarks>
    public int Victories { get; internal set; }

    /// <summary>Can he be sent on an expedition?</summary>
    public bool IsFitForCampaign =>
        Warrior.IsAlive && !Released && !Retired && RecoveryDaysRemaining == 0;

    /// <summary>Puts the warrior in the infirmary. A longer stay overrides a shorter one, never the reverse.</summary>
    public void Injure(int days)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(days);

        if (days > RecoveryDaysRemaining)
        {
            RecoveryDaysRemaining = days;
        }

        if (RecoveryDaysRemaining > 0)
        {
            Activity = DojoActivity.Recovering;
        }
    }

    /// <summary>Puts him on training today. A warrior in the infirmary is not accepted.</summary>
    /// <remarks>
    /// If no drill is given, the last one chosen continues. The gain is applied when the day closes
    /// (<see cref="DojoState.AdvanceDay"/>): a warrior left hungry does not advance that day.
    /// </remarks>
    public bool Train(Drill? drill = null)
    {
        if (!IsFitForCampaign)
        {
            return false;
        }

        if (drill is Drill wanted)
        {
            Drill = wanted;
        }

        Activity = DojoActivity.Training;
        return true;
    }

    public void Rest() => Activity = RecoveryDaysRemaining > 0
        ? DojoActivity.Recovering
        : DojoActivity.Resting;
}

/// <summary>A warrior's occupation that day.</summary>
public enum DojoActivity
{
    /// <summary>Idle — neither training nor the infirmary.</summary>
    Resting,

    /// <summary>On the training ground.</summary>
    Training,

    /// <summary>In the infirmary; cannot go on an expedition, cannot train.</summary>
    Recovering,
}
