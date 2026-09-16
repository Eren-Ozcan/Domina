using Domina.Core.Campaign;
using Domina.Core.Model;

namespace Domina.Core.Dojo.Journal;

/// <summary>What one move of a replay turned out to be.</summary>
public enum ReplayNoteKind
{
    /// <summary>The move was made again and the dojo came out looking the same.</summary>
    Matched,

    /// <summary>The move was made again but the dojo came out looking different.</summary>
    Diverged,

    /// <summary>The move could not be made again, so the replay walked past it.</summary>
    Skipped,
}

/// <summary>One line of a replay's report.</summary>
/// <param name="Index">Which move it was, counting from 0 as the file does.</param>
/// <param name="Move">The move itself, as it was read from the journal.</param>
/// <param name="Kind">How it went.</param>
/// <param name="Message">What happened, in one sentence.</param>
public sealed record ReplayNote(int Index, Move Move, ReplayNoteKind Kind, string Message)
{
    public override string ToString() => $"#{Index} day {Move.Day} {Move.Name}: {Message}";
}

/// <summary>A replay as a whole.</summary>
/// <param name="Dojo">The dojo the journal was walked into.</param>
/// <param name="Notes">One note per move, in order.</param>
public sealed record ReplayReport(DojoState Dojo, IReadOnlyList<ReplayNote> Notes)
{
    /// <summary>Did every move come out the way the journal said it did?</summary>
    public bool Faithful => Notes.All(n => n.Kind == ReplayNoteKind.Matched);

    /// <summary>The first move that came out differently; <c>null</c> if none did.</summary>
    public ReplayNote? FirstDivergence => Notes.FirstOrDefault(n => n.Kind == ReplayNoteKind.Diverged);

    /// <summary>The moves this build could not make again.</summary>
    public IEnumerable<ReplayNote> Skipped => Notes.Where(n => n.Kind == ReplayNoteKind.Skipped);

    /// <summary>The report as text, one move a line — what a failing test prints.</summary>
    public string Describe() => string.Join('\n', Notes.Select(n => n.ToString()));
}

/// <summary>
/// Walks a journal into a fresh dojo and says where, if anywhere, the run stopped agreeing with itself.
/// </summary>
/// <remarks>
/// <para>
/// This is what the journal is for. The core is seeded and deterministic (<c>CLAUDE.md</c> →
/// "Architecture rule"), so a seed and a list of moves are a whole run: a session a player complains
/// about becomes a test by being replayed, and a balance change is checked by replaying the same
/// session against the new numbers.
/// </para>
/// <para>
/// The replay is <b>checked line by line</b> rather than at the end. Each move is made again and the
/// dojo is then compared against what the journal observed at that point — gold, stores, who is
/// standing. The first line where the two disagree is the line where the change bit, which is a far
/// more useful thing to be handed than "the season ended differently".
/// </para>
/// <para>
/// A move this build cannot make again is <b>skipped, not fatal</b>: a fight recorded without its seed
/// (an older journal), a facility since removed, a name no catalogue carries. The replay says so and
/// walks on, because the moves before it are still worth checking.
/// </para>
/// </remarks>
public static class MoveReplay
{
    /// <summary>Replays a journal into a dojo built from its own opening line.</summary>
    /// <param name="journal">The journal to walk.</param>
    /// <param name="tuning">The day-loop settings; the defaults if not given.</param>
    /// <exception cref="InvalidOperationException">If the journal carries no opening line.</exception>
    public static ReplayReport Run(MoveJournal journal, DojoTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(journal);

        if (journal.Seed is not ulong seed)
        {
            throw new InvalidOperationException(
                "The journal has no Start line, so the run's seed is unknown and it cannot be replayed.");
        }

        DojoState dojo = NewGame.Create(seed, tuning, journal.Difficulty ?? DifficultyTier.Master);
        return Run(journal, dojo);
    }

