namespace Domina.Core.Campaign;

/// <summary>The season's tunable numbers (docs/GDD.md §10).</summary>
/// <remarks>
/// The season is the whole horizon the long-horizon economy pass measured: 180 days, one compulsory
/// fight a week, three heads to enter the last night, five bouts in that night. The only number here
/// that is a <b>balance</b> knob rather than a design constant is
/// <see cref="MissedWeekHonorPenalty"/> — the rest were settled in the decision pass.
/// </remarks>
public sealed record SeasonTuning
{
    /// <summary>The season's length. Day 1 opens it, <see cref="Days"/> closes it.</summary>
    public int Days { get; init; } = 180;

    /// <summary>How often the compulsory-fight tick lands.</summary>
    /// <remarks>
    /// One tick, two consequences (GDD §10): the week the dojo files no fight costs honour, and the
    /// rival's move lands on the same day. Only the first is written here — the settlement map is not
    /// in the core yet, so what this exposes of the second is the counter
    /// (<see cref="DaysToTick"/>) the screen stands on.
    /// </remarks>
    public int CompulsoryFightDays { get; init; } = 7;

    /// <summary>
    /// What a week with no fight filed costs <b>every living warrior's</b> honour.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The penalty is honour and not gold for the reason it always was: a rich dojo would simply
    /// <b>buy</b> the safe training loop and the endless-training exploit would stay open. Because
    /// honour also pushes toward the seppuku threshold (GDD §6), the price of hiding accumulates.
    /// </para>
    /// <para>
    /// <b>Locked at 5</b> (200 hiding dojos against 200 ordinary ones, 180 days each). The floor is
    /// hard: honour's own decay pulls a warrior 0.5 a day back toward neutral, which is 3.5 a week, so
    /// anything at or below 4 is <b>inert</b> — the hiding dojo settled at honour 41 and not one of its
    /// warriors ever reached the seppuku threshold. At 5 the hider is at 22 and 99% of them cross by
    /// day 91, while the ordinary dojo ends at 47 and its crossing rate rises from the 8.5% it has
    /// without any penalty at all to 21%. Higher values buy speed with collateral: 6 puts 41.5% of
    /// <b>ordinary</b> dojos below the threshold and 8 puts 59% there, for a hider who was already
    /// going to be punished. 5 is the value that bites the exploit without turning a bad month into a
    /// death sentence.
    /// </para>
    /// </remarks>
    public double MissedWeekHonorPenalty { get; init; } = 5;

    /// <summary>
    /// The consecutive missed weeks that cost nothing before the penalty starts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An ordinary dojo — one that fights 40-57 times in a season — still closes 11-13 of its 25 weeks
    /// with no fight filed: a party comes home wounded, the infirmary takes the week, the market has
    /// nobody worth buying. A flat weekly penalty cannot tell that dojo apart from one that is hiding,
    /// and every value big enough to bite a hider drove three quarters of the <b>honest</b> dojos below
    /// the seppuku threshold.
    /// </para>
    /// <para>
    /// The first thing measured against that was this grace week, on the theory that hiding is
    /// consecutive and bad luck is scattered. <b>The measurement said otherwise</b>: the ordinary dojo's
    /// longest run of missed weeks was 12.9 — its quiet weeks are one long block at the end of the
    /// season, exactly the shape a hider's are, so a grace of one week separated nothing (paid weeks
    /// 12.3 of 13.4). What does the separating is <see cref="FitDaysBeforeCharged"/>. The grace week is
    /// kept because it is still worth something on its own terms — the first quiet week after a mauling
    /// costs nothing — but it is not the rule that closes the exploit, and it should not be credited
    /// with it.
    /// </para>
    /// </remarks>
    public int GraceWeeks { get; init; } = 1;

