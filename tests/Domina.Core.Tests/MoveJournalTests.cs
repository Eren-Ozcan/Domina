using Domina.Core.Campaign;
using Domina.Core.Combat;
using Domina.Core.Dojo;
using Domina.Core.Dojo.Journal;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Tests;

/// <summary>
/// The move journal and its replay. The decision protected: <b>a seed plus the journal is the whole
/// run</b>. The core is seeded and deterministic, so a session can be walked again in a test instead of
/// being described in a bug report — and if a balance change moves a run, the replay must say which
/// line it moved at, not merely that the season ended differently.
/// </summary>
public class MoveJournalTests
{
    private static DojoState Dojo(ulong seed = 7) => NewGame.Create(seed);

    /// <summary>The opening line is what a replay rebuilds the dojo from; without it there is no run.</summary>
    [Fact]
    public void ANewGameOpensItsJournalWithTheSeedAndTheTier()
    {
        DojoState dojo = NewGame.Create(42, tier: DifficultyTier.Master);

        Move opening = dojo.Journal.Moves[0];

        Assert.Equal(MoveKind.Start, opening.Kind);
        Assert.Equal(42ul, dojo.Journal.Seed);
        Assert.Equal(DifficultyTier.Master, dojo.Journal.Difficulty);
    }

    /// <summary>Every decision is written down, and the arguments are the ones the method was given.</summary>
    [Fact]
    public void ADecisionIsWrittenDownWithItsOwnArguments()
    {
        DojoState dojo = Dojo();
        dojo.SetPurse(dojo.Resources with { Gold = 2000 });

        dojo.HireRecruit(0, Weapon.Nodachi(), Armor.Medium());

        Move hire = dojo.Journal.Moves[^1];

        Assert.Equal(MoveKind.HireRecruit, hire.Kind);
        Assert.Equal(0, hire.Number("index"));
        Assert.Equal("Nodachi", hire.Text("weapon"));
        Assert.True(hire.Ok);
        Assert.Equal(dojo.Resources.Gold, hire.Number("gold", hire.After));
    }

    /// <summary>
    /// A refusal is data: "I pressed it and nothing happened" is the shape most bug reports arrive in,
    /// and a journal of successes alone would delete exactly that report.
    /// </summary>
    [Fact]
    public void ARefusedMoveIsWrittenDownToo()
    {
        DojoState dojo = Dojo();
        dojo.SetPurse(dojo.Resources with { Gold = 0 });

        Assert.Null(dojo.HireRecruit(0));

        Move refused = dojo.Journal.Moves[^1];

        Assert.Equal(MoveKind.HireRecruit, refused.Kind);
        Assert.False(refused.Ok);
    }

    /// <summary>The day's bill is written out item by item — where the gold went, and to whom.</summary>
    [Fact]
    public void TheDayWritesItsBillAndItsTrainingIntoTheDetail()
    {
        DojoState dojo = Dojo();
        dojo.SetPurse(dojo.Resources with { Gold = 1000, Food = 40, Water = 40 });

        RosterEntry student = dojo.Roster.Living.First();
        dojo.SetDrill(student.Id, Drill.Strikes);
        dojo.AdvanceDay();

        Move day = dojo.Journal.Moves.Last(m => m.Kind == MoveKind.AdvanceDay);

        MoveRow bill = day.Detail.First(r => r.Text("what") == "upkeep");
        Assert.Equal(day.Number("spent").ToString(), bill.Text("gold"));

        MoveRow drilled = day.Detail.First(
            r => r.Text("what") == "trained" && r.Text("warrior") == student.Id.Value.ToString());
        Assert.Equal("Strikes", drilled.Text("drill"));
        Assert.Equal(student.Name, drilled.Text("name"));
    }

