using Domina.Core.Campaign;
using Domina.Core.Dojo;
using Domina.Core.Model;

namespace Domina.Presentation;

/// <summary>The season's clock as the screen reads it (docs/GDD.md §10).</summary>
/// <param name="Day">Today.</param>
/// <param name="Days">The season's length.</param>
/// <param name="DaysLeft">The days left, today included.</param>
/// <param name="DaysToTick">
/// The days to the next weekly tick. GDD §10 hangs two things on this one clock — the compulsory fight
/// and the rival's next move — and both halves are now real.
/// </param>
/// <param name="FiledThisWeek">Has a fight been filed inside the week that is running?</param>
/// <param name="AtRisk">
/// Will the week that is running be paid for if it stays quiet — is there anybody standing to send?
/// </param>
/// <param name="Heads">The heads brought in.</param>
/// <param name="Gate">The heads the last night asks for.</param>
/// <param name="Phase">Where the run stands.</param>
/// <param name="Yours">The settlements that speak for the dojo.</param>
/// <param name="His">The settlements that pay him.</param>
/// <param name="DaysToMove">The days to his next move.</param>
/// <param name="Pressed">
/// The settlement he is pressing, if the dojo has been told which — a village that came over may hand
/// his next target across, and that word is good for one turn (GDD §10).
/// </param>
/// <param name="UnderRaid">Is he at the gate today?</param>
public readonly record struct SeasonBanner(
    int Day,
    int Days,
    int DaysLeft,
    int DaysToTick,
    bool FiledThisWeek,
    bool AtRisk,
    int Heads,
    int Gate,
    SeasonPhase Phase,
    int Yours = 0,
    int His = 0,
    int DaysToMove = 0,
    string? Pressed = null,
    bool UnderRaid = false)
{
    /// <summary>Are the heads in?</summary>
    public bool GateOpen => Heads >= Gate;
}

/// <summary>A bout of the last night as the screen reads it.</summary>
/// <param name="Round">The bout that is next, from 1.</param>
/// <param name="Rounds">How many bouts the night has.</param>
/// <param name="Sighting">What is standing on the other side — kind and count, no stats.</param>
/// <param name="MaxPartySize">The most men that can be sent out for this bout.</param>
/// <param name="Last">Is this the bout that ends with the man himself?</param>
public readonly record struct FinalNightCard(
    int Round,
    int Rounds,
    string Sighting,
    int MaxPartySize,
    bool Last);

/// <summary>A man the night can still call on.</summary>
/// <param name="Id">His identity; the screen's command returns it.</param>
/// <param name="Name">Display name.</param>
/// <param name="CanStand">Can he answer the bell at all?</param>
/// <param name="WoundDays">The infirmary days he is carrying into the bout.</param>
/// <param name="HealthShare">
/// What is left of him — 1 is a whole man. The night heals nobody, so this is the only thing that
/// carries from one bout to the next.
/// </param>
/// <param name="Score">The total stat score, disabilities included.</param>
public readonly record struct NightCandidate(
    WarriorId Id,
    string Name,
    bool CanStand,
    int WoundDays,
    double HealthShare,
    double Score);

/// <summary>The verdict on sending the selected party out for the bout.</summary>
/// <param name="Refusal">The reason for the refusal; <c>null</c> if they can take the field.</param>
/// <param name="Size">The number selected.</param>
public readonly record struct NightVerdict(FinalRefusal? Refusal, int Size)
{
    public bool CanSend => Refusal is null;
}

