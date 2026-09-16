using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Chat;

/// <summary>A viewer standing in the gateway, asking to be let in.</summary>
/// <param name="Viewer">The name the viewer types with — the account, not the man.</param>
/// <param name="Man">The man the game drew for him, with the viewer's name on him.</param>
public readonly record struct GateArrival(string Viewer, RecruitOffer Man);

/// <summary>
/// The queue of viewers asking to be let into the yard (docs/GDD.md §9; design canvas → 9b).
/// </summary>
/// <remarks>
/// <para>
/// A viewer who asks is given <b>a man</b>, not a badge: the game draws a warrior from the term's own
/// seed and the viewer's name, puts the viewer's name on him, and stands him in the gateway. Whether he
/// is taken in is the player's decision, and if he is taken in he can die in the yard like anybody else.
/// </para>
/// <para>
/// One per viewer per term. The rule lives here rather than in the transport, so that Twitch, Kick and a
/// test all obey it — the transport's only job is to call <see cref="Ask"/> with a name.
/// </para>
/// <para>
/// There is no transport in this build. The gate is the seam it will plug into: the chat layer calls
/// <see cref="Ask"/>, the dojo takes the next arrival at dawn, and nothing else in the game knows where
/// the name came from.
/// </para>
/// </remarks>
public sealed class ViewerGate
{
    /// <summary>The mixer that keeps a viewer's man off the market's own stream.</summary>
    private const ulong GateSalt = 0x5EED_9A7E_0F7A_1D01;

    private readonly Queue<GateArrival> _waiting = new();
    private readonly HashSet<string> _asked = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How many are standing in the gateway.</summary>
    public int Waiting => _waiting.Count;

    /// <summary>Whether this viewer has already had his one man this term.</summary>
    public bool HasAsked(string viewer) =>
        !string.IsNullOrWhiteSpace(viewer) && _asked.Contains(viewer.Trim());

    /// <summary>
    /// A viewer asks to be let in, and a man is drawn for him.
    /// </summary>
    /// <returns><c>false</c> if he has already asked this term, or gave no name.</returns>
    public bool Ask(string viewer, DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        if (string.IsNullOrWhiteSpace(viewer))
        {
            return false;
        }

        string name = viewer.Trim();

        if (!_asked.Add(name))
        {
            return false;
        }

        // The man is a function of the term's seed and the viewer's name, so the same viewer asking in
        // the same term is the same man however often the save is reopened.
        SeededRandom stream = new(dojo.Seed ^ GateSalt ^ (ulong)name.GetHashCode(StringComparison.Ordinal));
        RecruitOffer drawn = dojo.Market
            .Stock(stream, dojo.Market.AnchorFor(dojo.Roster), dojo.Economy.RecruitPrice)[0];

        _waiting.Enqueue(new GateArrival(name, drawn with { Name = name }));
        return true;
    }

    /// <summary>The next man in the gateway, or nothing when nobody is asking.</summary>
    public GateArrival? Next() => _waiting.Count == 0 ? null : _waiting.Peek();

    /// <summary>Takes the next man off the gate — he was let in, or sent away.</summary>
    public GateArrival? Take() => _waiting.Count == 0 ? null : _waiting.Dequeue();
}
