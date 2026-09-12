using Domina.Core.Rng;

namespace Domina.Core.Dojo;

/// <summary>Who a settlement pays (docs/GDD.md §10, Open Decision #17).</summary>
public enum Allegiance
{
    /// <summary>The rival's. He takes its fee and manufactures the trouble he sells protection from.</summary>
    His,

    /// <summary>Nobody's yet — it pays neither school.</summary>
    None,

    /// <summary>The player's. It pays him nothing; it speaks for him.</summary>
    Yours,
}

/// <summary>The one thing a settlement gives on the day it comes over.</summary>
/// <remarks>
/// A settlement drained by years of protection money has one thing left, and it gives it once. A
/// weekly trickle was worked out on paper and dropped (GDD §10): stores are gold in another form, and
/// a standing income would void the measured economy through the back door.
/// </remarks>
public enum SettlementGift
{
    /// <summary>The last of the rice.</summary>
    Rice,

    /// <summary>A bundle of medicine.</summary>
    Medicine,

    /// <summary>What was kept for a festival nobody held.</summary>
    Sake,

    /// <summary>The name of the rival's next target — his move is visible for one turn.</summary>
    Word,
}

/// <summary>One of the province's twelve settlements.</summary>
/// <remarks>
/// They are <b>equal</b>: per-settlement traits (a smith, a ferry, a gambling house) were rejected in
/// the same sitting that closed #17. What distinguishes one from another is only where it stands in
/// the tug-of-war.
/// </remarks>
public sealed class Settlement(int index, string name)
{
    /// <summary>Its place in the province; this is what goes into the save.</summary>
    public int Index { get; } = index;

    public string Name { get; } = name;

    /// <summary>Who it pays today.</summary>
    public Allegiance Held { get; internal set; } = Allegiance.None;

    /// <summary>How far the rival has pressed it — it falls to him on the move after the top.</summary>
    public int Warning { get; internal set; }

    /// <summary>The contracts the player has finished for it since it last changed hands.</summary>
    public int Contracts { get; internal set; }

    /// <summary>The day it last changed hands; 0 if it never has.</summary>
    public int ChangedDay { get; internal set; }
}

/// <summary>The province's tunable numbers.</summary>
/// <remarks>
/// None of them are locked: GDD §10 lists them as "numbers to measure before locking", and the real
/// question behind them is whether the map adds any pressure at all on top of the missed-week honour
/// penalty — the rule that already closes the endless-training exploit.
/// </remarks>
public sealed record ProvinceTuning
{
    /// <summary>
    /// Is the map running at all?
    /// </summary>
    /// <remarks>
    /// A rule switch rather than a number, and it exists for one reason: the measurement's question is
    /// whether the map adds any pressure <b>on top of</b> the missed-week honour penalty, and that can
    /// only be answered against a control season with the map switched off entirely.
    /// </remarks>
    public bool Active { get; init; } = true;

    /// <summary>How many settlements the province has.</summary>
    /// <remarks>
    /// Twelve, not sixteen: 180 days ÷ 7 is <b>25 moves</b>, and 25 moves against 12 settlements is
    /// defensible in part — which is the shape the season wants. At 16 most of the map would never be
    /// touched at all.
    /// </remarks>
    public int Settlements { get; init; } = 12;

    /// <summary>The days between the rival's moves — the compulsory fight's own clock.</summary>
    public int MoveEveryDays { get; init; } = 7;

    /// <summary>The warning level a settlement falls from.</summary>
    /// <remarks>
    /// Two, so a settlement needs <b>three</b> uncontested moves to change hands: pressed, pressed
    /// again, taken. At one the map would turn over faster than the player can answer; at three an
    /// answer would be optional.
    /// </remarks>
    public int WarningToFall { get; init; } = 2;

    /// <summary>The contracts that win a settlement that pays nobody.</summary>
    public int ContractsForNeutral { get; init; } = 2;

    /// <summary>The contracts that win one of his — after its warning has been brought to 0.</summary>
    public int ContractsForHis { get; init; } = 3;

    /// <summary>The days an answered move pushes the next one back.</summary>
    public int PushBackDays { get; init; } = 2;