/// <summary>The closing screen's figures.</summary>
/// <param name="Phase">How the run ended.</param>
/// <param name="Headline">The one line that says what happened.</param>
/// <param name="Days">The days played.</param>
/// <param name="Battles">The fights filed.</param>
/// <param name="Victories">The fights won.</param>
/// <param name="Heads">The heads brought in.</param>
/// <param name="MissedWeeks">The weeks that closed with no fight filed.</param>
/// <param name="SettlementsHeld">The villages still speaking for the dojo when it ended.</param>
/// <param name="SettlementsHis">The villages still paying him.</param>
/// <param name="Sacks">The times he was left standing in the yard.</param>
/// <param name="Dead">The men the season buried.</param>
/// <param name="Freed">The men who walked out free.</param>
public readonly record struct SeasonEndCard(
    SeasonPhase Phase,
    string Headline,
    int Days,
    int Battles,
    int Victories,
    int Heads,
    int MissedWeeks,
    IReadOnlyList<string> Dead,
    IReadOnlyList<string> Freed,
    int SettlementsHeld = 0,
    int SettlementsHis = 0,
    int Sacks = 0);

/// <summary>
/// What the season's screens read: the banner on the day screen, the last night, the closing screen.
/// </summary>
/// <remarks>
/// Like the other models it <b>decides</b> and does not draw, and it never writes a second set of
/// rules: the refusal comes from <see cref="FinalNight.Refuse"/> itself and the wound share from
/// <see cref="FinalNight.WoundedShares"/>, so a dimmed button and a thrown call can never disagree.
/// </remarks>
public static class SeasonModel
{
    /// <summary>The clock as it stands today.</summary>
    public static SeasonBanner Describe(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        Season season = dojo.Season;

        return new SeasonBanner(
            Day: dojo.Day,
            Days: season.Tuning.Days,
            DaysLeft: season.DaysLeft(dojo.Day),
            DaysToTick: season.DaysToTick(dojo.Day),
            FiledThisWeek: season.FiledThisWeek(dojo.Day),

            // The warning is only honest when the dojo could actually take the field: a roster in the
            // infirmary is not charged for the quiet week, so the screen must not threaten it either.
            AtRisk: !season.FiledThisWeek(dojo.Day) && dojo.Roster.FitForCampaign.Any(),
            Heads: season.HeadsTaken,
            Gate: season.Tuning.BountyGate,
            Phase: season.Phase,
            Yours: dojo.Province.YourHoldings,
            His: dojo.Province.HisHoldings,
            DaysToMove: dojo.Province.DaysToMove(dojo.Day),

            // The target is named only while a settlement's word is still good. The rest of the time the
            // player reads the province, never a number — GDD §10 keeps the rival's own counter hidden.
            Pressed: dojo.Day <= dojo.Province.TargetKnownUntil ? dojo.Province.Target?.Name : null,
            UnderRaid: dojo.UnderRaid);
    }

    /// <summary>The line the day screen puts above everything else.</summary>
    public static string Line(SeasonBanner banner)
    {
        string week = banner.FiledThisWeek
            ? "this week's fight is filed"
            : banner.AtRisk
                ? $"no fight filed — {banner.DaysToTick} days"
                : $"nobody fit to send — {banner.DaysToTick} days";

        // What the player reads is the province — how much of it is his, how much is Kurogane's, and
        // when the man moves next. His own bound is never printed: a visible bar would turn the season's
        // one hidden pressure into arithmetic (GDD §10).
        string map = banner.UnderRaid
            ? "Kurogane is at the gate"
            : banner.Pressed is string pressed
                ? $"Province {banner.Yours}/{banner.Yours + banner.His} · he moves on {pressed} in {banner.DaysToMove} days"
                : $"Province {banner.Yours}/{banner.Yours + banner.His} · he moves in {banner.DaysToMove} days";

        return string.Join(
            "  ·  ",
            $"Day {banner.Day} of {banner.Days}",
            $"Week closes in {banner.DaysToTick} days",
            week,
            $"Heads {banner.Heads}/{banner.Gate}",
            map);
    }

    /// <summary>The bout that is next; <c>null</c> if the night is not being played.</summary>
    public static FinalNightCard? DescribeNight(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        if (dojo.Season.Phase != SeasonPhase.FinalNight)
        {
            return null;
        }

        SeasonTuning tuning = dojo.Season.Tuning;
        int round = dojo.Season.FinalRound;
        IReadOnlyList<Warrior> enemies = FinalNight.Opponents(tuning, round);
        bool last = round == tuning.FinalRounds;

        return new FinalNightCard(
            Round: round,
            Rounds: tuning.FinalRounds,
            Sighting: Sighting(enemies),
            MaxPartySize: Core.Campaign.EncounterOffer.MaxPartySize,
            Last: last);
    }

