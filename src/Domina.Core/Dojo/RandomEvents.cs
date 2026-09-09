using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Dojo;

/// <summary>The mishaps that can befall a day.</summary>
/// <remarks>
/// <para>
/// GDD §11's last item: random events can subtract resources, which creates <b>buffer-keeping</b>
/// pressure. For the pressure to work, the events have to hit <b>the treasury</b> and <b>the
/// calendar</b> rather than the store: because the daily shopping fills the store to exactly what is
/// needed (see <see cref="Quartermaster.Restock"/>), three stolen measures of rice already amount to
/// nothing. So the events either take gold, or grow that day's need, or take a warrior's day.
/// </para>
/// <para>
/// They all <b>subtract</b>. There are no donations, treasures or good news — GDD §11 describes events
/// as buffer pressure; a two-way event table would remove the pressure.
/// </para>
/// </remarks>
public enum DayEventKind
{
    /// <summary>Kasadan para gitti.</summary>
    Theft,

    /// <summary>The provisions spoiled: that day's food got more expensive.</summary>
    Spoilage,

    /// <summary>The well got muddy: that day's water got more expensive.</summary>
    FoulWell,

    /// <summary>The medicine did not work: that day the infirmary went without it.</summary>
    SpoiledMedicine,

    /// <summary>A warrior fell ill: he landed in the infirmary without fighting.</summary>
    Illness,
}

/// <summary>What happened that day.</summary>
/// <param name="Kind">The kind of event.</param>
/// <param name="Description">The sentence shown in the log.</param>
/// <param name="Gold">The gold that left the treasury.</param>
/// <param name="Target">The warrior the event touched, if it touched one.</param>
/// <param name="RecoveryDays">The days the illness put him down for.</param>
/// <param name="FoodFactor">The multiplier on that day's food need (1 = normal).</param>
/// <param name="WaterFactor">The multiplier on that day's water need (1 = normal).</param>
public sealed record DayEvent(
    DayEventKind Kind,
    string Description,
    int Gold = 0,
    WarriorId? Target = null,
    int RecoveryDays = 0,
    double FoodFactor = 1,
    double WaterFactor = 1)
{
    /// <summary>Can medicine be used that day?</summary>
    public bool MedicineWorks => Kind != DayEventKind.SpoiledMedicine;
}

/// <summary>The random events' tunable numbers.</summary>
/// <remarks>
/// The numbers are <b>not locked</b>: GDD §11 only says "random events can subtract resources",
/// frequency and severity will be settled by measurement. The measurement's question is clear — the
/// events must strain the buffer but must not close a dojo on their own.
/// </remarks>
public sealed record EventTuning
{
    /// <summary>The probability of an event occurring in a day.</summary>
    public double ChancePerDay { get; init; } = 0.15;

    /// <summary>The <b>largest</b> share a theft can take from the treasury.</summary>
    /// <remarks>
    /// The actual share is drawn between zero and this number each time. A fixed rate would turn the
    /// mishap into a calculable tax: if the player knows the loss up front, keeping a buffer is not a
    /// decision but arithmetic.
    /// </remarks>
    public double MaxTheftShare { get; init; } = 0.12;

    /// <summary>The <b>largest</b> share spoiled provisions can add to that day's bill.</summary>
    /// <remarks>1.0 = at worst the bill doubles.</remarks>
    public double MaxSpoilageShare { get; init; } = 1.0;

    /// <summary>The <b>largest</b> share a muddy well can add to that day's water bill.</summary>
    public double MaxFoulWellShare { get; init; } = 1.0;

    /// <summary>The <b>most</b> days an illness puts a warrior down; the real duration is between 1 and this.</summary>
    public int MaxIllnessDays { get; init; } = 3;

    /// <summary>The draw weights of the event kinds.</summary>
    /// <remarks>
    /// Theft is the heaviest item: it is the only event that hits the buffer directly. The others make
    /// the day or a warrior more expensive — they upset the plan, not the buffer.
    /// </remarks>
    public IReadOnlyList<(DayEventKind Kind, double Weight)> Weights { get; init; } =
    [
        (DayEventKind.Theft, 3),
        (DayEventKind.Spoilage, 2),
        (DayEventKind.FoulWell, 2),
        (DayEventKind.SpoiledMedicine, 1.5),
        (DayEventKind.Illness, 2),
    ];
}

/// <summary>Draws the day's event.</summary>
/// <remarks>
/// <b>Pure</b> like the encounter offer: the same day and the same seed always give the same event. So
/// the event is not written to the save either, and the mishap that befalls you cannot be changed by
/// reloading the save.
/// </remarks>
public sealed class DayEventTable(EventTuning? tuning = null)
{
    public EventTuning Tuning { get; } = tuning ?? new EventTuning();

