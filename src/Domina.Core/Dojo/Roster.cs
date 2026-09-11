using System.Diagnostics.CodeAnalysis;
using Domina.Core.Model;

namespace Domina.Core.Dojo;

/// <summary>The dojo's roster — all the warriors, the dead included.</summary>
/// <remarks>
/// <para>
/// A dead warrior is <b>not removed</b> from the roster: permadeath is permanent, but the warrior's
/// history (honour, disabilities, his name) stays on record. Whether he is alive
/// is read through <see cref="Model.Warrior.IsAlive"/>.
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

    /// <summary>The warriors the dojo still keeps — the dead and the released are not among them.</summary>
    public IEnumerable<RosterEntry> Living =>
        _entries.Values.Where(e => e.Warrior.IsAlive && !e.Released);

    /// <summary>The men who walked out free while the season ran.</summary>
    public IEnumerable<RosterEntry> Released => _entries.Values.Where(e => e.Released);

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
    /// <exception cref="InvalidOperationException">The id is already on the roster.</exception>
    public RosterEntry Add(Warrior warrior)
    {
        ArgumentNullException.ThrowIfNull(warrior);

        if (_entries.ContainsKey(warrior.Id))
        {
            throw new InvalidOperationException($"{warrior.Id} is already on the roster.");
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
                e => e.Warrior.IsAlive
                     && !e.Released
                     && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

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

    /// <summary>
    /// Ends the warrior's term: he leaves the dojo alive and is counted on the closing screen.
    /// </summary>
    /// <remarks>
    /// Releasing is not the same as dismissing a member of staff — the man is the point of the fiction,
    /// not a running cost. He cannot be called back: a dojo that could release a mouth before a hungry
    /// day and take him back after it would be buying the upkeep rule off.
    /// </remarks>
    /// <returns><c>true</c> if he walked out.</returns>
    public bool Release(WarriorId id)
    {
        RosterEntry entry = Require(id);
        if (!entry.Warrior.IsAlive || entry.Released)
        {
            return false;
        }

        entry.Released = true;
        entry.RecoveryDaysRemaining = 0;
        entry.Activity = DojoActivity.Resting;
        return true;
    }

    private RosterEntry Require(WarriorId id) =>
        _entries.TryGetValue(id, out RosterEntry? entry)
            ? entry
            : throw new KeyNotFoundException($"{id} is not on the roster.");

    private void RequireFreeName([NotNull] string name)
    {
        if (IsNameTaken(name))
        {
            throw new InvalidOperationException($"The name '{name}' is in use by a living warrior.");
        }
    }
}
