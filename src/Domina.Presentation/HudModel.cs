using System.Globalization;
using Domina.Core.Combat;

namespace Domina.Presentation;

/// <summary>The current state of the "pull out" key.</summary>
/// <param name="Text">What the key says.</param>
/// <param name="Enabled">Can it be pressed?</param>
/// <param name="Locked">Should it stress that the command will be delayed for at least one warrior?</param>
/// <param name="Shut">
/// Will the press be <b>refused</b> because the fight has not started yet? The key stays pressable in
/// this state too — a refused press produces the text that teaches the rule; a key that cannot be
/// pressed would say nothing.
/// </param>
public readonly record struct RetreatPrompt(
    string Text,
    bool Enabled,
    bool Locked,
    bool Shut = false);

/// <summary>The kind of answer given to a press before contact.</summary>
public enum RetreatNoticeKind
{
    /// <summary>There is nothing to say.</summary>
    None,

    /// <summary>The text that teaches the rule. Shown a limited number of times at the start of the game.</summary>
    Teaching,

    /// <summary>The answer given to someone who keeps pressing. It also unlocks the achievement.</summary>
    Taunt,
}

/// <summary>The interface's answer to a refused press.</summary>
/// <param name="Kind">The kind of answer.</param>
/// <param name="Text">The text to show; empty if <see cref="RetreatNoticeKind.None"/>.</param>
/// <param name="AchievementId">The achievement unlocked, or null.</param>
public readonly record struct RetreatRefusalNotice(
    RetreatNoticeKind Kind,
    string Text,
    string? AchievementId);

/// <summary>
/// Works out what the interface should say. It produces text, it does not draw.
/// </summary>
public static class HudModel
{
    /// <summary>
    /// The state of the single key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Surrender is the only intervention point in a fight (GDD §5) and it is <b>a single key</b>: the
    /// command pulls the whole team, no warrior is selected. Were it per-warrior, the right play would be
    /// "pull the wounded one, continue with the rest"; a single key makes the decision rare and heavy.
    /// </para>
    /// <para>
    /// The key shows <b>how many warriors the command will take effect on immediately</b>: the command is
    /// buffered for warriors locked into a strike and the escape only starts when the strike finishes.
    /// The player must see this before pressing, or the delay feels like a bug.
    /// </para>
    /// </remarks>
    public static RetreatPrompt DescribeRetreat(
        IReadOnlyList<CombatantSnapshot> snapshots,
        bool contactMade)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        int standing = 0;
        int locked = 0;
        int leaving = 0;

        foreach (CombatantSnapshot snapshot in snapshots)
        {
            if (snapshot.Team != Battle.PlayerTeam || !snapshot.IsActive)
            {
                continue;
            }

            if (snapshot.RetreatRequested || snapshot.State == CombatState.Retreating)
            {
                leaving++;
                continue;
            }

            standing++;

            if (!snapshot.CanCancel)
            {
                locked++;
            }
        }

        if (!contactMade)
        {
            // No pulling out before the fight starts (§5). The key stays visible but cannot be pressed:
            // hidden, the rule's existence would never be learnt.
            return new RetreatPrompt(
                "FIGHT NOT STARTED", Enabled: true, Locked: false, Shut: true);
        }

        if (standing == 0)
        {
            return new RetreatPrompt(leaving > 0 ? "PARTY PULLING OUT" : "—", Enabled: false, Locked: false);
        }

        string text = locked == 0
            ? $"PULL THE PARTY ({standing})"
            : $"PULL THE PARTY ({standing}) · {locked} locked";