    /// <summary>That day's event; <c>null</c> if there is none.</summary>
    public DayEvent? Roll(DojoState state, int day)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Roll(state, new SeededRandom(Mix(state.Seed, day)));
    }

    /// <summary>A draw whose stream is supplied from outside — for measurement and tests.</summary>
    public DayEvent? Roll(DojoState state, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(random);

        if (!random.Chance(Tuning.ChancePerDay))
        {
            return null;
        }

        return Build(state, Pick(random), random);
    }

    private DayEvent Build(DojoState state, DayEventKind kind, IRandomSource random) => kind switch
    {
        DayEventKind.Theft => Theft(state, random),
        DayEventKind.Spoilage => Spoilage(random),
        DayEventKind.FoulWell => FoulWell(random),
        DayEventKind.SpoiledMedicine => new DayEvent(kind, "The medicine went mouldy; the infirmary worked empty-handed today."),
        DayEventKind.Illness => Illness(state, random),
        _ => new DayEvent(kind, "An ordinary mishap."),
    };

    private DayEvent Theft(DojoState state, IRandomSource random)
    {
        int purse = Math.Max(0, state.Resources.Gold);
        int taken = Math.Min(purse, (int)Math.Round(purse * random.NextDouble() * Tuning.MaxTheftShare));

        // While there is money in the treasury the thief does not leave empty-handed: even if the rounding falls to zero, one gold goes.
        if (taken == 0 && purse > 0)
        {
            taken = 1;
        }

        return new DayEvent(
            DayEventKind.Theft,
            taken > 0 ? $"{taken} gold was stolen from the treasury." : "A thief got in but the treasury was empty.",
            Gold: taken);
    }

    private DayEvent Spoilage(IRandomSource random)
    {
        double factor = 1 + (random.NextDouble() * Tuning.MaxSpoilageShare);
        return new DayEvent(
            DayEventKind.Spoilage,
            "Part of the provisions in the store spoiled.",
            FoodFactor: factor);
    }

    private DayEvent FoulWell(IRandomSource random)
    {
        double factor = 1 + (random.NextDouble() * Tuning.MaxFoulWellShare);
        return new DayEvent(
            DayEventKind.FoulWell,
            "The well went muddy, water had to be carried from afar.",
            WaterFactor: factor);
    }

    /// <summary>
    /// An illness touches a <b>healthy</b> warrior.
    /// </summary>
    /// <remarks>
    /// Making someone already lying in the infirmary ill would be an invisible event: the infirmary-day
    /// counter does not overwrite a longer one (see <see cref="RosterEntry.Injure"/>), so the event would
    /// mostly do nothing.
    /// </remarks>
    private DayEvent Illness(DojoState state, IRandomSource random)
    {
        RosterEntry? victim = PickWarrior(state, random, e => e.IsFitForCampaign);
        if (victim is null)
        {
            return new DayEvent(DayEventKind.Illness, "Illness went round the dojo but put nobody in bed.");
        }

        int days = 1 + random.NextInt(Math.Max(1, Tuning.MaxIllnessDays));
        return new DayEvent(
            DayEventKind.Illness,
            $"{victim.Name} fell ill; {days} days in the infirmary.",
            Target: victim.Id,
            RecoveryDays: days);
    }

    private static RosterEntry? PickWarrior(
        DojoState state,
        IRandomSource random,
        Func<RosterEntry, bool> fits)
    {
        List<RosterEntry> pool = [.. state.Roster.Living.Where(fits)];
        return pool.Count == 0 ? null : pool[random.NextInt(pool.Count)];
    }

    private DayEventKind Pick(IRandomSource random)
    {
        double total = Tuning.Weights.Sum(w => w.Weight);
        if (total <= 0)
        {
            return DayEventKind.Theft;
        }

        double roll = random.NextDouble() * total;
        foreach ((DayEventKind kind, double weight) in Tuning.Weights)
        {
            roll -= weight;
            if (roll <= 0)
            {
                return kind;
            }
        }

        return Tuning.Weights[^1].Kind;
    }

    /// <summary>
    /// Mixes the seed with the day — with a <b>different</b> salt from the offer stream.
    /// </summary>
    /// <remarks>
    /// With the same salt the event and the offer would be locked to each other: on the day a heavy offer
    /// arrived there would always be a theft too, and two systems would turn into one.
    /// </remarks>
    private static ulong Mix(ulong seed, int day)
    {
        ulong x = seed ^ ((ulong)day * 0xD1B54A32D192ED03) ^ 0xA5A5A5A5A5A5A5A5;
        x ^= x >> 31;
        x *= 0x9FB21C651E98DF25;
        x ^= x >> 29;
        return x;
    }
}