    /// <summary>
    /// The days in the week on which the dojo had somebody fit to send before the week counts as hiding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured, and it is what makes the rule fair.</b> A week with no fight filed is not proof of
    /// hiding: an ordinary dojo spends long stretches with its party in the infirmary, and measurement
    /// showed those stretches are not scattered — the honest dojo's missed weeks come in one long run
    /// at the end, exactly the shape a hider's do. What separates them is not the pattern but
    /// <b>whether there was anyone to send</b>: hiding is choosing not to take the field, and a roster
    /// lying in the infirmary is not choosing anything. A dojo that had men standing for fewer than
    /// this many days of the week pays nothing.
    /// </para>
    /// <para>
    /// The threshold itself is a <b>decision knob and not a survival one</b>: swept 1-7, the ordinary
    /// dojo's paid weeks moved 5.5 → 4.9 and its crossing rate 42.5% → 38.5%, while the hider did not
    /// move at all. What did the work was the condition existing — with no fit test the ordinary dojo
    /// paid for 12.3 weeks instead of 5.1, because its quiet weeks are the ones with its party in the
    /// infirmary. Four days is the majority of the week.
    /// </para>
    /// </remarks>
    public int FitDaysBeforeCharged { get; init; } = 4;

    /// <summary>The heads that must be brought in before the last night opens.</summary>
    /// <remarks>
    /// Deliberately small: bounty hunting is hard as it is — a selective dojo was entering 0.69
    /// contracts in 60 days in measurement, and a gate of five would be a wall rather than a gate.
    /// </remarks>
    public int BountyGate { get; init; } = 3;

    /// <summary>
    /// The powers the five bouts come at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The night crosses the difficulty curve's own ceiling (<c>EncounterTuning.MaxPower</c> = 2.2)
    /// rather than starting there: it opens under what the late season was offering and ends above
    /// anything it ever did. The fifth is a single man at the top of the list — Kurogane himself, who
    /// stands there because a candidate who sends others in his place has conceded the post.
    /// </para>
    /// <para>
    /// <b>Measured, in two passes, and the first pass was measuring the wrong dojo.</b> Against a dojo
    /// that never built a facility, never filled a post and never chose a path, the night at these
    /// powers was unwinnable and the numbers were pulled down to 1.8-2.8 to compensate. Re-measured
    /// against a dojo that plays the game — school, staff and paths on — the same night was won by 78%
    /// of well-stocked dojos, i.e. the ending had become a formality. The powers are therefore back
    /// where the design put them, at and above the difficulty curve's own ceiling (2.2), and the
    /// calibration is carried by <see cref="FinalRoundEnemies"/> instead: past a point, power adds only
    /// health and strength — the enemy's accuracy, defence and evasion are capped at 95 — and a
    /// developed party answers that with its own kit. Between 2.2-3.2 and 2.6-3.6 the night moved by
    /// three points (78% → 75%); between two men in a bout and four it moves by forty.
    /// </para>
    /// </remarks>
    public IReadOnlyList<double> FinalRoundPowers { get; init; } = [2.2, 2.4, 2.6, 2.8, 3.2];

    /// <summary>How many men each bout puts on the field.</summary>
    /// <remarks>
    /// <para>
    /// Four bouts of seniors and then the man alone. The night tests <b>roster depth</b>, not a
    /// champion: nothing heals in between, so a school that spent 180 days perfecting four warriors
    /// has nobody left to send out for the fourth bout.
    /// </para>
    /// <para>
    /// <b>This is the knob the night is calibrated on</b>, and what it says is that the night asks for
    /// two things at once (200 dojos × 180 days per row, powers 2.2-3.2):
    /// </para>
    /// <list type="table">
    /// <item><description>a dojo that built its school, filled its posts and chose its paths, carrying
    /// <b>eight</b> men into the night: wins it <b>38.5%</b> of the time</description></item>
    /// <item><description>the same developed dojo with <b>six</b> men: <b>13.5%</b></description></item>
    /// <item><description>a dojo that built none of it, with six or eight men: <b>0%</b> — it never gets
    /// past the fourth bout</description></item>
    /// </list>
    /// <para>
    /// So bodies alone do not buy the night and neither does a school with nobody left to send: the
    /// fourth bout, four of his seniors at once, is the gate (29% survival at six men, 51% at eight),
    /// and the fifth is the man himself, alone, as the story fixes it. Raising the crowd one further
    /// step (3/4/4/5) closes the gate outright — 0.5% — which is a wall rather than an ending.
    /// </para>
    /// </remarks>
    public IReadOnlyList<int> FinalRoundEnemies { get; init; } = [2, 3, 3, 4, 1];