    /// <summary>Who the night can still call on — those who can stand first.</summary>
    /// <remarks>
    /// A man too broken to answer the bell is <b>listed and not selectable</b>, for the same reason the
    /// day screen lists the infirmary: "there is nobody left" and "nobody can stand" are different
    /// endings and the screen has to be able to say which one this is.
    /// </remarks>
    public static IReadOnlyList<NightCandidate> Candidates(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        SeasonTuning tuning = dojo.Season.Tuning;
        Func<RosterEntry, bool> canStand = FinalNight.CanAnswerTheBell(dojo);

        return dojo.Roster.Living
            .Select(entry => new NightCandidate(
                entry.Id,
                entry.Name,
                canStand(entry),
                entry.RecoveryDaysRemaining,
                Share(entry, tuning),
                MarketModel.Score(entry.Warrior.EffectiveStats)))
            .OrderByDescending(c => c.CanStand)
            .ThenByDescending(c => c.HealthShare)
            .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Can the selected party take the field for this bout?</summary>
    public static NightVerdict Judge(DojoState dojo, IReadOnlyList<WarriorId> party)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        ArgumentNullException.ThrowIfNull(party);

        List<RosterEntry> entries = [.. party.Select(dojo.Roster.Find).OfType<RosterEntry>()];
        if (entries.Count != party.Count)
        {
            return new NightVerdict(FinalRefusal.NotInRoster, party.Count);
        }

        return new NightVerdict(FinalNight.Refuse(dojo, entries), party.Count);
    }

    /// <summary>The selected ids as roster entries — the form the night wants.</summary>
    public static IReadOnlyList<RosterEntry> Party(DojoState dojo, IReadOnlyList<WarriorId> party)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        ArgumentNullException.ThrowIfNull(party);

        return [.. party.Select(dojo.Roster.Find).OfType<RosterEntry>()];
    }

    /// <summary>The closing screen.</summary>
    public static SeasonEndCard Close(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        SeasonSummary summary = dojo.Summarise();

        return new SeasonEndCard(
            summary.Phase,
            Headline(summary.Phase, dojo),
            summary.Days,
            summary.Battles,
            summary.Victories,
            summary.Heads,
            summary.MissedWeeks,
            summary.Dead,
            summary.Freed,
            summary.SettlementsHeld,
            summary.SettlementsHis,
            summary.Sacks);
    }

    private static string Headline(SeasonPhase phase, DojoState dojo) => phase switch
    {
        SeasonPhase.Triumph =>
            "Five bouts, one night, and the licensing of the province is yours.",
        SeasonPhase.Fallen =>
            "The night ended before the fifth bout. The post goes to Kurogane.",
        SeasonPhase.FinalNight =>
            "The season is over and the last night is waiting.",
        _ when !dojo.Roster.Living.Any() =>
            "There is nobody left to open the gate. The school closes.",
        _ when !dojo.Season.GateOpen =>
            $"The season ran out with {dojo.Season.HeadsTaken} of "
            + $"{dojo.Season.Tuning.BountyGate} heads. The contest is held without you.",
        _ => "The season is over.",
    };

    /// <summary>Kind and count, no stats — the same rule the day's offer follows.</summary>
    private static string Sighting(IReadOnlyList<Warrior> enemies) =>
        string.Join(
            " and ",
            enemies.GroupBy(e => e.Name).Select(g => g.Count() == 1 ? g.Key : $"{g.Count()} {g.Key}"));

    private static double Share(RosterEntry entry, SeasonTuning tuning) =>
        entry.RecoveryDaysRemaining <= 0
            ? 1
            : Math.Max(
                tuning.NightWoundHealthFloor,
                1 - (entry.RecoveryDaysRemaining * tuning.NightWoundHealthPerDay));
}
