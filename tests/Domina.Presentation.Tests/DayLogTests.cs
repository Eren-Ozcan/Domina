using Domina.Core.Campaign;
using Domina.Core.Dojo;
using Domina.Core.Honor;
using Domina.Core.Model;
using Domina.Presentation;

namespace Domina.Presentation.Tests;

/// <summary>
/// A closed day as the player reads it. It matters more since build step 8 than it did while the day
/// was a button: with the clock running, the log is the <b>only</b> place a rival's move, a verdict or
/// a payroll that emptied is ever seen, so a field that carries news and is not printed here is a
/// field the player never learns about.
/// </summary>
public class DayLogTests
{
    private static DayReport Day(
        DayEvent? happening = null,
        bool bountyBroken = false,
        bool missedWeek = false,
        double honorLost = 0,
        IReadOnlySet<WarriorId>? hungry = null,
        IReadOnlyList<StaffRole>? walked = null,
        IReadOnlyList<SchoolNodeId>? opened = null,
        ProvinceMove? rivalMove = null,
        SackReport? sacked = null,
        TribunalVerdict? tribunal = null) =>
        new(
            Day: 12,
            Recovered: [],
            Trained: [],
            Upkeep: new UpkeepReport(
                GoldSpent: 14,
                Food: 3,
                Water: 3,
                Medicine: 0,
                Hungry: hungry ?? new HashSet<WarriorId>(),
                Medicated: new HashSet<WarriorId>(),
                Walked: walked),
            Event: happening,
            BountyBroken: bountyBroken,
            Opened: opened,
            MissedWeek: missedWeek,
            HonorLost: honorLost,
            Tribunal: tribunal,
            RivalMove: rivalMove,
            Sacked: sacked);

    /// <summary>A day that did nothing says so in one line and stops.</summary>
    [Fact]
    public void AQuietDayIsOneLine()
    {
        IReadOnlyList<string> lines = DayLog.Lines(Day());

        Assert.Single(lines);
        Assert.Contains("Day 12 closed", lines[0]);
        Assert.Contains("14 gold", lines[0]);
    }

    /// <summary>The staff walking out is the day's news, not a number in the upkeep table.</summary>
    [Fact]
    public void AnEmptiedPayrollNamesWhoWalked()
    {
        string text = DayLog.Line(Day(walked: [StaffRole.Physician]));

        Assert.Contains("payroll", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("walked out", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The honour the quiet week cost is printed, because nothing else on screen shows it.</summary>
    [Fact]
    public void AMissedWeekPrintsWhatItCost()
    {
        string text = DayLog.Line(Day(missedWeek: true, honorLost: 15));

        Assert.Contains("15 honour", text);
    }

    /// <summary>The rival's three kinds of move read differently — a raid is not a warning.</summary>
    [Theory]
    [InlineData(ProvinceMoveKind.Pressed, "pressed")]
    [InlineData(ProvinceMoveKind.Taken, "took")]
    [InlineData(ProvinceMoveKind.Raid, "gate")]
    public void TheRivalsMoveIsPrinted(ProvinceMoveKind kind, string expected)
    {
        string text = DayLog.Line(Day(rivalMove: new ProvinceMove(kind, Settlement: 2, Warning: 1)));

        Assert.Contains(expected, text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A raid nobody answered took something: the amounts are named.</summary>
    [Fact]
    public void ASackNamesWhatWasTaken()
    {
        string text = DayLog.Line(Day(sacked: new SackReport(Gold: 120, Food: 8)));

        Assert.Contains("120 gold", text);
        Assert.Contains("8 food", text);
    }

    /// <summary>A verdict is the one line of the day that can end a man.</summary>
    [Fact]
    public void ATribunalVerdictSaysWhichWayItWent()
    {
        TribunalVerdict seppuku = new(
            new WarriorId(3),
            "Kenji",
            SeppukuOutcome.Seppuku,
            CrowdVerdict.Silent,
            DecidedByAudience: false);

        TribunalVerdict pardon = seppuku with { Outcome = SeppukuOutcome.Pardoned };

        Assert.Contains("took his own life", DayLog.Line(Day(tribunal: seppuku)));
        Assert.Contains("pardoned", DayLog.Line(Day(tribunal: pardon)));
    }

    /// <summary>Everything that happened on one day is printed on that day — no item hides another.</summary>
    [Fact]
    public void ACrowdedDayPrintsEveryItem()
    {
        IReadOnlyList<string> lines = DayLog.Lines(Day(
            happening: null,
            bountyBroken: true,
            missedWeek: true,
            honorLost: 5,
            hungry: new HashSet<WarriorId> { new(1) },
            opened: [SchoolNodeId.Infirmary],
            rivalMove: new ProvinceMove(ProvinceMoveKind.Taken, 4, 0),
            sacked: new SackReport(30, 2)));

        Assert.Equal(7, lines.Count);
    }
}
