using Domina.Sim;

namespace Domina.Sim.Tests;

/// <summary>
/// The played season's harness: <c>--play</c>. What is protected here is not the core's arithmetic but
/// the <b>telling</b> of it — every one of these tests stands for a place where a hand-played season
/// gave the player a number he could not act on, or a refusal he could not read.
/// </summary>
public class PlayCommandTests : IDisposable
{
    private const ulong Seed = 7101;

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "domina-play-" + Guid.NewGuid().ToString("N"));

    public PlayCommandTests() => Directory.CreateDirectory(_folder);

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

    /// <summary>
    /// A warrior's number is his for the season: a hire does not hand it to somebody else.
    /// </summary>
    /// <remarks>
    /// The roster was printed alphabetically and numbered by position, so a hire renumbered the men
    /// under it and a queued <c>drill 6</c> reached a different warrior than the one the player had
    /// read. Two veterans were put on the wrong exercise that way in a played season.
    /// </remarks>
    [Fact]
    public void ANumberFollowsTheManAndNotHisPlaceInTheList()
    {
        string standing = Play("hire 4", "drill 0 Guard");

        string row = Row(standing, "0  ");
        Assert.Contains("Warrior 1", row, StringComparison.Ordinal);
        Assert.Contains("Guard", row, StringComparison.Ordinal);
    }

    /// <summary>The wage is a lease, so the standing prices it and there is a move that ends it.</summary>
    /// <remarks>
    /// Hiring staff costs nothing at the counter and then takes its wage out of every day that
    /// follows. Two played seasons died with a full purse and no men at 40-44 gold a day, and neither
    /// player had a line to read the bill from or a move to stop it — the core's <c>Dismiss</c> was
    /// there all along and the harness had only the hiring half of it.
    /// </remarks>
    [Fact]
    public void StaffSayWhatTheyCostAndCanBeLetGo()
    {
        string hired = Play("build Shrine", "day", "day", "day", "day", "day", "staff Monk");
        Assert.Contains("payroll 4 gold a day", hired, StringComparison.Ordinal);

        string gone = Play("build Shrine", "day", "day", "day", "day", "day", "staff Monk", "dismiss Monk");
        Assert.Contains("staff: nobody", gone, StringComparison.Ordinal);
        Assert.Contains("payroll falls to 0 gold a day", gone, StringComparison.Ordinal);
    }

    /// <summary>A post that cannot be filled names the building it belongs to.</summary>
    [Fact]
    public void AnEmptyPostNamesItsBuilding()
    {
        string standing = Play("staff Monk");

        Assert.Contains("a Monk works out of the Shrine", standing, StringComparison.Ordinal);
    }

    /// <summary>The moves on one man say which rule turned them down.</summary>
    /// <remarks>
    /// <c>path</c>, <c>retire</c>, <c>class</c> and <c>fit</c> all printed the same six words — "the
    /// move was refused" — and a player burned four turns guessing at tenure, training days, a hall he
    /// had not built and a slot that was full.
    /// </remarks>
    [Fact]
    public void ARefusedMoveNamesTheRuleThatRefusedIt()
    {
        string standing = Play("retire 0", "path 0 Blade", "class 0 Torite", "fit 0 SteadyHand");

        Assert.Contains("the house asks for 12", standing, StringComparison.Ordinal);
        Assert.Contains("training day(s) of the 20 it asks for", standing, StringComparison.Ordinal);
        Assert.Contains("taught in the ToriteHall", standing, StringComparison.Ordinal);
        Assert.Contains("no SteadyHand is in the store", standing, StringComparison.Ordinal);
    }

    /// <summary>The party can be named, so the bench is reachable.</summary>
    /// <remarks>
    /// The party was taken off the top of the roster by quality and nothing else could be asked of it,
    /// so the bottom of the bench never fought, never gained and was raw on the night it was needed.
    /// </remarks>
    [Fact]
    public void ThePartyCanBeNamedManByMan()
    {
        Assert.DoesNotContain("REFUSED", Play("expedition 0 men:0,1"), StringComparison.Ordinal);

        string wrong = Play("expedition 0 men:9");
        Assert.Contains("there is no warrior 9 on the roster", wrong, StringComparison.Ordinal);
    }

    /// <summary>The tribunal's threshold is on the screen before it kills anybody.</summary>
    [Fact]
    public void TheStandingSaysWhatHonourCosts()
    {
        string standing = Play("day");

        Assert.Contains("called before the tribunal", standing, StringComparison.Ordinal);
        Assert.Contains("seppuku unless the crowd pardons him", standing, StringComparison.Ordinal);

        // The line used to promise that winning lifts honour and sitting out drops it. Both halves
        // were wrong — the fight's honour is the man's own hit rate — and a played season lost a
        // veteran to the tribunal while reading the screen correctly.
        Assert.Contains("by how well HE fought, not by whether the party won", standing, StringComparison.Ordinal);
    }

    /// <summary>The board leads with the count, and says what the threat word is worth.</summary>
    [Fact]
    public void TheBoardSaysTheThreatWordIsABand()
    {
        string standing = Play();

        Assert.Contains("the day's band", standing, StringComparison.Ordinal);
        Assert.Matches(@"\d+ enem(y|ies), health \d+ \(\d+ each\)", standing);
    }

    /// <summary>A night asked for too early says when it opens.</summary>
    /// <remarks>
    /// A player who read the standing on day 180 and typed <c>night</c> was told only that the season
    /// was "Running", and missed the climax he had played 180 days for: the phase turns when the last
    /// day <b>closes</b>.
    /// </remarks>
    [Fact]
    public void TheLastNightSaysWhenItOpens()
    {
        string standing = Play("night");

        Assert.Contains("it opens when day 180 closes", standing, StringComparison.Ordinal);
    }

    /// <summary>An oversized hunting party is refused, not thrown at the player.</summary>
    /// <remarks>
    /// The expedition asks the core whether the party may go and prints the refusal; the hunt asked
    /// nothing and handed the party straight to a core that throws rather than refuses. A played
    /// season died on that throw — the whole run, on one line — and the contract had never printed
    /// the party range the board prints on every job.
    /// </remarks>
    [Fact]
    public void AnOversizedHuntingPartyIsRefusedAndTheContractSaysTheRange()
    {
        string standing = Play("hire 0", "hire 4", "accept", "bounty men:0,1,2,3,4,5");

        Assert.Contains("WrongPartySize — a contract takes 1-4 men, 6 were sent", standing, StringComparison.Ordinal);
        Assert.Contains("send 1-4", standing, StringComparison.Ordinal);
    }

    /// <summary>The order that could not be given says which rule stopped it.</summary>
    /// <remarks>
    /// "the order was refused" was the last refusal in the harness that named no rule: a player with
    /// 1,273 gold in the chest ordered a building whose prerequisite was not standing, was told
    /// nothing, and never found out why. The drill list went the same way — every other enum refusal
    /// printed its whole set and this one did not, so five drills cost eighteen guesses to find.
    /// </remarks>
    [Fact]
    public void ARefusedOrderAndAnUnknownDrillNameTheirRule()
    {
        Assert.Contains(
            "the Broker is built onto the Patron, and that is not standing",
            Play("build Broker"),
            StringComparison.Ordinal);

        Assert.Contains(
            "they are Strikes, Guard, Footwork, Conditioning, Meditation",
            Play("drill 0 Xyz"),
            StringComparison.Ordinal);
    }

    /// <summary>Sake can be bought, and the store prints its prices.</summary>
    /// <remarks>
    /// The core has had <c>BuySake</c> since the feast landed and the harness had only the drinking
    /// half, so a played season met "it drinks 10 sake (4 in the store)" with no move that could ever
    /// reach ten. Sake was not in the purse either, and no price was printed anywhere: two players
    /// worked the market out by subtracting purses across restocks.
    /// </remarks>
    [Fact]
    public void SakeIsBoughtByTheMeasureAndThePricesAreOnTheScreen()
    {
        string standing = Play("sake 12", "feast");

        Assert.Contains("bought 12 measure(s) of sake", standing, StringComparison.Ordinal);
        Assert.Contains("| sake 8", standing, StringComparison.Ordinal);
        Assert.Matches(@"food \d+g, water \d+g, medicine \d+g, sake \d+g", standing);
    }

    /// <summary>An open gate says what the night actually is.</summary>
    /// <remarks>
    /// The header said "the last night will be fought" from the day the gate opened and nothing more,
    /// in one season for 171 days. Four of six played seasons reached the night with one good party of
    /// four and met a chain of five bouts that carries its wounds and ends on the first loss; two of
    /// them said in as many words that they would have played the whole season differently.
    /// </remarks>
    [Fact]
    public void AnOpenGateSaysTheNightIsAChainThatEndsOnTheFirstLoss()
    {
        string shut = Play();
        Assert.DoesNotContain("the night is 5 bouts", shut, StringComparison.Ordinal);

        List<string> heads = ["restock 40 40 6"];
        for (int hunt = 0; hunt < 25; hunt++)
        {
            heads.AddRange(["accept", "bounty 4", "day", "restock 40 40 6"]);
        }

        string open = Play([.. heads]);
        Assert.Contains("gate open", open, StringComparison.Ordinal);
        Assert.Contains("the night is 5 bouts", open, StringComparison.Ordinal);
        Assert.Contains("the FIRST bout lost ends the season", open, StringComparison.Ordinal);
    }

    private static string Row(string standing, string number) =>
        standing
            .Split('\n')
            .First(line => line.TrimStart().StartsWith(number, StringComparison.Ordinal));

    private string Play(params string[] script)
    {
        string path = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllLines(path, script);

        StringWriter output = new();
        int code = PlayCommand.Run(output, ["--play", path, "--seed", Seed.ToString()]);

        Assert.Equal(0, code);
        return output.ToString();
    }
}
