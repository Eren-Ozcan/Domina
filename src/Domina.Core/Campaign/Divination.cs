using Domina.Core.Model;

namespace Domina.Core.Campaign;

/// <summary>How much of the day's offer can be read before going in.</summary>
/// <remarks>
/// The diviner's whole output is <b>information</b> (docs/GDD.md §10) — the decision round took the
/// curses of the reference game out and left this in their place. It is deliberately the one staff
/// effect that changes no number in the fight: what it changes is whether the player says yes.
/// </remarks>
public enum ReadingDepth
{
    /// <summary>No hut: the threat band and the sighting line, as it has always been.</summary>
    None,

    /// <summary>The hut stands and nobody reads in it: who is out there and what he carries.</summary>
    Partial,

    /// <summary>A diviner in the hut: the same, with the numbers behind it.</summary>
    Full,
}

/// <summary>One enemy as the diviner reads him.</summary>
/// <param name="Name">The kind's name.</param>
/// <param name="Weapon">The weapon in his hand.</param>
/// <param name="Stats">His stats — <c>null</c> at <see cref="ReadingDepth.Partial"/>.</param>
public sealed record EnemyReading(string Name, string Weapon, WarriorStats? Stats);

/// <summary>What the hut says about the day's offer.</summary>
/// <param name="Depth">How deep the reading goes.</param>
/// <param name="Enemies">The enemies, in the order they will take the field.</param>
public sealed record OfferReading(ReadingDepth Depth, IReadOnlyList<EnemyReading> Enemies)
{
    /// <summary>Nothing was read.</summary>
    public static OfferReading Blind { get; } = new(ReadingDepth.None, []);

    /// <summary>Is there anything to show?</summary>
    public bool Any => Depth != ReadingDepth.None && Enemies.Count > 0;
}

/// <summary>Reads an offer. Pure, and it rolls no die.</summary>
/// <remarks>
/// There is no chance of a <b>wrong</b> reading. A diviner who lies now and then would be measured as
/// a worse diviner and priced accordingly, but it would also make the one thing the post sells —
/// trust in what the screen says — untrustworthy, and the player would go back to ignoring the panel.
/// The design's own words for the post are "no curses, but information".
/// </remarks>
public static class Divination
{
    /// <summary>What the dojo can see of this offer at this depth.</summary>
    public static OfferReading Read(EncounterOffer? offer, ReadingDepth depth)
    {
        if (offer is null || depth == ReadingDepth.None)
        {
            return OfferReading.Blind;
        }

        List<EnemyReading> lines = [];
        foreach (Warrior enemy in offer.Enemies)
        {
            lines.Add(new EnemyReading(
                enemy.Name,
                enemy.Weapon.Name,
                depth == ReadingDepth.Full ? enemy.EffectiveStats : null));
        }

        return new OfferReading(depth, lines);
    }
}
