using Domina.Core.Combat;
using Domina.Core.Model;

namespace Domina.Core.Dojo.Journal;

/// <summary>
/// A finished fight written out warrior by warrior, for the journal's detail rows.
/// </summary>
/// <remarks>
/// <para>
/// The fight's own result and what the dojo then wrote to the roster are two different books
/// (<see cref="BattleResult"/> and <see cref="AftermathReport"/>): the first knows how the blows fell,
/// the second knows what the dojo is left carrying. A question asked afterwards — which limb did that
/// cost, how much damage did he take before he went down, what did the fight teach him — needs both,
/// so they are joined here into one row per man.
/// </para>
/// <para>
/// Both sides of the field are written, the enemies included. An enemy's row costs one line and is the
/// only record that the encounter was as heavy as the board said it would be.
/// </para>
/// </remarks>
public static class FightDetail
{
    /// <summary>One row per warrior on either side.</summary>
    /// <param name="battle">The fight as the resolver finished it.</param>
    /// <param name="aftermath">What the dojo then wrote to the roster; the enemies have none.</param>
    public static IReadOnlyList<MoveRow> Of(BattleResult battle, AftermathReport? aftermath = null)
    {
        ArgumentNullException.ThrowIfNull(battle);

        List<MoveRow> rows = [];
        foreach (WarriorBattleSummary summary in battle.Summaries)
        {
            WarriorAftermath? books = aftermath?.Warriors.FirstOrDefault(w => w.Id == summary.Id);
            rows.Add(Row(summary, books));
        }

        return rows;
    }

    /// <summary>
    /// The pull-out presses as one argument — the only part of a fight the seed does not decide.
    /// </summary>
    /// <remarks>
    /// It is an <b>argument</b> and not an observation: a replay has to hand these back
    /// (<see cref="ScriptedRetreat"/>) or a watched fight would come out differently from the fight
    /// that was watched. A fight nobody intervened in writes an empty value and costs nothing.
    /// </remarks>
    public static MoveArg Presses(BattleResult battle)
    {
        ArgumentNullException.ThrowIfNull(battle);

        return MoveArg.Of(
            "retreats",
            string.Join('|', battle.RetreatPresses.Select(
                t => Math.Round(t, 3).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))));
    }

    private static MoveRow Row(WarriorBattleSummary summary, WarriorAftermath? books)
    {
        List<MoveArg> args =
        [
            MoveArg.Of("what", summary.Team == 0 ? "ours" : "enemy"),
            MoveArg.Of("warrior", summary.Id.Value),
            MoveArg.Of("name", summary.Name),
            MoveArg.Of("state", summary.FinalState),
            MoveArg.Of("healthLeft", summary.HealthRemaining),
            MoveArg.Of("damageDealt", summary.DamageDealt),
            MoveArg.Of("damageTaken", summary.DamageTaken),
            MoveArg.Of("attacks", summary.AttacksMade),
            MoveArg.Of("hits", summary.HitsLanded),
            MoveArg.Of("timesHit", summary.TimesHit),
            MoveArg.Of("dodges", summary.DodgesPerformed),
            MoveArg.Of("panicked", summary.Panicked),

            // The limbs are the detail the fiction is built on: a lost arm closes classes, changes the
            // weapon he can hold and follows him for the rest of the run (docs/GDD.md §7).
            MoveArg.Of("lostParts", Parts(summary.LostParts)),
        ];

        if (books is not null)
        {
            args.AddRange(
            [
                MoveArg.Of("died", books.Died),
                MoveArg.Of("pulledBack", books.PulledBack),
                MoveArg.Of("recoveryDays", books.RecoveryDays),
                MoveArg.Of("honor", books.HonorDelta),
                MoveArg.Of("kept", string.Join('|', books.SavedParts)),
                MoveArg.Of("armorBroken", string.Join('|', books.ShatteredArmor)),
                MoveArg.Of("lesson", books.Lesson),
            ]);
        }

        return new MoveRow(args);
    }

    private static string Parts(BodyPartSet lost) => string.Join('|', lost.Parts());
}