    /// <summary>
    /// The share of his health a night's wound costs a warrior, per infirmary day he is carrying.
    /// </summary>
    /// <remarks>
    /// <para>
    /// "No healing between the bouts" had to be given a body. The dojo measures a wound in <b>days</b>,
    /// and the first version of the night simply refused anybody carrying one — which read as
    /// reasonable and was, measured, the rule that made the night unplayable: a season's roster arrives
    /// with almost every man carrying a day or two, so 75% of the dojos that reached the night could put
    /// **nobody** on the field, and lowering the bouts' power to below day 1's did not move it.
    /// </para>
    /// <para>
    /// So a wound is carried as <b>health</b> instead: a man who can stand, stands, with what is left of
    /// him. That is both the fiction (nobody withdraws from a tournament for a cut) and the depth test
    /// the design asked for — every bout takes something off the men who fought it, and the fifth is
    /// answered by whoever is left.
    /// </para>
    /// <para>
    /// <b>Locked at 0.05</b>: swept 0-0.20, the night's win rate moves 8.0% → 6.5% → 5.0% → 4.0% and
    /// the men sent across it 7.2 → 6.1. It is a gentle, monotone knob — it prices the wound without
    /// deciding the night, which is what it is for.
    /// </para>
    /// </remarks>
    public double NightWoundHealthPerDay { get; init; } = 0.05;

    /// <summary>The most health a night's accumulated wounds can take off a warrior.</summary>
    /// <remarks>
    /// <para>
    /// Without a floor a man carrying a fortnight's wounds would walk on with nothing and the night
    /// would decide itself in the first bout.
    /// </para>
    /// <para>
    /// <b>It is a guard, not a knob, and under the locked numbers it never fires</b>: swept 0.2 against
    /// 0.7 the night did not move by a single figure, because <see cref="NightMaxWoundDays"/> (10) times
    /// <see cref="NightWoundHealthPerDay"/> (0.05) already stops at half health. It stays for the tunings
    /// where that is not true.
    /// </para>
    /// </remarks>
    public double NightWoundHealthFloor { get; init; } = 0.4;

    /// <summary>The infirmary days past which a warrior cannot answer the bell at all.</summary>
    /// <remarks>
    /// <para>
    /// This is what a lost limb or a mortal wound comes to: below it a man fights hurt, above it he
    /// cannot be carried to the field. It is the depth test's real edge — a roster runs out of men here,
    /// not in the graveyard.
    /// </para>
    /// <para>
    /// <b>Locked at 10</b>: swept 0, 5, 10, 20 the night's win rate goes 0% → 5.0% → 6.5% → 6.5%. Zero
    /// is the rule this replaced (nobody carrying a wound may fight) and it is the reason the night was
    /// unplayable; past 10 nothing changes, because only a mortal wound carries more days than that.
    /// </para>
    /// </remarks>
    public int NightMaxWoundDays { get; init; } = 10;

    /// <summary>The bouts of the last night.</summary>
    public int FinalRounds => Math.Min(FinalRoundPowers.Count, FinalRoundEnemies.Count);
}

/// <summary>Where the run stands.</summary>
public enum SeasonPhase
{
    /// <summary>The season is being played.</summary>
    Running,

    /// <summary>Day <see cref="SeasonTuning.Days"/> closed with the gate open: the last night is pending.</summary>
    FinalNight,

    /// <summary>All five bouts were won — the licensing authority is the dojo's.</summary>
    Triumph,

    /// <summary>A bout of the last night was lost. The run is over.</summary>
    Fallen,

    /// <summary>
    /// The dojo closed: the roster ran out, or the season ended without the three heads.
    /// </summary>
    Closed,
}

/// <summary>What closing a day did to the season.</summary>
/// <param name="MissedWeek">A compulsory-fight tick landed on a week with no fight filed.</param>
/// <param name="HonorPenalty">
/// What that week cost every living warrior in honour — <c>0</c> for the free first week of a streak.
/// </param>
/// <param name="Phase">The phase the season stands in after the day closed.</param>
public readonly record struct SeasonDayClose(bool MissedWeek, double HonorPenalty, SeasonPhase Phase);

/// <summary>
/// The season's clock and its books — the 180-day countdown, the weekly tick, the head gate and the
/// last night's score (docs/GDD.md §10).
/// </summary>
/// <remarks>
/// <para>
/// It holds <b>no</b> engine and <b>no</b> randomness: everything here is bookkeeping over the day
/// number and what the dojo filed. The fight itself is set up by <see cref="FinalNight"/> and the
/// honour a missed week costs is written by <see cref="Dojo.DojoState"/> — this class only says that
/// the week was missed. Keeping the writing out of it is what lets the whole season be replayed in
/// the batch simulator with no dojo at all.
/// </para>
/// </remarks>
public sealed class Season(SeasonTuning? tuning = null)
{
    public SeasonTuning Tuning { get; } = tuning ?? new SeasonTuning();

