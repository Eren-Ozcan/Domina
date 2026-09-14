namespace Domina.Core.Dojo.Journal;

/// <summary>Every move the journal knows how to write down.</summary>
/// <remarks>
/// <para>
/// A move is <b>a decision the player made</b>, not a consequence of one. The day's upkeep, a wound,
/// a verdict and a facility finishing construction are all written into the day's report instead: they
/// follow from the seed and from the moves already made, so recording them would only let a replay
/// disagree with itself.
/// </para>
/// <para>
/// The enum is an open list on the reading side: a file written by a later build may carry a move this
/// build has never heard of, and <see cref="MoveJournal.Parse"/> keeps such a line as
/// <see cref="Unknown"/> with its original name instead of throwing the whole file away.
/// </para>
/// </remarks>
public enum MoveKind
{
    /// <summary>A move this build does not know; its written name is kept in the move's arguments.</summary>
    Unknown = 0,

    /// <summary>The run's opening line: the seed and the tier every later move is read against.</summary>
    Start,

    /// <summary>
    /// Something went wrong: an exception caught, a fight that hit the stall guard, a save that
    /// loaded incompletely.
    /// </summary>
    /// <remarks>
    /// It is not a move — nobody decided it — but it belongs in the same file and in the same order.
    /// A bug report is "it broke", and the only question worth asking afterwards is <b>what was the
    /// run doing at that moment</b>. The line before the fault is that answer.
    /// </remarks>
    Fault,

    /// <summary>The day was closed.</summary>
    AdvanceDay,

    /// <summary>The day's job was turned down and the day closed with it.</summary>
    Decline,

    /// <summary>A candidate was bought from the stall.</summary>
    HireRecruit,

    /// <summary>A warrior was taken on by name — the sim's own way in, and the tests'.</summary>
    Hire,

    /// <summary>A warrior was given a new name.</summary>
    Rename,

    /// <summary>A warrior's term was ended; he walked out of the gate.</summary>
    Release,

    /// <summary>A warrior left the field for good and became a master of the house.</summary>
    Retire,

    /// <summary>A feast was held.</summary>
    Feast,

    /// <summary>Sake was bought.</summary>
    BuySake,

    /// <summary>
    /// The purse was set from outside — a test or a tool putting gold and stores into a running dojo.
    /// </summary>
    /// <remarks>
    /// Nothing in the game does this. It is a move rather than a hole because a run that was topped up
    /// has to replay like any other, and a silent change to the treasury would make every line after it
    /// disagree with the journal for no visible reason.
    /// </remarks>
    SetPurse,

    /// <summary>The stores were topped up at the market.</summary>
    Restock,

    /// <summary>A facility was begun.</summary>
    BuySchoolNode,

    /// <summary>A gift was sent to a patron.</summary>
    SendGift,

    /// <summary>Today's contract was accepted.</summary>
    AcceptBounty,

    /// <summary>A drill was set for a warrior, which also puts him on the training ground.</summary>
    SetDrill,

    /// <summary>A voice was counted at the tribunal — chat speaking for or against the man standing.</summary>
    CastVote,

    /// <summary>A charm was bought from the temple.</summary>
    BuyCharm,

    /// <summary>A charm was sold back to the temple.</summary>
    SellCharm,

    /// <summary>A charm was hung on a warrior.</summary>
    FitCharm,

    /// <summary>A charm was taken off a warrior.</summary>
    UnfitCharm,

    /// <summary>A post was filled from outside; the dojo took on a wage.</summary>
    HireStaff,

    /// <summary>A master of the house was put into a post.</summary>
    AppointStaff,

    /// <summary>A post holder was let go.</summary>
    DismissStaff,

    /// <summary>A warrior was trained into a class.</summary>
    TrainClass,

    /// <summary>A warrior's path was chosen — once, with no way back.</summary>
    ChoosePath,

    /// <summary>A piece of armour was repaired.</summary>
    Repair,

    /// <summary>A piece of armour was bought and put on.</summary>
    EquipArmor,

    /// <summary>A thrown weapon was bought.</summary>
    EquipThrown,

    /// <summary>A weapon was taken to the forge.</summary>
    ForgeWeapon,

    /// <summary>A party was sent to a posting.</summary>
    Fight,

    /// <summary>A party was sent after a contract's head.</summary>
    BountyFight,

    /// <summary>A round of the final night was fought.</summary>
    FinalRound,
}