        return new RetreatPrompt(text, Enabled: true, Locked: locked > 0);
    }

    /// <summary>
    /// The status shown on the warrior's panel.
    /// </summary>
    /// <remarks>
    /// A warrior whose command is buffered is written separately. Before the key is pressed it says how
    /// many are locked; after it is pressed it must still be readable which ones are still waiting, or
    /// during the delay the panel says "attacking" and the command looks swallowed.
    /// </remarks>
    public static string DescribeState(in CombatantSnapshot snapshot)
    {
        // Poison is not a state but a mark laid on top of the state: a poisoned warrior keeps walking,
        // striking and pulling out. That is why the panel writes it as an addition rather than a separate
        // line — and what the player needs to see is exactly "still fighting but losing health".
        string mark = snapshot.Poisoned && snapshot.IsActive ? " · poisoned" : string.Empty;

        // Being unarmed is a mark of the same kind: the warrior keeps fighting, only with his fists — and
        // while walking to the weapon on the ground. Both must be readable at once; a poisoned and
        // unarmed warrior is the player's decision to press the key itself.
        if (snapshot.Disarmed && snapshot.IsActive)
        {
            mark += " · unarmed";
        }

        if (snapshot.RetreatRequested && snapshot.IsActive && snapshot.State != CombatState.Retreating)
        {
            return "pulling out · when the strike ends" + mark;
        }

        string label = snapshot.State switch
        {
            CombatState.Idle => "waiting",
            CombatState.AttackWindup => "attacking",
            CombatState.AttackRecovery => "recovering",
            CombatState.ChargeWindup => "gathering",
            CombatState.Charging => "charging",
            CombatState.Stunned => "stunned",
            CombatState.WeaponBound => "weapon caught",
            CombatState.Retreating => "pulling out",
            CombatState.Escaped => "escaped",
            CombatState.Dead => "dead",
            _ => string.Empty,
        };

        return label.Length == 0 ? label : label + mark;
    }

    /// <summary>
    /// The top line: the seed and the time.
    /// </summary>
    /// <remarks>
    /// The seed must stay permanently visible — it is the only way to reopen a fight and find its
    /// counterpart in batch simulation (see <c>Domina.Sim</c>).
    /// </remarks>
    public static string DescribeStatus(long seed, double elapsedSeconds, BattleOutcome? outcome)
    {
        string head = string.Create(
            CultureInfo.InvariantCulture,
            $"seed {seed}  ·  {elapsedSeconds:F1} s");

        return outcome is null ? head : $"{head}  ·  {DescribeOutcome(outcome.Value)}";
    }

    public static string DescribeOutcome(BattleOutcome outcome) => outcome switch
    {
        BattleOutcome.PlayerVictory => "VICTORY",
        BattleOutcome.PlayerWithdrawal => "WITHDREW",
        BattleOutcome.PlayerWipe => "ROUT",
        _ => "TIME LIMIT",
    };

    /// <summary>What is shown for a press waiting for the first hit before contact.</summary>
    private const string TeachingText =
        "There is no pulling out before the fight starts. The key opens when first blood is drawn.";

    private const string TauntText =
        "Nobody has touched you yet. Do not be such a coward.";

    /// <summary>The achievement that answers an insistent press.</summary>
    public const string CowardAchievementId = "dont-roll-up-your-trousers-before-you-see-the-stream";

    /// <summary>The maximum number of times the teaching text is shown over the game.</summary>
    public const int TeachingNoticeLimit = 3;

    /// <summary>The number of consecutive presses that triggers the mocking answer.</summary>
    public const int TauntPressCount = 11;

    /// <summary>
    /// What answer to give to a key pressed before contact.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule is not explained again in every fight: it is shown <see cref="TeachingNoticeLimit"/>
    /// times, then falls silent. Saying it again is repeating to the player what he knows.
    /// </para>
    /// <para>
    /// How many times it has been shown is information belonging to <b>the save</b>, not to the fight —
    /// which is why it is supplied from outside. The number of consecutive presses is in-fight and comes
    /// from the core with <c>RetreatRefused</c>.
    /// </para>
    /// </remarks>
    /// <param name="consecutivePresses">The number of consecutive refused presses in this fight.</param>
    /// <param name="teachingNoticesShown">How many times the teaching text has been shown so far.</param>
    public static RetreatRefusalNotice DescribeRefusal(
        int consecutivePresses,
        int teachingNoticesShown)
    {
        if (consecutivePresses == TauntPressCount)
        {
            return new RetreatRefusalNotice(RetreatNoticeKind.Taunt, TauntText, CowardAchievementId);
        }

        if (teachingNoticesShown < TeachingNoticeLimit)
        {
            return new RetreatRefusalNotice(RetreatNoticeKind.Teaching, TeachingText, null);
        }

        return new RetreatRefusalNotice(RetreatNoticeKind.None, string.Empty, null);
    }
}