    /// <summary>Replays a journal into a dojo that has already been built.</summary>
    /// <remarks>
    /// The dojo's own journal is <b>turned off</b> for the walk: a replay that recorded itself would
    /// double every move and the second run's file would not compare with the first.
    /// </remarks>
    public static ReplayReport Run(MoveJournal journal, DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(dojo);

        bool recording = dojo.Journal.Enabled;
        dojo.Journal.Enabled = false;

        List<ReplayNote> notes = [];
        try
        {
            for (int i = 0; i < journal.Moves.Count; i++)
            {
                notes.Add(Walk(dojo, journal.Moves[i], i));
            }
        }
        finally
        {
            dojo.Journal.Enabled = recording;
        }

        return new ReplayReport(dojo, notes);
    }

    private static ReplayNote Walk(DojoState dojo, Move move, int index)
    {
        if (move.Kind == MoveKind.Start)
        {
            return new ReplayNote(index, move, ReplayNoteKind.Matched, "the run opened");
        }

        // A fault is a record of something that went wrong, not a move anybody made: it is carried
        // into the report so the walk shows where the trouble sat, and nothing is done about it.
        if (move.Kind == MoveKind.Fault)
        {
            return new ReplayNote(
                index,
                move,
                ReplayNoteKind.Matched,
                $"a fault was noted in {move.Text("where") ?? "the run"}: {move.Text("what")}");
        }

        // A day that closed inside a fight or a declined offer has already been closed by that move.
        // Its line is still checked — it carries the bill and the wounds — but it is not acted on, and
        // it is not held against the calendar either: the move it belongs to has already moved it on.
        if (move.Text("within") is not null)
        {
            return Compare(dojo, move, index);
        }

        if (move.Day != dojo.Day)
        {
            return new ReplayNote(
                index,
                move,
                ReplayNoteKind.Diverged,
                $"the journal is on day {move.Day} and the replay on day {dojo.Day}");
        }

        Attempt attempt = new();

        // A journal is a file on disk and may have been written by another build, or edited by hand.
        // A line that makes the core throw is a line this build cannot walk — it is skipped with the
        // reason, exactly like one whose facility no longer exists. The walk must not die on it.
        string? refusal;
        try
        {
            refusal = Apply(dojo, move, attempt);
        }
        catch (Exception broken) when (broken is ArgumentException or InvalidOperationException)
        {
            return new ReplayNote(
                index,
                move,
                ReplayNoteKind.Skipped,
                $"the move could not be made again: {broken.Message}");
        }

        if (refusal is not null)
        {
            return new ReplayNote(index, move, ReplayNoteKind.Skipped, refusal);
        }

        if (attempt.Ok != move.Ok)
        {
            return new ReplayNote(
                index,
                move,
                ReplayNoteKind.Diverged,
                move.Ok
                    ? "it took effect in the journal but not in the replay"
                    : "it was refused in the journal but took effect in the replay");
        }

        return Compare(dojo, move, index);
    }

    /// <summary>Holds the replayed dojo against what the journal saw at this point.</summary>
    private static ReplayNote Compare(DojoState dojo, Move move, int index)
    {
        // A move that eats the day was observed <b>before</b> the day's bill was paid, and by the time
        // the replay gets here the bill has been paid. Its own day line carries the state after the
        // closing and is compared instead, so nothing is lost by letting this one past.
        if (move.Flag("closesDay") == true)
        {
            return new ReplayNote(
                index,
                move,
                ReplayNoteKind.Matched,
                "it was made again; the day it closed is checked on its own line");
        }

        IReadOnlyList<MoveArg> now = MoveJournal.Observe(dojo, move.Ok);

        foreach (MoveArg observed in move.After)
        {
            if (observed.Name == Move.OkKey)
            {
                continue;
            }

            MoveArg? mine = now.FirstOrDefault(a => a.Name == observed.Name);
            if (mine is null || mine.Value == observed.Value)
            {
                continue;
            }

            return new ReplayNote(
                index,
                move,
                ReplayNoteKind.Diverged,
                $"{observed.Name} was {observed.Value} in the journal and is {mine.Value} in the replay");
        }

        return new ReplayNote(index, move, ReplayNoteKind.Matched, "it came out the same");
    }