    /// <summary>A fight writes a row per man: what he dealt, what he took, what it cost him.</summary>
    [Fact]
    public void AFightWritesEveryWarriorsBooks()
    {
        DojoState dojo = Dojo();
        dojo.SetPurse(dojo.Resources with { Gold = 900, Food = 40, Water = 40 });

        EncounterOffer offer = dojo.Offer;
        List<RosterEntry> party = [.. dojo.Roster.FitForCampaign.Take(offer.RequiredPartySize ?? 2)];
        new Expedition().Send(dojo, offer, party, 31ul);

        Move fight = dojo.Journal.Moves.First(m => m.Kind == MoveKind.Fight);

        Assert.Equal(31ul, fight.Seed("seed"));
        Assert.Equal(party.Select(e => e.Id.Value), fight.Numbers("party"));
        Assert.Contains(fight.Detail, r => r.Text("what") == "ours");
        Assert.Contains(fight.Detail, r => r.Text("what") == "enemy");

        MoveRow mine = fight.Detail.First(r => r.Text("warrior") == party[0].Id.Value.ToString());
        Assert.NotNull(mine.Text("damageDealt"));
        Assert.NotNull(mine.Text("damageTaken"));
        Assert.NotNull(mine.Text("recoveryDays"));
        Assert.NotNull(mine.Text("lostParts"));
    }

    /// <summary>A journal survives the round trip through its file, detail and all.</summary>
    [Fact]
    public void TheFileReadsBackIntoTheSameMoves()
    {
        DojoState dojo = Dojo();
        dojo.SetPurse(dojo.Resources with { Gold = 1200, Food = 40, Water = 40 });
        dojo.BuySake(3);
        dojo.AdvanceDay();

        MoveJournal read = MoveJournal.Parse(dojo.Journal.ToJsonl());

        Assert.Empty(read.Warnings);
        Assert.Equal(dojo.Journal.Count, read.Count);
        Assert.Equal(dojo.Journal.ToJsonl(), read.ToJsonl());
    }

    /// <summary>
    /// A file cut short by a crash still loads: the save's own rule (GDD §2) applies here, because the
    /// run that crashed is precisely the run worth replaying.
    /// </summary>
    [Fact]
    public void ABrokenLineDoesNotCostTheWholeFile()
    {
        DojoState dojo = Dojo();
        dojo.AdvanceDay();

        string file = dojo.Journal.ToJsonl() + "{\"day\":3,\"move\":\"Adva";
        MoveJournal read = MoveJournal.Parse(file);

        Assert.Equal(dojo.Journal.Count, read.Count);
        Assert.Single(read.Warnings);
    }

    /// <summary>A move a later build wrote is kept under its own name rather than thrown away.</summary>
    [Fact]
    public void AMoveThisBuildDoesNotKnowIsKeptNotDropped()
    {
        MoveJournal read = MoveJournal.Parse(
            "{\"day\":4,\"move\":\"BuyHorse\",\"breed\":\"Kiso\",\"after\":{\"ok\":true,\"gold\":10}}");

        Move unknown = read.Moves.Single();

        Assert.Equal(MoveKind.Unknown, unknown.Kind);
        Assert.Equal("BuyHorse", unknown.Name);
        Assert.Equal("Kiso", unknown.Text("breed"));
    }

    /// <summary>
    /// The whole point: a run walked again from its journal comes out the same, line by line. The
    /// comparison is made at every move, so a divergence names the move it started at.
    /// </summary>
    [Fact]
    public void AReplayedRunComesOutTheSame()
    {
        DojoState played = Play(11);

        ReplayReport report = MoveReplay.Run(MoveJournal.Parse(played.Journal.ToJsonl()));

        Assert.True(report.Faithful, report.Describe());
        Assert.Equal(played.Day, report.Dojo.Day);
        Assert.Equal(played.Resources.Gold, report.Dojo.Resources.Gold);
        Assert.Equal(
            played.Roster.Entries.Select(e => (e.Name, e.Warrior.IsAlive, e.RecoveryDaysRemaining)),
            report.Dojo.Roster.Entries.Select(e => (e.Name, e.Warrior.IsAlive, e.RecoveryDaysRemaining)));
    }

