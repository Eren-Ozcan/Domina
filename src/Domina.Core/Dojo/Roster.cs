using System.Diagnostics.CodeAnalysis;
using Domina.Core.Model;

namespace Domina.Core.Dojo;

/// <summary>Dojo'nun kadrosu — ölüler dahil, bütün savaşçılar.</summary>
/// <remarks>
/// <para>
/// Ölen savaşçı kadrodan <b>silinmez</b>: permadeath kalıcıdır ama savaşçının geçmişi
/// (onur, sakatlıklar, adı) kayıtta durur. Canlı olup olmadığı
/// <see cref="Model.Warrior.IsAlive"/> ile okunur.
/// </para>
/// <para>
/// İsim eşsizliği yalnızca <b>canlılar</b> arasında zorlanır — GDD §6'nın kuralı bu:
/// bir isim aynı anda tek bir canlı savaşçınındır, o ölünce isim havuza döner.
/// Chat komutu (<c>!ronin-&lt;isim&gt;</c>) bu sayede tek bir hedefe çözülür.
/// </para>
/// </remarks>
public sealed class Roster
{
    private readonly Dictionary<WarriorId, RosterEntry> _entries = [];
    private int _nextId;

    /// <summary>Kadrodaki bütün kayıtlar — ölüler dahil, eklenme sırasıyla.</summary>
    public IReadOnlyCollection<RosterEntry> Entries => _entries.Values;

    public IEnumerable<RosterEntry> Living => _entries.Values.Where(e => e.Warrior.IsAlive);

    /// <summary>Bugün sefere gönderilebilecek savaşçılar.</summary>
    public IEnumerable<RosterEntry> FitForCampaign => _entries.Values.Where(e => e.IsFitForCampaign);

    public int Count => _entries.Count;

    /// <summary>Yeni savaşçı alır ve ona benzersiz bir kimlik verir.</summary>
    /// <exception cref="InvalidOperationException">İsim canlı bir savaşçıda kullanılıyorsa.</exception>
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

    /// <summary>Kimliği önceden verilmiş bir savaşçıyı kadroya koyar (kayıt yüklerken).</summary>
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
    /// Chat'in yazdığı ismi tek bir canlı savaşçıya çözer. Bulunamazsa <c>null</c> —
    /// GDD §6 gereği bu sessiz bir sonuçtur, hata değil.
    /// </summary>
    public RosterEntry? FindLiving(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : _entries.Values.FirstOrDefault(
                e => e.Warrior.IsAlive && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

    public bool IsNameTaken(string name) => FindLiving(name) is not null;

    /// <summary>Savaşçının adını değiştirir. Oyuncu bunu her zaman yapabilir (GDD §8).</summary>
    /// <exception cref="InvalidOperationException">Yeni ad başka bir canlıdaysa.</exception>
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
    /// Savaşçıyı kalıcı olarak öldürür — dövüşte ölüm, seppuku, hepsi buradan geçer.
    /// Kayıt kadroda kalır; adı o anda havuza döner.
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
            throw new InvalidOperationException($"'{name}' adı canlı bir savaşçıda kullanılıyor.");
        }
    }
}