    /// <summary>
    /// Makes one move again.
    /// </summary>
    /// <returns>
    /// <c>null</c> if the move was made — <paramref name="done"/> then says whether it took effect —
    /// or a sentence saying why this build could not make it.
    /// </returns>
    private static string? Apply(DojoState dojo, Move move, Attempt done)
    {
        switch (move.Kind)
        {
            case MoveKind.AdvanceDay:
                dojo.AdvanceDay();
                done.Ok = true;
                return null;

            case MoveKind.Decline:
                dojo.Decline();
                done.Ok = true;
                return null;

            case MoveKind.HireRecruit:
                if (move.Number("index") is not int index)
                {
                    return "the line carries no candidate index";
                }

                done.Ok = dojo.HireRecruit(
                    index,
                    EquipmentCatalogue.FindWeapon(move.Text("weapon")),
                    EquipmentCatalogue.FindArmor(move.Text("armor"))) is not null;
                return null;

            case MoveKind.Hire:
                if (move.Text("name") is not string hired || string.IsNullOrWhiteSpace(hired))
                {
                    return "the line names no warrior to take on";
                }

                done.Ok = dojo.Quartermaster.Hire(
                    dojo,
                    hired,
                    null,
                    EquipmentCatalogue.FindWeapon(move.Text("weapon")),
                    EquipmentCatalogue.FindArmor(move.Text("armor"))) is not null;
                return null;

            case MoveKind.Rename:
                return Warrior(
                    move,
                    id => done.Ok = dojo.RenameWarrior(id, move.Text("name") ?? string.Empty));

            case MoveKind.Release:
                return Warrior(move, id => done.Ok = dojo.Release(id));

            case MoveKind.Retire:
                return Warrior(move, id => done.Ok = dojo.Retire(id));

            case MoveKind.Feast:
                done.Ok = dojo.Feast();
                return null;

            case MoveKind.BuySake:
                done.Ok = dojo.BuySake(move.Number("measures") ?? 0) > 0;
                return null;

            case MoveKind.SetPurse:
                dojo.SetPurse(new Resources(
                    Gold: move.Number("gold") ?? 0,
                    Food: move.Number("food") ?? 0,
                    Water: move.Number("water") ?? 0,
                    Medicine: move.Number("medicine") ?? 0,
                    Sake: move.Number("sake") ?? 0));
                done.Ok = true;
                return null;

            case MoveKind.Restock:
                done.Ok = dojo.Quartermaster.Restock(
                    dojo,
                    new Resources(
                        Food: move.Number("wantFood") ?? 0,
                        Water: move.Number("wantWater") ?? 0,
                        Medicine: move.Number("wantMedicine") ?? 0)) > 0;
                return null;

            case MoveKind.BuySchoolNode:
                if (move.Choice<SchoolNodeId>("node") is not SchoolNodeId node)
                {
                    return $"this build has no facility called {move.Text("node")}";
                }

                done.Ok = dojo.BuySchoolNode(node);
                return null;

            case MoveKind.SendGift:
                if (move.Choice<Patron>("patron") is not Patron patron)
                {
                    return $"this build has no patron called {move.Text("patron")}";
                }

                done.Ok = dojo.SendGift(patron);
                return null;

            case MoveKind.AcceptBounty:
                done.Ok = dojo.AcceptBounty() is not null;
                return null;

            case MoveKind.SetDrill:
                return Warrior(move, id => done.Ok = dojo.SetDrill(id, move.Choice<Drill>("drill")));

            case MoveKind.CastVote:
                done.Ok = dojo.CastVote(move.Text("user") ?? string.Empty, move.Flag("bushi") == true);
                return null;

            case MoveKind.BuyCharm:
                return Charm(move, kind => done.Ok = dojo.BuyCharm(kind));

            case MoveKind.SellCharm:
                return Charm(move, kind => done.Ok = dojo.SellCharm(kind) > 0);

            case MoveKind.FitCharm:
            case MoveKind.UnfitCharm:
                if (move.Choice<OmamoriKind>("charm") is not OmamoriKind hung)
                {
                    return $"this build has no charm called {move.Text("charm")}";
                }

                bool fitting = move.Kind == MoveKind.FitCharm;
                return Warrior(
                    move,
                    id => done.Ok = fitting ? dojo.FitCharm(id, hung) : dojo.UnfitCharm(id, hung));

            case MoveKind.HireStaff:
                return Role(move, role => done.Ok = dojo.Hire(role));

            case MoveKind.DismissStaff:
                return Role(move, role => done.Ok = dojo.Dismiss(role));

            case MoveKind.AppointStaff:
                if (move.Choice<StaffRole>("role") is not StaffRole post)
                {
                    return $"this build has no post called {move.Text("role")}";
                }

                return Warrior(move, id => done.Ok = dojo.Appoint(id, post));

            case MoveKind.TrainClass:
                if (move.Choice<WarriorClass>("class") is not WarriorClass klass)
                {
                    return $"this build has no class called {move.Text("class")}";
                }

                return Warrior(move, id => done.Ok = dojo.TrainClass(id, klass));

            case MoveKind.ChoosePath:
                if (move.Choice<WarriorPath>("path") is not WarriorPath path)
                {
                    return $"this build has no path called {move.Text("path")}";
                }

                return Warrior(move, id => done.Ok = dojo.ChoosePath(id, path));

            case MoveKind.Repair:
                if (move.Choice<HitLocation>("slot") is not HitLocation slot)
                {
                    return "the line names no armour slot";
                }

                return OnWarrior(dojo, move, w => done.Ok = dojo.Quartermaster.Repair(dojo, w, slot));

            case MoveKind.EquipArmor:
                if (move.Choice<HitLocation>("slot") is not HitLocation fitted)
                {
                    return "the line names no armour slot";
                }

                if (EquipmentCatalogue.FindPiece(move.Text("piece")) is not ArmorPiece piece)
                {
                    return $"this build sells no armour piece called {move.Text("piece")}";
                }

                return OnWarrior(dojo, move, w => done.Ok = dojo.Quartermaster.Equip(dojo, w, fitted, piece));

            case MoveKind.EquipThrown:
                if (EquipmentCatalogue.FindThrown(move.Text("thrown")) is not ThrownWeapon thrown)
                {
                    return $"this build sells no thrown weapon called {move.Text("thrown")}";
                }

                return OnWarrior(dojo, move, w => done.Ok = dojo.Quartermaster.EquipThrown(dojo, w, thrown));

            case MoveKind.EquipWeapon:
                if (EquipmentCatalogue.FindWeapon(move.Text("weapon")) is not Weapon bought)
                {
                    return $"this build sells no weapon called {move.Text("weapon")}";
                }

                return OnWarrior(dojo, move, w => done.Ok = dojo.Quartermaster.EquipWeapon(dojo, w, bought));

            case MoveKind.ForgeWeapon:
                return OnWarrior(dojo, move, w => done.Ok = dojo.Quartermaster.Forge(dojo, w));

            case MoveKind.Fight:
                return Fight(dojo, move, done);

            case MoveKind.BountyFight:
                return BountyFight(dojo, move, done);

            case MoveKind.FinalRound:
                return FinalRound(dojo, move, done);

            case MoveKind.Unknown:
            default:
                return $"this build does not know the move {move.Name}";
        }
    }