    /// <summary>How far he can go before the appointment year stops him.</summary>
    /// <remarks>
    /// The one number he stores. Killing his men spends it down, and at zero the ladder's fourth step —
    /// a raid on the dojo itself — opens. It is never shown raw (GDD §10): what the player reads is the
    /// province, not a bar.
    /// </remarks>
    public int Deniability { get; init; } = 8;

    /// <summary>What one won fight against his men costs that bound.</summary>
    public int DeniabilityPerVictory { get; init; } = 1;

    /// <summary>The share a held settlement adds to what the day's work pays.</summary>
    /// <remarks>
    /// The only return a settlement gives after its one gift, and it is <b>not</b> its money: with the
    /// settlement out of his hand, the work that used to go to him is offered to the player at his
    /// rates (GDD §10). You take his trade, never their fee.
    /// </remarks>
    public double RewardSharePerSettlement { get; init; } = 0.03;
}

/// <summary>The steps of his ladder.</summary>
public enum ProvinceMoveKind
{
    /// <summary>He pressed a settlement — its warning rose.</summary>
    Pressed,

    /// <summary>He took it.</summary>
    Taken,

    /// <summary>He came to the gate — the bound is spent, or the map is.</summary>
    Raid,
}

/// <summary>What the rival did on his move day.</summary>
/// <param name="Kind">What kind of move it was.</param>
/// <param name="Settlement">The settlement he pressed or took; <c>null</c> for a raid.</param>
/// <param name="Warning">The warning level after the move.</param>
public readonly record struct ProvinceMove(ProvinceMoveKind Kind, int? Settlement, int Warning);

/// <summary>
/// The province: twelve settlements and the one number the rival keeps.
/// </summary>
/// <remarks>
/// <para>
/// It holds state and rolls no die of its own beyond the starting deal: the move itself is
/// deterministic — the player's settlements first, the most pressed among them — so a season replays
/// identically and the map can be simulated tens of thousands of times.
/// </para>
/// <para>
/// His strength is <b>derived</b>, never stored: it is the count of what he holds, and it is what he
/// brings to the last night.
/// </para>
/// </remarks>
public sealed class Province
{
    private readonly List<Settlement> _settlements = [];

    public Province(ProvinceTuning? tuning = null)
    {
        Tuning = tuning ?? new ProvinceTuning();
        for (int i = 0; i < Math.Max(1, Tuning.Settlements); i++)
        {
            _settlements.Add(new Settlement(i, Names[i % Names.Count]));
        }

        NextMoveDay = Math.Max(1, Tuning.MoveEveryDays);
        Deniability = Math.Max(0, Tuning.Deniability);
    }

