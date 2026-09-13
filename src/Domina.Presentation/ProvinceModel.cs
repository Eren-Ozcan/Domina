using Domina.Core.Dojo;

namespace Domina.Presentation;

/// <summary>One settlement as the board draws it.</summary>
/// <param name="Name">The village's name.</param>
/// <param name="Held">Who it pays.</param>
/// <param name="Warning">How far he has pressed it, 0 to the level it falls from.</param>
/// <param name="WarningLevels">What that warning is out of, so the screen need not know the rule.</param>
/// <param name="Contracts">The contracts the dojo has finished for it since it last changed hands.</param>
/// <param name="ContractsNeeded">How many it takes to win it from where it stands today.</param>
/// <param name="Pressed">Is this the village his next move lands on — and does the dojo know it?</param>
public readonly record struct SettlementTile(
    string Name,
    Allegiance Held,
    int Warning,
    int WarningLevels,
    int Contracts,
    int ContractsNeeded,
    bool Pressed);

/// <summary>The board's own line.</summary>
/// <param name="Yours">The villages that speak for the dojo.</param>
/// <param name="His">The villages that pay him.</param>
/// <param name="Free">The villages that pay neither.</param>
/// <param name="DaysToMove">The days to his next move.</param>
/// <param name="UnderRaid">Is he at the gate today?</param>
public readonly record struct ProvinceBoard(
    IReadOnlyList<SettlementTile> Settlements,
    int Yours,
    int His,
    int Free,
    int DaysToMove,
    bool UnderRaid);

/// <summary>
/// The province as the board reads it (docs/GDD.md §10).
/// </summary>
/// <remarks>
/// <para>
/// The board is a <b>picture, not a screen the player acts on</b> — Open Decision #2's footnote: no
/// travel, no routing, no fight started from it. So the model carries no commands at all; it only
/// says what the season has done.
/// </para>
/// <para>
/// Two things are deliberately <b>not</b> in it: the rival's deniability, which is the season's one
/// hidden pressure and would become arithmetic the moment it were printed, and his next target, which
/// is shown only while a village's word is still good.
/// </para>
/// </remarks>
public static class ProvinceModel
{
    /// <summary>The province as it stands today.</summary>
    public static ProvinceBoard Describe(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);

        Province province = dojo.Province;
        bool known = dojo.Day <= province.TargetKnownUntil;
        int? pressed = known ? province.Target?.Index : null;

        List<SettlementTile> tiles = [];
        foreach (Settlement settlement in province.Settlements)
        {
            tiles.Add(new SettlementTile(
                settlement.Name,
                settlement.Held,
                settlement.Warning,
                province.Tuning.WarningToFall,
                settlement.Contracts,
                Needed(province.Tuning, settlement),
                settlement.Index == pressed));
        }

        return new ProvinceBoard(
            tiles,
            province.YourHoldings,
            province.HisHoldings,
            tiles.Count - province.YourHoldings - province.HisHoldings,
            province.DaysToMove(dojo.Day),
            dojo.UnderRaid);
    }

    /// <summary>What one village's row says.</summary>
    /// <remarks>
    /// The rule is rendered here rather than on the screen, so that what a warning level or a contract
    /// count <b>means</b> is decided in one tested place: a village of his under pressure cannot be
    /// worked on at all until his grip is broken, and the row has to say that rather than show a
    /// progress count the player cannot move.
    /// </remarks>
    public static string Line(SettlementTile tile) => tile.Held switch
    {
        Allegiance.Yours => tile.Warning > 0
            ? $"{tile.Name} — yours, pressed {tile.Warning}/{tile.WarningLevels}"
            : $"{tile.Name} — yours",
        Allegiance.His => tile.Warning > 0
            ? $"{tile.Name} — Kurogane's, his grip holds ({tile.Warning})"
            : $"{tile.Name} — Kurogane's, {tile.Contracts}/{tile.ContractsNeeded} contracts",
        _ => tile.Warning > 0
            ? $"{tile.Name} — pays nobody, pressed {tile.Warning}/{tile.WarningLevels}"
            : $"{tile.Name} — pays nobody, {tile.Contracts}/{tile.ContractsNeeded} contracts",
    };

    private static int Needed(ProvinceTuning tuning, Settlement settlement) => settlement.Held switch
    {
        Allegiance.His => Math.Max(1, tuning.ContractsForHis),
        Allegiance.Yours => 0,
        _ => Math.Max(1, tuning.ContractsForNeutral),
    };
}