    private static string? Fight(DojoState dojo, Move move, Attempt done)
    {
        if (move.Seed("seed") is not ulong seed)
        {
            return "the fight was recorded without its seed, so it cannot be fought again";
        }

        if (Party(dojo, move) is not List<RosterEntry> party)
        {
            return "somebody in the party is not on the replay's roster";
        }

        int posted = move.Number("posted") ?? dojo.Day;
        // The day's own offer is tried first and the board second: the two can carry the same posting
        // day, and the daily offer is the one an ordinary fight was sent against.
        EncounterOffer? offer = dojo.Offer.Day == posted
            ? dojo.Offer
            : dojo.Board.FirstOrDefault(o => o.Day == posted);

        if (offer is null)
        {
            return $"the posting of day {posted} is not on the replay's board";
        }

        new Expedition().Send(dojo, offer, party, seed, retreat: Presses(move));
        done.Ok = true;
        return null;
    }

    private static string? BountyFight(DojoState dojo, Move move, Attempt done)
    {
        if (move.Seed("seed") is not ulong seed)
        {
            return "the hunt was recorded without its seed, so it cannot be fought again";
        }

        if (Party(dojo, move) is not List<RosterEntry> party)
        {
            return "somebody in the party is not on the replay's roster";
        }

        if (dojo.Bounty is not BountyContract contract
            || contract.PostedDay != (move.Number("posted") ?? contract.PostedDay))
        {
            return "the contract the journal names is not the one posted in the replay";
        }

        new Expedition().SendToBounty(dojo, contract, party, seed, retreat: Presses(move));
        done.Ok = true;
        return null;
    }