    /// <summary>The province's names. Twelve equal villages; the name is all that separates them.</summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        "Kawabe", "Higashi", "Shioya", "Takadera", "Nishino", "Okuma",
        "Sugihara", "Mitsuse", "Yanagi", "Kuroiwa", "Hozumi", "Asagiri",
    ];

    public ProvinceTuning Tuning { get; }

    public IReadOnlyList<Settlement> Settlements => _settlements;

    /// <summary>The day his next move falls on.</summary>
    public int NextMoveDay { get; private set; }

    /// <summary>How far he can still go before the appointment year stops him.</summary>
    public int Deniability { get; private set; }

    /// <summary>Is he at the gate — a raid he has announced and not yet made?</summary>
    public bool RaidPending { get; private set; }

    /// <summary>Until which day his next target is known; 0 when it is not.</summary>
    public int TargetKnownUntil { get; private set; }

    /// <summary>The move cycle an answer has already been counted for.</summary>
    public int AnsweredForMoveDay { get; private set; }

    /// <summary>What he holds — his strength, derived and never stored.</summary>
    public int HisHoldings => _settlements.Count(s => s.Held == Allegiance.His);

    /// <summary>What the player holds.</summary>
    public int YourHoldings => _settlements.Count(s => s.Held == Allegiance.Yours);

    /// <summary>The days to his next move, today included.</summary>
    public int DaysToMove(int day) => Math.Max(0, NextMoveDay - day);

    /// <summary>The settlement he will press next, or <c>null</c> if there is nothing left.</summary>
    /// <remarks>
    /// Deterministic, and in this order: the player's settlements first and the most pressed among
    /// them, then whatever pays nobody. He takes back what was taken from him before he takes what is
    /// free — which is what makes holding a settlement a commitment rather than a collection.
    /// </remarks>
    public Settlement? Target =>
        _settlements
            .Where(s => s.Held != Allegiance.His)
            .OrderBy(s => s.Held == Allegiance.Yours ? 0 : 1)
            .ThenByDescending(s => s.Warning)
            .ThenBy(s => s.Index)
            .FirstOrDefault();

    /// <summary>What the day's work pays with these settlements speaking for the dojo.</summary>
    public int Sweeten(int reward) => reward <= 0
        ? reward
        : (int)Math.Round(reward * (1 + (YourHoldings * Math.Max(0, Tuning.RewardSharePerSettlement))));

    /// <summary>Deals the starting map — the only run-to-run variety the design allows.</summary>
    /// <param name="random">The dojo's own seeded source.</param>
    /// <param name="hisShare">The share of the province he already holds.</param>
    public void Deal(IRandomSource random, double hisShare = 0.5)
    {
        ArgumentNullException.ThrowIfNull(random);

        int his = (int)Math.Round(_settlements.Count * Math.Clamp(hisShare, 0, 1));
        List<int> order = [.. Enumerable.Range(0, _settlements.Count)];

        // Shuffled on the dojo's own source: the map is part of the seeded run, so the same seed deals
        // the same province and a measurement can be repeated.
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = random.NextInt(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        foreach (Settlement settlement in _settlements)
        {
            settlement.Held = Allegiance.None;
            settlement.Warning = 0;
            settlement.Contracts = 0;
        }

        for (int i = 0; i < his && i < order.Count; i++)
        {
            _settlements[order[i]].Held = Allegiance.His;
        }
    }

    /// <summary>
    /// Puts one settlement into a given state.
    /// </summary>
    /// <remarks>
    /// The door the save comes back through, and the one a measurement sets a map up with. It is a
    /// <b>write</b> and not a move: it files no contract, presses nobody and touches no clock, so a
    /// restored season starts exactly where it stopped.
    /// </remarks>
    public void Place(int index, Allegiance held, int warning = 0, int contracts = 0, int changedDay = 0)
    {
        Settlement? settlement = _settlements.Find(s => s.Index == index);
        if (settlement is null)
        {
            return;
        }

        settlement.Held = held;
        settlement.Warning = Math.Clamp(warning, 0, Math.Max(1, Tuning.WarningToFall));
        settlement.Contracts = Math.Max(0, contracts);
        settlement.ChangedDay = Math.Max(0, changedDay);
    }

    /// <summary>Runs his move if this is the day for it.</summary>
    /// <returns>What he did, or <c>null</c> on a day that is not his.</returns>
    public ProvinceMove? Advance(int day)
    {
        if (!Tuning.Active || day < NextMoveDay)
        {
            return null;
        }

        NextMoveDay = day + Math.Max(1, Tuning.MoveEveryDays);

        if (RaidPending)
        {
            return new ProvinceMove(ProvinceMoveKind.Raid, null, 0);
        }

        if (Deniability <= 0)
        {
            // The fourth step of the ladder, and the <b>only</b> thing a spent bound opens. It comes out
            // of the same ladder as every other move: losing to him does not summon a raid of its own,
            // it spends the bound (GDD §10).
            RaidPending = true;
            return new ProvinceMove(ProvinceMoveKind.Raid, null, 0);
        }

        if (Target is not Settlement target)
        {
            // He holds the province and there is nothing left to press. The ladder does not stop there:
            // what is left to take is the school, and the bound no longer has anything to protect —
            // nobody files a complaint on behalf of a school the province has already written off
            // (docs/STORY.md). This is what keeps the map from being ignorable by a dojo that never
            // fights: hiding hands him the map, and the map run out is him at the gate.
            RaidPending = true;
            return new ProvinceMove(ProvinceMoveKind.Raid, null, 0);
        }

        if (target.Warning >= Math.Max(1, Tuning.WarningToFall))
        {
            target.Held = Allegiance.His;
            target.Warning = 0;
            target.Contracts = 0;
            target.ChangedDay = day;
            return new ProvinceMove(ProvinceMoveKind.Taken, target.Index, 0);
        }

        target.Warning++;
        return new ProvinceMove(ProvinceMoveKind.Pressed, target.Index, target.Warning);
    }

    /// <summary>
    /// A fight won against his men: the settlement he is pressing eases, and his next move is late.
    /// </summary>
    /// <remarks>
    /// Capped at one answer per move cycle. Without the cap a dojo that fought three times in a week
    /// would push his clock clean out of the season, and the map would stop being a tug-of-war — the
    /// tempo is meant to run both ways, not one.
    /// </remarks>
    /// <returns><c>true</c> if the answer counted.</returns>
    public bool Answer(int day)
    {
        if (AnsweredForMoveDay == NextMoveDay || day < 1)
        {
            return false;
        }

        Deniability = Math.Max(0, Deniability - Math.Max(0, Tuning.DeniabilityPerVictory));

        if (Target is Settlement target && target.Warning > 0)
        {
            target.Warning--;
        }

        // The cycle is stamped <b>after</b> the push, not before: the answer belongs to the move it
        // delayed, and stamping the old day would let the second win of the same week through.
        NextMoveDay += Math.Max(0, Tuning.PushBackDays);
        AnsweredForMoveDay = NextMoveDay;
        return true;
    }

    /// <summary>
    /// A contract finished for a settlement. It comes over once enough of them are.
    /// </summary>
    /// <returns>The gift it gave if it changed hands, otherwise <c>null</c>.</returns>
    public SettlementGift? FileContract(int index, int day)
    {
        Settlement? settlement = _settlements.Find(s => s.Index == index);
        if (settlement is null || settlement.Held == Allegiance.Yours)
        {
            return null;
        }

        // One of his has to be pushed off his books before it can be won at all: the warning is what his
        // grip is, and a village still under pressure signs with nobody.
        if (settlement.Held == Allegiance.His && settlement.Warning > 0)
        {
            settlement.Warning--;
            return null;
        }

        settlement.Contracts++;

        int needed = settlement.Held == Allegiance.His
            ? Math.Max(1, Tuning.ContractsForHis)
            : Math.Max(1, Tuning.ContractsForNeutral);

        if (settlement.Contracts < needed)
        {
            return null;
        }

        settlement.Held = Allegiance.Yours;
        settlement.Warning = 0;
        settlement.Contracts = 0;
        settlement.ChangedDay = day;

        SettlementGift gift = (SettlementGift)(settlement.Index % Enum.GetValues<SettlementGift>().Length);
        if (gift == SettlementGift.Word)
        {
            TargetKnownUntil = day + Math.Max(1, Tuning.MoveEveryDays);
        }

        return gift;
    }

    /// <summary>The raid is over — answered on the field, or paid for out of the store.</summary>
    /// <remarks>
    /// The bound is restored rather than left at zero: he spent it coming to the gate, and the
    /// appointment year still runs. Left spent, every move after it would be another raid and the
    /// ladder would have a top step and nothing else.
    /// </remarks>
    public void RaidSettled()
    {
        RaidPending = false;
        Deniability = Math.Max(1, Tuning.Deniability);
    }

    /// <summary>Restores the province coming from the save.</summary>
    internal void Restore(
        IEnumerable<(int Index, Allegiance Held, int Warning, int Contracts, int ChangedDay)> settlements,
        int nextMoveDay,
        int deniability,
        bool raidPending,
        int targetKnownUntil,
        int answeredForMoveDay)
    {
        foreach ((int index, Allegiance held, int warning, int contracts, int changed) in settlements)
        {
            Place(index, held, warning, contracts, changed);
        }

        NextMoveDay = Math.Max(1, nextMoveDay);
        Deniability = Math.Clamp(deniability, 0, Math.Max(1, Tuning.Deniability));
        RaidPending = raidPending;
        TargetKnownUntil = Math.Max(0, targetKnownUntil);
        AnsweredForMoveDay = Math.Max(0, answeredForMoveDay);
    }
}
