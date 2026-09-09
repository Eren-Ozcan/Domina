using System.Diagnostics.CodeAnalysis;
using Domina.Core.Model;

namespace Domina.Core.Dojo;

/// <summary>The dojo's roster — all the warriors, the dead included.</summary>
/// <remarks>
/// <para>
/// A dead warrior is <b>not removed</b> from the roster: permadeath is permanent, but the warrior's
/// history (honour, disabilities, his name) stays on record. Whether he is alive
/// <see cref="Model.Warrior.IsAlive"/> ile okunur.
/// </para>
/// <para>
/// Name uniqueness is enforced only among <b>the living</b> — that is GDD §6's rule: a name belongs to
/// a single living warrior at a time, and when he dies the name returns to the pool. That is how a chat
/// command (<c>!ronin-&lt;name&gt;</c>) resolves to a single target.
/// </para>
/// </remarks>
public sealed class Roster
{
    private readonly Dictionary<WarriorId, RosterEntry> _entries = [];
    private int _nextId;

    /// <summary>All the records on the roster — the dead included, in the order they were added.</summary>
    public IReadOnlyCollection<RosterEntry> Entries => _entries.Values;

    public IEnumerable<RosterEntry> Living => _entries.Values.Where(e => e.Warrior.IsAlive);

    /// <summary>The warriors who can be sent on an expedition today.</summary>
    public IEnumerable<RosterEntry> FitForCampaign => _entries.Values.Where(e => e.IsFitForCampaign);

    public int Count => _entries.Count;

    /// <summary>Hires a new warrior and gives him a unique identity.</summary>
    /// <exception cref="InvalidOperationException">If the name is in use by a living warrior.</exception>
    public RosterEntry Recruit(
        string name,
        WarriorStats? stats = null,
        Weapon? weapon = null,
        Armor? armor = null,
        double talent = 1.0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        RequireFreeName(name);

        Warrior warrior = new(
            new WarriorId(++_nextId),
            name,
            stats ?? WarriorStats.Recruit(),
            weapon,
            armor)
        {
            Talent = talent,
        };

        RosterEntry entry = new(warrior);
        _entries.Add(warrior.Id, entry);
        return entry;
    }

    /// <summary>Puts a warrior with an already-assigned identity onto the roster (when loading a save).</summary>
    /// <exception cref="InvalidOperationException">Kimlik zaten kadroda varsa.</exception>
    public RosterEntry Add(Warrior warrior)
    {
        ArgumentNullException.ThrowIfNull(warrior);

        if (_entries.ContainsKey(warrior.Id))
        {
            throw new InvalidOperationException($"{warrior.Id} kadroda zaten var.");
        }

        RosterEntry entry = new(warrior);
        _entries.Add(warrior.Id, entry);
        _nextId = Math.Max(_nextId, warrior.Id.Value);
        return entry;
    }

    public RosterEntry? Find(WarriorId id) => _entries.GetValueOrDefault(id);

    /// <summary>
    /// Resolves the name chat wrote to a single living warrior. <c>null</c> if it is not found — per
    /// GDD §6 this is a silent outcome, not an error.
    /// </summary>
    public RosterEntry? FindLiving(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : _entries.Values.FirstOrDefault(
                e => e.Warrior.IsAlive && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

    public bool IsNameTaken(string name) => FindLiving(name) is not null;

    /// <summary>Changes the warrior's name. The player can always do this (GDD §8).</summary>
    /// <exception cref="InvalidOperationException">If the new name belongs to another living warrior.</exception>
    public void Rename(WarriorId id, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        RosterEntry entry = Require(id);
        if (string.Equals(entry.Name, newName, StringComparison.OrdinalIgnoreCase))
        {
            entry.Warrior.Name = newName;
            return;
        }

        RequireFreeName(newName);
        entry.Warrior.Name = newName;
    }

    /// <summary>
    /// Kills the warrior permanently — death in a fight, seppuku, all of it goes through here.
    /// The record stays on the roster; his name returns to the pool at that moment.
    /// </summary>
    public bool Kill(WarriorId id)
    {
        RosterEntry entry = Require(id);
        if (!entry.Warrior.IsAlive)
        {
            return false;
        }

        entry.Warrior.Kill();
        entry.RecoveryDaysRemaining = 0;
        entry.Activity = DojoActivity.Resting;
        return true;
    }

    private RosterEntry Require(WarriorId id) =>
        _entries.TryGetValue(id, out RosterEntry? entry)
            ? entry
            : throw new KeyNotFoundException($"{id} kadroda yok.");

    private void RequireFreeName([NotNull] string name)
    {
        if (IsNameTaken(name))
        {
            throw new InvalidOperationException($"The name '{name}' is in use by a living warrior.");
        }
    }
}
