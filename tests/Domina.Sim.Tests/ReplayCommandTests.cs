using Domina.Core.Campaign;
using Domina.Core.Dojo;
using Domina.Core.Dojo.Journal;
using Domina.Sim;

namespace Domina.Sim.Tests;

/// <summary>
/// The journal's consumer: <c>--replay</c>. The decision protected is the one the whole journal rests
/// on — a run written down by the game must be walkable in a terminal, without the engine, and it must
/// say <b>where</b> it stopped agreeing rather than merely that it did.
/// </summary>
public class ReplayCommandTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "domina-replay-" + Guid.NewGuid().ToString("N"));

    public ReplayCommandTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A temporary folder that will not go is not worth failing a test over.
        }
    }

    /// <summary>A run written down and walked again comes out the same, and the tool says so.</summary>
    [Fact]
    public void AJournalOfAnHonestRunReplaysClean()
    {
        string path = Write(Play(5));

        StringWriter output = new();
        StringWriter error = new();

        int code = SimCli.Run([ReplayCommand.Flag, path], output, error);

        Assert.Equal(SimCli.ExitOk, code);
        Assert.Contains("came out the same", output.ToString(), StringComparison.Ordinal);
        Assert.Empty(error.ToString());
    }

    /// <summary>
    /// A journal whose moves no longer produce the same run fails, and the failing line is named. This
    /// is what a retuning is held against.
    /// </summary>
    [Fact]
    public void AJournalThatNoLongerHoldsFailsAndNamesTheLine()
    {
        DojoState played = Play(5);

        // What the run <b>observed</b> is moved under its feet, and only that: the moves themselves are
        // left alone, so the replay makes the same decisions and sees different money — which is the
        // shape a balance change has.
        string tampered = played.Journal.ToJsonl()
            .Replace("\"after\":{\"ok\":true,\"gold\":", "\"after\":{\"ok\":true,\"gold\":9", StringComparison.Ordinal);

        string path = Path.Combine(_folder, "tampered.jsonl");
        File.WriteAllText(path, tampered);

        StringWriter output = new();

        int code = SimCli.Run([ReplayCommand.Flag, path], output, new StringWriter());

        Assert.Equal(ReplayCommand.ExitDiverged, code);
        Assert.Contains("diverged at", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>The faults the run filed are printed whatever the verdict — they are why it arrived.</summary>
    [Fact]
    public void TheFaultsTheRunFiledArePrinted()
    {
        DojoState played = Play(5);
        played.RecordFault("day", "the day would not close");

        StringWriter output = new();

        SimCli.Run([ReplayCommand.Flag, Write(played)], output, new StringWriter());

        Assert.Contains("fault", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("the day would not close", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>A path that is not there is an IO failure, not a crash and not a false pass.</summary>
    [Fact]
    public void AMissingFileIsReportedRatherThanThrown()
    {
        StringWriter error = new();

        int code = SimCli.Run(
            [ReplayCommand.Flag, Path.Combine(_folder, "nothing.jsonl")],
            new StringWriter(),
            error);

        Assert.Equal(SimCli.ExitIoError, code);
        Assert.Contains("No moves could be read", error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>The flag with nothing after it is a usage error, and the usage is printed.</summary>
    [Fact]
    public void TheFlagWithoutAPathPrintsTheUsage()
    {
        StringWriter error = new();

        int code = SimCli.Run([ReplayCommand.Flag], new StringWriter(), error);

        Assert.Equal(SimCli.ExitUsage, code);
        Assert.Contains("--replay", error.ToString(), StringComparison.Ordinal);
    }

    private string Write(DojoState dojo)
    {
        string path = Path.Combine(_folder, "moves.jsonl");
        dojo.Journal.Save(path);
        return path;
    }

    /// <summary>A few days of ordinary play — drills, a purse topped up, a fight, the days between.</summary>
    private static DojoState Play(ulong seed)
    {
        DojoState dojo = NewGame.Create(seed);
        dojo.SetPurse(dojo.Resources with { Gold = 900, Food = 40, Water = 40 });

        foreach (RosterEntry entry in dojo.Roster.Living)
        {
            dojo.SetDrill(entry.Id, Drill.Strikes);
        }

        dojo.AdvanceDay();

        EncounterOffer offer = dojo.Offer;
        List<RosterEntry> party = [.. dojo.Roster.FitForCampaign.Take(offer.RequiredPartySize ?? 2)];
        if (party.Count > 0 && Expedition.Refuse(dojo, offer, party) is null)
        {
            new Expedition().Send(dojo, offer, party, 909ul);
        }
        else
        {
            dojo.Decline();
        }

        dojo.AdvanceDay();
        return dojo;
    }
}