    /// <summary>Where the run stands.</summary>
    public SeasonPhase Phase { get; private set; } = SeasonPhase.Running;

    /// <summary>The day the dojo last filed a fight; <c>0</c> if it never has.</summary>
    public int LastFightDay { get; private set; }

    /// <summary>The weekly ticks that landed on a week with no fight filed.</summary>
    public int MissedWeeks { get; private set; }

    /// <summary>The days of the current week on which somebody was fit to take the field.</summary>
    public int FitDaysThisWeek { get; private set; }

    /// <summary>How many of those weeks were missed in a row, counting the current one.</summary>
    /// <remarks>
    /// This is what the penalty is actually charged on: a scattered bad week is the dojo's luck, a run
    /// of them is a dojo that has stopped taking the field.
    /// </remarks>
    public int MissedStreak { get; private set; }

    /// <summary>The heads brought in — the gate counts these.</summary>
    public int HeadsTaken { get; private set; }

    /// <summary>Every fight the season filed, the last night's bouts included.</summary>
    public int Battles { get; private set; }

    /// <summary>The fights that were won.</summary>
    public int Victories { get; private set; }

    /// <summary>The warriors the season buried.</summary>
    public int Dead { get; private set; }

    /// <summary>The bout of the last night that comes next — 1 before the night starts.</summary>
    public int FinalRound { get; private set; } = 1;

    /// <summary>Are the three heads in?</summary>
    public bool GateOpen => HeadsTaken >= Tuning.BountyGate;

    /// <summary>Is the run still being played — is any further day or bout possible?</summary>
    public bool IsOver => Phase is SeasonPhase.Triumph or SeasonPhase.Fallen or SeasonPhase.Closed;

    /// <summary>The days left, today included.</summary>
    public int DaysLeft(int day) => Math.Max(0, Tuning.Days - day + 1);

    /// <summary>
    /// The days to the next compulsory-fight tick, today included.
    /// </summary>
    /// <remarks>
    /// This is the counter that stands on screen as <c>Next move: n days</c>: the week's fight and the
    /// rival's move sit on one clock, so a single number answers both.
    /// </remarks>
    public int DaysToTick(int day)
    {
        int period = Math.Max(1, Tuning.CompulsoryFightDays);
        int into = day % period;
        return into == 0 ? period : period - into;
    }

    /// <summary>Has a fight been filed inside the week that ends on this day?</summary>
    /// <remarks>
    /// A dojo that has never filed one is <b>not</b> filed for the week: without the first test, day 0
    /// would fall inside the opening week's window and the screen would tell a brand-new dojo its
    /// compulsory fight was already done.
    /// </remarks>
    public bool FiledThisWeek(int day) =>
        LastFightDay > 0 && LastFightDay > day - Math.Max(1, Tuning.CompulsoryFightDays);

    /// <summary>The dojo filed a fight today.</summary>
    public void RecordFight(int day, bool victory, int dead = 0)
    {
        LastFightDay = Math.Max(LastFightDay, day);
        Battles++;
        if (victory)
        {
            Victories++;
        }

        Dead += Math.Max(0, dead);
    }

    /// <summary>
    /// Records that today the dojo had — or did not have — somebody fit to send.
    /// </summary>
    /// <remarks>
    /// It is called every day, before the day is closed. Without it the weekly tick could not tell a
    /// dojo that is hiding from one whose roster is in the infirmary, and the penalty meant to close the
    /// training exploit would land hardest on the dojo that is already losing.
    /// </remarks>
    public void RecordStanding(bool anyoneFit)
    {
        if (anyoneFit)
        {
            FitDaysThisWeek++;
        }
    }

    /// <summary>A head was brought in — one more step through the gate.</summary>
    public void RecordHead() => HeadsTaken++;

