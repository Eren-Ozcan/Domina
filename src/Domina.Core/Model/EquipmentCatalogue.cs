namespace Domina.Core.Model;

/// <summary>
/// Every piece of equipment the game hands out, addressable <b>by its name</b>.
/// </summary>
/// <remarks>
/// <para>
/// The equipment itself lives in factory methods (<see cref="Weapon.Katana"/> and its neighbours),
/// which is right for the game — the caller asks for a katana and gets one. The journal cannot do that:
/// it writes down "the player bought a katana" as the text <c>Katana</c>, and a replay has to turn that
/// text back into the weapon. That is the whole job of this file.
/// </para>
/// <para>
/// The name and not the numbers is what travels, for the same reason the save writes only what the
/// player produced (see <c>DojoSnapshot</c>): a journal recorded before a balance pass must replay
/// against the <b>new</b> numbers, or it would measure the patch that was thrown away.
/// </para>
/// <para>
/// A name this build does not know gives <c>null</c> rather than an exception. An old journal that
/// mentions a weapon since removed is still worth walking as far as that line.
/// </para>
/// </remarks>
public static class EquipmentCatalogue
{
    /// <summary>Every weapon that can be handed to a warrior, by name.</summary>
    public static IReadOnlyList<Weapon> Weapons { get; } =
    [
        Weapon.Katana(),
        Weapon.Nodachi(),
        Weapon.Yari(),
        Weapon.Tetsubo(),
        Weapon.Jitte(),
        Weapon.Sai(),
        Weapon.Tanto(),
        Weapon.PoisonedTanto(),
        Weapon.Fists(),
    ];

    /// <summary>The melee weapons the rack sells — everything a hand can be armed with for gold.</summary>
    /// <remarks>
    /// <para>
    /// It is <see cref="Weapons"/> without the fists: bare hands are what is left when a weapon is
    /// dropped or an arm is gone, not something the dojo buys. A forged blade is not on the rack
    /// either — the forge makes it out of the weapon already in the hand and the rack never sells one.
    /// </para>
    /// <para>
    /// This is the list Open Decision #21 opened: a class hall could train a torite and a dokushi, and
    /// there was no counter anywhere in the game that would put a jitte or a poisoned tantō in their
    /// hands (docs/GDD.md §10).
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Weapon> Rack { get; } =
        [.. Weapons.Where(w => w.Name != Weapon.Fists().Name)];

    /// <summary>The kits a warrior can be taken on in.</summary>
    public static IReadOnlyList<Armor> Armors { get; } =
    [
        Armor.None(),
        Armor.Light(),
        Armor.Medium(),
        Armor.Heavy(),
    ];

    /// <summary>The single pieces the quartermaster sells slot by slot.</summary>
    public static IReadOnlyList<ArmorPiece> Pieces { get; } =
    [
        ArmorPiece.Bare,
        ArmorPiece.Keikogi,
        ArmorPiece.DoMaru,
        ArmorPiece.OYoroiCuirass,
        ArmorPiece.Kote,
        ArmorPiece.HeavyKote,
        ArmorPiece.Suneate,
        ArmorPiece.HeavySuneate,
        ArmorPiece.Kabuto,
    ];

    /// <summary>The weapon of that name, or <c>null</c> if this build has none.</summary>
    /// <remarks>
    /// A forged weapon carries the forge in its name (<see cref="Weapon.Forged"/>), so it is not on the
    /// list; it is recognised by its stem and reforged instead, which is exactly what the player did.
    /// </remarks>
    public static Weapon? FindWeapon(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        Weapon? plain = Find(Weapons, name, w => w.Name);
        if (plain is not null)
        {
            return plain;
        }

        foreach (Weapon candidate in Weapons)
        {
            Weapon forged = Weapon.Forged(candidate);
            if (string.Equals(forged.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return forged;
            }
        }

        return null;
    }

    /// <summary>The kit of that name, or <c>null</c>.</summary>
    public static Armor? FindArmor(string? name) => Find(Armors, name, a => a.Name);

    /// <summary>The single piece of that name, or <c>null</c>.</summary>
    public static ArmorPiece? FindPiece(string? name) => Find(Pieces, name, p => p.Name);

    /// <summary>The thrown implement of that name, or <c>null</c>.</summary>
    public static ThrownWeapon? FindThrown(string? name) =>
        Find(ThrownWeapon.Catalogue, name, t => t.Name);

    private static T? Find<T>(IReadOnlyList<T> items, string? name, Func<T, string> naming)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        foreach (T item in items)
        {
            if (string.Equals(naming(item), name, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }
}
