using Domina.Core.Model;

namespace Domina.Presentation;

/// <summary>How one man differs from the next man on the same bones.</summary>
/// <remarks>
/// <para>
/// The rig is one skeleton and every warrior is drawn on it, so without this a page of six men is six
/// copies of the same figure with different names over them. The look is a small set of dials —
/// stature, build, hair, beard, a band, a scar — that the drawing reads before it hangs anything on a
/// bone. Nothing here is a second skeleton: the part list and the joints are untouched, which is what
/// keeps the animations valid (docs/ROADMAP.md, phase 2).
/// </para>
/// <para>
/// It is <b>derived, not stored</b>: the dials come from the man's <b>name</b>, so the same man looks
/// the same in the arena, on his own page, at the gate and across a save and a reload, with nothing
/// written to the save file.
/// </para>
/// <para>
/// The name and not the id, deliberately. A candidate at the market has no id until he is bought (a
/// <see cref="Domina.Core.Dojo.RecruitOffer"/> carries only a name), and the man the player weighed at
/// the stall must be the man who walks into the yard — keying on the id would swap his face at the
/// moment of the purchase, which is the one moment the player is looking. A name belongs to only one
/// <b>living</b> warrior at a time (docs/GDD.md §6), so the dojo cannot hold two men with one face;
/// what it costs is that renaming a man reprints him, which is the player's own doing.
/// </para>
/// <para>
/// It carries no colour. The team tint is the arena's reading aid and a per-man colour would fight it,
/// so what varies here is a lightness nudge and an <see cref="Accent"/> index the drawing side maps
/// onto its own palette.
/// </para>
/// </remarks>
/// <param name="Height">Multiplies every bone's length — the man's stature.</param>
/// <param name="Girth">Multiplies every limb's width — how heavily he is built.</param>
/// <param name="HeadSize">Multiplies the head's radius.</param>
/// <param name="Shade">A lightness nudge on the team tint, -0.12 to 0.12.</param>
/// <param name="Hair">What is on his head.</param>
/// <param name="Beard">What is on his jaw.</param>
/// <param name="Headband">Does he wear a hachimaki?</param>
/// <param name="Scar">Does an old cut cross his face?</param>
/// <param name="Accent">The index of his sash colour in the drawing side's palette.</param>
public readonly record struct WarriorLook(
    float Height,
    float Girth,
    float HeadSize,
    float Shade,
    HairStyle Hair,
    BeardStyle Beard,
    bool Headband,
    bool Scar,
    int Accent)
{
    /// <summary>How many sash colours the drawing side must provide.</summary>
    public const int AccentCount = 6;

    /// <summary>The look of a man the screen knows only by name — a candidate, an arrival at the gate.</summary>
    public static WarriorLook Of(string? name) => Of(default, name);

    /// <summary>The look of this man. The same name always gives the same answer.</summary>
    /// <param name="id">Used only when he has no name yet; the name is what the look is keyed on.</param>
    /// <param name="name">His name.</param>
    public static WarriorLook Of(WarriorId id, string? name)
    {
        uint seed = Hash(id, name);

        return new WarriorLook(
            Height: 0.94f + (Pick(ref seed, 13) * 0.01f),
            Girth: 0.86f + (Pick(ref seed, 18) * 0.02f),
            HeadSize: 0.92f + (Pick(ref seed, 9) * 0.02f),
            Shade: -0.12f + (Pick(ref seed, 13) * 0.02f),
            Hair: (HairStyle)Pick(ref seed, 5),
            Beard: (BeardStyle)Pick(ref seed, 4),
            Headband: Pick(ref seed, 3) == 0,
            Scar: Pick(ref seed, 5) == 0,
            Accent: Pick(ref seed, AccentCount));
    }

    /// <summary>The look of a man who is not in the roster — an enemy the core spawned.</summary>
    public static WarriorLook Of(Warrior warrior)
    {
        ArgumentNullException.ThrowIfNull(warrior);

        return Of(warrior.Id, warrior.Name);
    }

    /// <summary>
    /// FNV-1a over the name, or over the id when there is no name.
    /// </summary>
    /// <remarks>
    /// <see cref="string.GetHashCode()"/> is seeded per process, so a man built from it would change
    /// his face on every launch. This one is written out for that reason and must not be replaced by
    /// the runtime's hash.
    /// </remarks>
    private static uint Hash(WarriorId id, string? name)
    {
        const uint prime = 16777619;
        uint hash = 2166136261;

        unchecked
        {
            if (string.IsNullOrEmpty(name))
            {
                for (int shift = 0; shift < 32; shift += 8)
                {
                    hash = (hash ^ (byte)(id.Value >> shift)) * prime;
                }

                return hash;
            }

            foreach (char c in name)
            {
                hash = (hash ^ (byte)c) * prime;
                hash = (hash ^ (byte)(c >> 8)) * prime;
            }
        }

        return hash;
    }

    /// <summary>Takes one dial's worth of the hash and moves it on, so the dials do not agree.</summary>
    private static int Pick(ref uint seed, int range)
    {
        unchecked
        {
            seed = (seed * 1664525) + 1013904223;
        }

        return (int)((seed >> 16) % (uint)range);
    }
}

/// <summary>What a man keeps on his head. The bald head is the rig's own, with nothing added.</summary>
public enum HairStyle
{
    /// <summary>Shaved — the head as the rig builds it.</summary>
    Shaved,

    /// <summary>The chonmage: a shaved pate with the tail laid back over it.</summary>
    Topknot,

    /// <summary>A bun tied high at the back.</summary>
    Bun,

    /// <summary>Loose hair falling to the shoulders.</summary>
    Loose,

    /// <summary>Untied and wild — the ronin's head.</summary>
    Wild,
}

/// <summary>What a man keeps on his jaw.</summary>
public enum BeardStyle
{
    /// <summary>Clean-shaven.</summary>
    None,

    /// <summary>A moustache only.</summary>
    Moustache,

    /// <summary>A short beard on the chin.</summary>
    Stubble,

    /// <summary>A full beard.</summary>
    Full,
}