    /// <summary>
    /// Closes the day: weighs the weekly tick and, on the last day, decides how the season ends.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tick is weighed on the day it lands, not on the day after: the week that closes with no
    /// fight filed is the week that is paid for. Otherwise the penalty would arrive a day late and the
    /// last week of the season would never be paid for at all.
    /// </para>
    /// <para>
    /// A dojo whose roster is gone closes <b>whatever day it is</b>: with nobody to send there is no
    /// way back, and letting the days run on would be a countdown the player cannot act on.
    /// </para>
    /// </remarks>
    /// <param name="day">The day that is closing.</param>
    /// <param name="rosterStanding">Is there still a living warrior on the roster?</param>
    public SeasonDayClose Close(int day, bool rosterStanding)
    {
        if (Phase != SeasonPhase.Running)
        {
            return new SeasonDayClose(false, 0, Phase);
        }

        int period = Math.Max(1, Tuning.CompulsoryFightDays);
        bool tick = day % period == 0;
        bool missed = tick && !FiledThisWeek(day);
        double penalty = 0;

        if (missed)
        {
            MissedWeeks++;

            // A week the dojo could not have fought is not a week it hid: the streak does not even grow.
            if (FitDaysThisWeek >= Math.Max(0, Tuning.FitDaysBeforeCharged))
            {
                MissedStreak++;
                if (MissedStreak > Math.Max(0, Tuning.GraceWeeks))
                {
                    penalty = Tuning.MissedWeekHonorPenalty;
                }
            }
        }
        else if (tick)
        {
            MissedStreak = 0;
        }

        if (tick)
        {
            FitDaysThisWeek = 0;
        }

        if (!rosterStanding)
        {
            Phase = SeasonPhase.Closed;
        }
        else if (day >= Tuning.Days)
        {
            // The gate is read on the last day and nowhere else: three heads brought in on day 12 are
            // worth exactly as much as three brought in on day 179 — what the gate asks is whether the
            // season did the work, not when.
            Phase = GateOpen ? SeasonPhase.FinalNight : SeasonPhase.Closed;
        }

        return new SeasonDayClose(missed, penalty, Phase);
    }

    /// <summary>Writes a bout of the last night; a lost bout ends the run.</summary>
    /// <returns>The phase after the bout.</returns>
    public SeasonPhase RecordFinalRound(bool victory, int dead = 0)
    {
        if (Phase != SeasonPhase.FinalNight)
        {
            return Phase;
        }

        Battles++;
        Dead += Math.Max(0, dead);

        if (!victory)
        {
            Phase = SeasonPhase.Fallen;
            return Phase;
        }

        Victories++;
        FinalRound++;
        if (FinalRound > Tuning.FinalRounds)
        {
            Phase = SeasonPhase.Triumph;
        }

        return Phase;
    }

    /// <summary>Restores the season coming from the save.</summary>
    internal void Restore(
        SeasonPhase phase,
        int lastFightDay,
        int missedWeeks,
        int headsTaken,
        int battles,
        int victories,
        int dead,
        int finalRound,
        int missedStreak = 0)
    {
        Phase = phase;
        LastFightDay = Math.Max(0, lastFightDay);
        MissedWeeks = Math.Max(0, missedWeeks);
        MissedStreak = Math.Max(0, missedStreak);
        HeadsTaken = Math.Max(0, headsTaken);
        Battles = Math.Max(0, battles);
        Victories = Math.Max(0, victories);
        Dead = Math.Max(0, dead);
        FinalRound = Math.Clamp(finalRound, 1, Math.Max(1, Tuning.FinalRounds + 1));
    }
}

/// <summary>The closing screen's figures (docs/GDD.md §10).</summary>
/// <param name="Phase">How the run ended.</param>
/// <param name="Days">The days played.</param>
/// <param name="Battles">The fights filed.</param>
/// <param name="Victories">The fights won.</param>
/// <param name="Heads">The heads brought in.</param>
/// <param name="MissedWeeks">The weeks that closed with no fight filed.</param>
/// <param name="Dead">The warriors buried.</param>
/// <param name="Freed">
/// The men who walked out free — those released during the season plus everyone still standing at the
/// end, whose term the season outlived. This is the figure the ending is actually about: the dojo is a
/// sentence being served, so the men who leave it are the score that is not gold.
/// </param>
public sealed record SeasonSummary(
    SeasonPhase Phase,
    int Days,
    int Battles,
    int Victories,
    int Heads,
    int MissedWeeks,
    IReadOnlyList<string> Dead,
    IReadOnlyList<string> Freed);