    /// <summary>A replay that drifts says <b>where</b> — that is the difference between a test and a shrug.</summary>
    [Fact]
    public void AReplayThatDriftsNamesTheMoveItDriftedAt()
    {
        DojoState played = Play(11);
        MoveJournal tampered = MoveJournal.Parse(played.Journal.ToJsonl());

        // A dojo that starts on a different seed opens with a different roster and a different board,
        // which is exactly the shape a balance change has when it bites.
        ReplayReport report = MoveReplay.Run(tampered, NewGame.Create(12));

        Assert.False(report.Faithful);
        Assert.NotNull(report.FirstDivergence);
    }

    /// <summary>The batch runner turns recording off; nothing is then written and nothing is allocated.</summary>
    [Fact]
    public void ADisabledJournalWritesNothing()
    {
        DojoState dojo = Dojo();
        dojo.Journal.Clear();
        dojo.Journal.Enabled = false;

        dojo.AdvanceDay();
        dojo.Feast();

        Assert.Equal(0, dojo.Journal.Count);
    }

    /// <summary>Renaming a man is a mark the player leaves, so it is a move like any other.</summary>
    [Fact]
    public void RenamingAWarriorIsAMoveAndReplaysAsOne()
    {
        DojoState dojo = Dojo();
        RosterEntry man = dojo.Roster.Living.First();

        Assert.True(dojo.RenameWarrior(man.Id, "Tsubaki"));

        Move renamed = dojo.Journal.Moves[^1];
        Assert.Equal(MoveKind.Rename, renamed.Kind);
        Assert.Equal("Tsubaki", renamed.Text("name"));

        ReplayReport report = MoveReplay.Run(MoveJournal.Parse(dojo.Journal.ToJsonl()));

        Assert.True(report.Faithful, report.Describe());
        Assert.Equal("Tsubaki", report.Dojo.Roster.Find(man.Id)!.Name);
    }

    /// <summary>A name already on the roster is refused — and the refusal is written down.</summary>
    [Fact]
    public void ARefusedRenameIsWrittenDownToo()
    {
        DojoState dojo = Dojo();
        List<RosterEntry> men = [.. dojo.Roster.Living];

        Assert.False(dojo.RenameWarrior(men[0].Id, men[1].Name));
        Assert.False(dojo.Journal.Moves[^1].Ok);
    }

    /// <summary>A voice at the tribunal decides a verdict the seed does not, so it is a move.</summary>
    [Fact]
    public void AChatVoiceIsAMove()
    {
        DojoState dojo = Dojo();

        // Nobody is standing, so the voice is not counted — and that refusal is the line.
        Assert.False(dojo.CastVote("viewer", bushi: true));

        Move vote = dojo.Journal.Moves[^1];
        Assert.Equal(MoveKind.CastVote, vote.Kind);
        Assert.Equal("viewer", vote.Text("user"));
        Assert.False(vote.Ok);
    }

    /// <summary>
    /// A watched fight is the one place the player reaches inside the resolver. The presses are written
    /// down with the fight, so the replay pulls out at the same seconds and the fight comes out the same.
    /// </summary>
    [Fact]
    public void AWatchedFightsPullOutPressesAreWrittenDownAndReplayed()
    {
        DojoState dojo = Dojo(3);

        EncounterOffer offer = dojo.Offer;
        List<RosterEntry> party = [.. dojo.Roster.FitForCampaign.Take(offer.RequiredPartySize ?? 2)];
        BattleSetup setup = Expedition.Prepare(dojo, offer, party);

        // The fight is stepped the way the arena steps it, and the key is pressed part of the way in.
        Battle battle = new(setup, new SeededRandom(77));
        bool pressed = false;
        while (battle.Step())
        {
            if (!pressed && battle.ElapsedSeconds >= 3)
            {
                battle.CommandRetreat();
                pressed = true;
            }
        }

        new Expedition().Settle(dojo, setup, battle.Result!, offer, 77ul);

        Move fight = dojo.Journal.Moves.First(m => m.Kind == MoveKind.Fight);
        Assert.False(string.IsNullOrEmpty(fight.Text("retreats")));

        ReplayReport report = MoveReplay.Run(MoveJournal.Parse(dojo.Journal.ToJsonl()));

        Assert.True(report.Faithful, report.Describe());
        Assert.Equal(dojo.Resources.Gold, report.Dojo.Resources.Gold);
    }