    private static string? FinalRound(DojoState dojo, Move move, Attempt done)
    {
        if (move.Seed("seed") is not ulong seed)
        {
            return "the bout was recorded without its seed, so it cannot be fought again";
        }

        if (Party(dojo, move) is not List<RosterEntry> party)
        {
            return "somebody in the party is not on the replay's roster";
        }

        new FinalNight().Fight(dojo, party, seed, retreat: Presses(move));
        done.Ok = true;
        return null;
    }

    /// <summary>Whether the move took effect, in a shape the small helpers below can write into.</summary>
    private sealed class Attempt
    {
        public bool Ok { get; set; }
    }

    /// <summary>
    /// The pull-out presses the journal recorded, as a policy the fight can be fought against again.
    /// </summary>
    /// <remarks>
    /// A fight nobody intervened in gives <c>null</c>, which is what the resolver expects when there is
    /// no policy at all — a scripted policy with nothing in it would be the same thing said louder.
    /// </remarks>
    private static Combat.ScriptedRetreat? Presses(Move move)
    {
        string? written = move.Text("retreats");
        if (string.IsNullOrWhiteSpace(written))
        {
            return null;
        }

        List<double> seconds = [];
        foreach (string part in written.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            if (double.TryParse(
                part,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double at))
            {
                seconds.Add(at);
            }
        }

        return seconds.Count == 0 ? null : new Combat.ScriptedRetreat(seconds);
    }

    private static List<RosterEntry>? Party(DojoState dojo, Move move)
    {
        List<RosterEntry> party = [];
        foreach (int id in move.Numbers("party"))
        {
            if (dojo.Roster.Find(new WarriorId(id)) is not RosterEntry entry)
            {
                return null;
            }

            party.Add(entry);
        }

        return party.Count == 0 ? null : party;
    }

    private static string? Warrior(Move move, Action<WarriorId> act)
    {
        if (move.Number("warrior") is not int id)
        {
            return "the line names no warrior";
        }

        act(new WarriorId(id));
        return null;
    }

    private static string? OnWarrior(DojoState dojo, Move move, Action<Warrior> act)
    {
        if (move.Number("warrior") is not int id)
        {
            return "the line names no warrior";
        }

        if (dojo.Roster.Find(new WarriorId(id)) is not RosterEntry entry)
        {
            return $"warrior {id} is not on the replay's roster";
        }

        act(entry.Warrior);
        return null;
    }

    private static string? Charm(Move move, Action<OmamoriKind> act)
    {
        if (move.Choice<OmamoriKind>("charm") is not OmamoriKind kind)
        {
            return $"this build has no charm called {move.Text("charm")}";
        }

        act(kind);
        return null;
    }

    private static string? Role(Move move, Action<StaffRole> act)
    {
        if (move.Choice<StaffRole>("role") is not StaffRole role)
        {
            return $"this build has no post called {move.Text("role")}";
        }

        act(role);
        return null;
    }
}
