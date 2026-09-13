namespace Domina.Core.Dojo;

/// <summary>The three parties a dojo has a standing with (docs/GDD.md §10).</summary>
/// <remarks>
/// The rival is deliberately <b>not</b> among them: there is no such thing as good relations with the
/// man who killed the master, and his axis is the province's (see <see cref="Province"/>). Nor is the
/// deputy a fourth party — the clerk's office stamps licensed work and the number tracks the file, not
/// the man.
/// </remarks>
public enum Patron
{
    /// <summary>The clerk's office — the lord's administration, and the owner of the offer queue.</summary>
    Clerk,

    /// <summary>The merchant guild — what the market asks and what it puts out.</summary>
    Guild,

    /// <summary>The temple — the omamori supply and the funeral rite.</summary>
    Temple,
}

/// <summary>The five tiers a standing is read in.</summary>
public enum StandingTier
{
    Hostile,
    Cold,
    Neutral,
    Pleased,
    Loyal,
}

/// <summary>What moves a standing, and what a tier is worth.</summary>
/// <remarks>
/// <para>
/// One number per party and five tiers to read it in (GDD §10). The numbers here are <b>not locked</b>
/// — what they have to produce is a relationship that a season can move a tier or two, never four, or
/// the parties would be a checklist to be maxed rather than a choice about whose work to take.
/// </para>
/// <para>
/// Everything a tier buys is small and on a different axis for each party, because the point of three
/// parties is that they cannot all be courted at once with the same days.
/// </para>
/// </remarks>
public sealed record StandingTuning
{
    /// <summary>Where every standing starts — the middle of the scale.</summary>
    public double Starting { get; init; } = 50;

    /// <summary>What finishing a contract for a party is worth to it.</summary>
    public double ContractGain { get; init; } = 8;

    /// <summary>What losing or abandoning one costs.</summary>
    /// <remarks>
    /// Heavier than the gain on purpose: taking a contract is giving your word, and GDD §10 makes the
    /// broken promise cost twice — the roster's honour <b>and</b> the standing.
    /// </remarks>
    public double BrokenLoss { get; init; } = 12;

    /// <summary>What the first gift of the season is worth.</summary>
    public double GiftGain { get; init; } = 6;

    /// <summary>What each gift after it is worth, as a share of the one before.</summary>
    /// <remarks>
    /// Diminishing, or a rich dojo would simply buy Loyal on three parties in a week and the
    /// relationship would be a shop rather than a record of what the season did.
    /// </remarks>
    public double GiftDiminish { get; init; } = 0.55;

    /// <summary>What a gift costs in gold.</summary>
    public int GiftPrice { get; init; } = 60;

    /// <summary>What a week with nothing filed for a party costs it.</summary>
    /// <remarks>
    /// The "never taking their offers" clause of GDD §10, and the reason a standing is a decision: the
    /// days are the same days, so courting one party is declining another's work.
    /// </remarks>
    public double NeglectPerWeek { get; init; } = 2;

    /// <summary>The days after which a party counts as neglected.</summary>
    public int NeglectAfterDays { get; init; } = 7;

    /// <summary>What one tier above neutral adds to what the clerk's work pays.</summary>
    public double ClerkRewardPerTier { get; init; } = 0.06;

    /// <summary>What one tier above neutral takes off the guild's prices.</summary>
    public double GuildPricePerTier { get; init; } = 0.05;

    /// <summary>What one tier above neutral takes off the temple's charms.</summary>
    public double TempleCharmPerTier { get; init; } = 0.10;

    /// <summary>The tier a standing reads as.</summary>
    public static StandingTier TierOf(double value) => value switch
    {
        < 20 => StandingTier.Hostile,
        < 40 => StandingTier.Cold,
        < 60 => StandingTier.Neutral,
        < 80 => StandingTier.Pleased,
        _ => StandingTier.Loyal,
    };

    /// <summary>How far a tier sits from neutral, in tiers (-2 to +2).</summary>
    public static int Steps(StandingTier tier) => (int)tier - (int)StandingTier.Neutral;
}