    /// <summary>
    /// Every move this build can write, this build can also read back. A kind that the journal writes
    /// but the replay has never heard of would be a silent hole in the run.
    /// </summary>
    [Fact]
    public void EveryMoveKindIsUnderstoodByTheReplay()
    {
        foreach (MoveKind kind in Enum.GetValues<MoveKind>())
        {
            if (kind is MoveKind.Unknown or MoveKind.Start)
            {
                continue;
            }

            MoveJournal journal = MoveJournal.Parse(
                new Move(1, kind, [], [MoveArg.Of(Move.OkKey, false)]).ToJson());

            ReplayNote note = MoveReplay.Run(journal, Dojo()).Notes[0];

            Assert.DoesNotContain("does not know the move", note.Message);
        }
    }

    /// <summary>
    /// What broke goes into the same file as what was done, in the same order. That order is the whole
    /// value of it: an exception on its own says little, an exception with the moves before it is a test.
    /// </summary>
    [Fact]
    public void AFaultIsFiledBesideTheMovesThatLedToIt()
    {
        DojoState dojo = Dojo();
        dojo.BuySake(1);

        dojo.RecordFault("day", new InvalidOperationException("the day would not close"));

        Move fault = dojo.Journal.Moves[^1];

        Assert.Equal(MoveKind.Fault, fault.Kind);
        Assert.Equal("day", fault.Text("where"));
        Assert.Contains("the day would not close", fault.Text("what"));
        Assert.False(fault.Ok);
        Assert.Equal(MoveKind.BuySake, dojo.Journal.Moves[^2].Kind);
        Assert.Single(dojo.Journal.Faults);
    }

    /// <summary>A fault survives the file and is carried into the replay's report without being acted on.</summary>
    [Fact]
    public void AFaultIsCarriedThroughTheFileAndTheReplay()
    {
        DojoState dojo = Dojo();
        dojo.RecordFault("save", "the save loaded incompletely", ["one warrior was skipped"]);

        MoveJournal read = MoveJournal.Parse(dojo.Journal.ToJsonl());

        Move fault = read.Faults.Single();
        Assert.Equal("one warrior was skipped", fault.Detail[0].Text("line"));

        ReplayReport report = MoveReplay.Run(read);

        Assert.True(report.Faithful, report.Describe());
        Assert.Contains("a fault was noted", report.Notes[^1].Message);
    }

    /// <summary>A few days of ordinary play: shopping, drills, a fight, and the days in between.</summary>
    private static DojoState Play(ulong seed)
    {
        // Nothing is injected into the treasury here on purpose: a run is only replayable if every
        // change came through a move, and assigning Resources by hand is the one door that goes round
        // the journal (it exists for measurement setups and for tests that do not replay).
        DojoState dojo = NewGame.Create(seed);

        dojo.BuySake(2);
        foreach (RosterEntry entry in dojo.Roster.Living)
        {
            dojo.SetDrill(entry.Id, Drill.Strikes);
        }

        dojo.AdvanceDay();

        EncounterOffer offer = dojo.Offer;
        List<RosterEntry> party = [.. dojo.Roster.FitForCampaign.Take(offer.RequiredPartySize ?? 2)];
        if (party.Count > 0 && Expedition.Refuse(dojo, offer, party) is null)
        {
            new Expedition().Send(dojo, offer, party, 404ul);
        }
        else
        {
            dojo.Decline();
        }

        dojo.AdvanceDay();
        return dojo;
    }
}