/// <summary>What the province's three standing parties think of the dojo.</summary>
/// <remarks>
/// It holds state and decides nothing about gold: the day loop and the quartermaster read the tiers,
/// and only the three numbers (with the gifts already given) go into the save — what a tier is
/// <b>worth</b> is a balance number and stays in the code.
/// </remarks>
public sealed class Standing
{
    private readonly Dictionary<Patron, double> _values = [];
    private readonly Dictionary<Patron, int> _gifts = [];
    private readonly Dictionary<Patron, int> _lastFiled = [];

    public Standing(StandingTuning? tuning = null)
    {
        Tuning = tuning ?? new StandingTuning();
        foreach (Patron patron in Enum.GetValues<Patron>())
        {
            _values[patron] = Tuning.Starting;
        }
    }

    public StandingTuning Tuning { get; }

    /// <summary>The raw number, 0-100.</summary>
    public double Of(Patron patron) => _values.GetValueOrDefault(patron, Tuning.Starting);

    /// <summary>The tier it reads as.</summary>
    public StandingTier TierOf(Patron patron) => StandingTuning.TierOf(Of(patron));

    /// <summary>The gifts already given to this party this season.</summary>
    public int GiftsTo(Patron patron) => _gifts.GetValueOrDefault(patron);

    /// <summary>The day the dojo last did work for this party; 0 if it never has.</summary>
    public int LastFiled(Patron patron) => _lastFiled.GetValueOrDefault(patron);

    /// <summary>A contract finished for a party.</summary>
    public void Filed(Patron patron, int day)
    {
        _lastFiled[patron] = day;
        Move(patron, Tuning.ContractGain);
    }

    /// <summary>A contract taken from a party and not kept.</summary>
    public void Broke(Patron patron) => Move(patron, -Tuning.BrokenLoss);

    /// <summary>
    /// A gift, worth less every time.
    /// </summary>
    /// <returns>What the standing actually moved by.</returns>
    public double Gift(Patron patron)
    {
        int already = GiftsTo(patron);
        _gifts[patron] = already + 1;

        double worth = Tuning.GiftGain * Math.Pow(Math.Clamp(Tuning.GiftDiminish, 0, 1), already);
        Move(patron, worth);
        return worth;
    }

    /// <summary>
    /// Charges the week's neglect to every party the dojo has not worked for.
    /// </summary>
    /// <remarks>
    /// It is read off the <b>last day filed</b> rather than counted down, so a reload cannot shake a
    /// week of neglect off — the same rule the season's own tick follows.
    /// </remarks>
    public void CloseWeek(int day)
    {
        foreach (Patron patron in Enum.GetValues<Patron>())
        {
            if (day - LastFiled(patron) >= Math.Max(1, Tuning.NeglectAfterDays))
            {
                Move(patron, -Tuning.NeglectPerWeek);
            }
        }
    }

    /// <summary>Raises a standing by something other than work — the monk's rite.</summary>
    public void Keep(Patron patron, double worth) => Move(patron, worth);

    /// <summary>What the clerk's work pays with this standing.</summary>
    public double ClerkReward =>
        1 + (StandingTuning.Steps(TierOf(Patron.Clerk)) * Tuning.ClerkRewardPerTier);

    /// <summary>What the guild asks with this standing.</summary>
    public double GuildPrice =>
        Math.Max(0.1, 1 - (StandingTuning.Steps(TierOf(Patron.Guild)) * Tuning.GuildPricePerTier));

    /// <summary>What the temple asks for a charm with this standing.</summary>
    public double TempleCharmPrice =>
        Math.Max(0.1, 1 - (StandingTuning.Steps(TierOf(Patron.Temple)) * Tuning.TempleCharmPerTier));

    /// <summary>Restores the three numbers coming from the save.</summary>
    internal void Restore(IEnumerable<(Patron Patron, double Value, int Gifts, int LastFiled)> records)
    {
        foreach ((Patron patron, double value, int gifts, int filed) in records)
        {
            _values[patron] = Math.Clamp(value, 0, 100);
            _gifts[patron] = Math.Max(0, gifts);
            _lastFiled[patron] = Math.Max(0, filed);
        }
    }

    private void Move(Patron patron, double delta) =>
        _values[patron] = Math.Clamp(Of(patron) + delta, 0, 100);
}
