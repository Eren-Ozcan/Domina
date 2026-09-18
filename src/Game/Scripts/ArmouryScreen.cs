using Domina.Core.Dojo;
using Domina.Core.Model;
using Domina.Presentation;
using Godot;

namespace Domina.Game;

/// <summary>
/// The quartermaster's counter: armour, repairs, the forge and the throwing slot, one warrior at a time.
/// </summary>
/// <remarks>
/// <para>
/// The core has been able to do all four since the economy landed, and until now nothing on screen
/// called any of it — the only side shopping for steel was the measuring policy. What the counter will
/// sell, and why it refuses, is decided by <see cref="QuartermasterModel"/> (engine-free, tested); the
/// purchase itself goes through <see cref="Quartermaster"/>, so a row that offers to sell and a
/// purchase that is turned away can never disagree.
/// </para>
/// <para>
/// It is its own screen rather than another panel on the roster: a kit is six regions, three or four
/// pieces each, a repair line and a stall, and hung under the roster's detail column it would push
/// everything the roster exists for off the bottom of the window.
/// </para>
/// <para>
/// A piece that needs the smith is <b>listed and refused</b>, never hidden: the plate works is a
/// long-term investment and the player cannot save toward something he cannot see.
/// </para>
/// </remarks>
public sealed partial class ArmouryScreen : DojoScreen
{
    private DojoState _dojo = null!;
    private HFlowContainer _summaryRow = null!;
    private VBoxContainer _list = null!;
    private VBoxContainer _counter = null!;
    private SlotMap _map = null!;
    private Label _notice = null!;
    private WarriorId? _selected;

    /// <summary>Builds the screen and prints the counter.</summary>
    public override void Build(DojoState dojo)
    {
        ArgumentNullException.ThrowIfNull(dojo);
        _dojo = dojo;

        VBoxContainer page = BuildPage(
            "the rack",
            "One man at a time: what he wears on each of his six parts, what his kit costs to mend, "
            + "and what he carries to throw. Pick a man on the left, buy on the right. Nothing is bought back.");

        VBoxContainer purse = UiKit.Section(page, "The counter");
        _summaryRow = UiKit.ChipRow();
        purse.AddChild(_summaryRow);
        purse.AddChild(UiKit.Note("A greyed button is a piece the counter will not sell — hover it to be told why."));

        HBoxContainer split = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        split.AddThemeConstantOverride("separation", 12);
        page.AddChild(split);

        VBoxContainer men = UiKit.Section(split, "The men", fill: true);

        ScrollContainer names = new()
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(280, 0),
        };
        men.AddChild(names);

        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 4);
        names.AddChild(_list);

        // The rows say what is on each part; the figure says where he is bare, which is the question
        // that is actually asked before a man is sent out.
        men.AddChild(UiKit.Rule());
        men.AddChild(UiKit.SectionLabel("Where he is covered"));
        _map = new SlotMap();
        men.AddChild(_map);

        VBoxContainer kit = UiKit.Section(split, "His kit", fill: true);

        ScrollContainer counter = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        kit.AddChild(counter);

        // The cards are held off the scrollbar by a margin of their own: stretched to the scroll's
        // full width they run under it, and the right edge of every card in the kit is clipped.
        _counter = UiKit.Padded(counter, 0, 0);
        MarginContainer counterMargin = _counter.GetParent<MarginContainer>();
        counterMargin.AddThemeConstantOverride("margin_right", 12);
        counterMargin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _counter.AddThemeConstantOverride("separation", 8);
        _counter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        _notice = UiKit.Note();
        page.AddChild(_notice);

        Refresh();
    }

    /// <summary>Reprints the men and the selected man's counter.</summary>
    public override void Refresh()
    {
        Clear(_list);
        Clear(_counter);

        IReadOnlyList<RosterRow> rows = [.. RosterModel.Describe(_dojo).Where(r => r.IsAlive)];

        Clear(_summaryRow);
        _summaryRow.AddChild(UiKit.Chip(
            $"{_dojo.Resources.Gold}",
            "gold",
            _dojo.Resources.Gold > 0 ? UiKit.Ink : UiKit.Warning,
            Mark.Coin));
        _summaryRow.AddChild(UiKit.Chip($"{rows.Count}", "men to equip", mark: Mark.Shield));

        if (rows.Count == 0)
        {
            _counter.AddChild(UiKit.Body("Nobody left to equip.", UiKit.Warning));
            return;
        }

        if (_selected is not WarriorId chosen || rows.All(r => r.Id != chosen))
        {
            _selected = rows[0].Id;
        }

        foreach (RosterRow row in rows)
        {
            Button button = UiKit.ListRow(
                $"{row.Name}  ·  {row.ArmorName}  ·  {ThrownName(row.Id)}",
                row.Id == _selected);

            WarriorId id = row.Id;
            button.Pressed += Guarded(_dojo, () =>
            {
                _selected = id;
                _notice.Text = string.Empty;
                Refresh();
            });

            _list.AddChild(button);
        }

        if (QuartermasterModel.Describe(_dojo, _selected!.Value) is CounterCard card)
        {
            _map.Slots = card.Slots;
            BuildCounter(card, rows.First(r => r.Id == _selected));
        }
    }

    private void BuildCounter(CounterCard card, RosterRow man)
    {
        // The counter served six slots and a forge with no one standing at it, so the player fitted
        // armour to a name. The man himself is at the head of it now — his own figure, with what he has
        // already lost missing from it, which is the same thing the slot map says in words.
        HBoxContainer standing = new();
        standing.AddThemeConstantOverride("separation", 12);
        _counter.AddChild(standing);

        WarriorPortrait fitted = new(PortraitCrop.Bust, new Vector2(110, 124));
        fitted.Print(man);
        standing.AddChild(fitted);

        VBoxContainer beside = new() { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        beside.AddThemeConstantOverride("separation", 4);
        standing.AddChild(beside);
        beside.AddChild(UiKit.OnPaper(man.Name, UiKit.Ink, UiKit.HeadSize, display: true));
        beside.AddChild(UiKit.Note($"{man.ArmorName}  ·  {man.WeaponName}", wrap: false));

        _counter.AddChild(UiKit.Rule());
        _counter.AddChild(UiKit.SectionLabel("Armour — six parts, each hit on its own"));

        foreach (ArmorSlotRow slot in card.Slots)
        {
            _counter.AddChild(SlotRow(slot));
        }

        _counter.AddChild(UiKit.Rule());
        _counter.AddChild(UiKit.SectionLabel("The blade"));
        _counter.AddChild(ForgeRow(card));

        _counter.AddChild(UiKit.Rule());
        _counter.AddChild(UiKit.SectionLabel("The throwing slot — one thing only"));

        _counter.AddChild(UiKit.Body(
            card.Carrying is ThrownWeapon thrown
                ? $"He carries {thrown.Name} — {thrown.Ammo} throws of {thrown.Damage:0} damage."
                : "His throwing slot is empty.",
            card.Carrying is null ? MutedColor : InkColor));

        foreach (ThrownOffer offer in card.Thrown)
        {
            _counter.AddChild(ThrownRow(card, offer));
        }
    }

    private Control SlotRow(ArmorSlotRow slot)
    {
        // Each part gets a card of its own: worn on the top line, everything the counter will sell for
        // it on the bottom. Six parts printed as loose labels ran together into one grey block, and
        // the player could not see where one part's buttons ended and the next part's began.
        PanelContainer panel = new();
        panel.AddThemeStyleboxOverride("panel", UiKit.PanelStyle(UiKit.Raised, radius: 3));
        VBoxContainer box = UiKit.Padded(panel, 10, 8);
        box.AddThemeConstantOverride("separation", 4);
        box.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;

        Label worn = UiKit.Body(
            slot.Worn.IsWorn
                ? $"{SlotName(slot.Slot)}: {slot.Worn.Name}  ·  worn {slot.Wear:0.0} of {slot.Worn.Durability:0}"
                : $"{SlotName(slot.Slot)}: bare",
            !slot.Worn.IsWorn ? MutedColor : slot.WornShare >= 0.75 ? WarningColor : InkColor);
        box.AddChild(worn);

        HFlowContainer buttons = new();
        buttons.AddThemeConstantOverride("h_separation", 6);
        buttons.AddThemeConstantOverride("v_separation", 4);
        box.AddChild(buttons);

        Button repair = new()
        {
            Text = slot.RepairPrice > 0 ? $"Mend ({slot.RepairPrice})" : "Mend",
            Disabled = !slot.CanRepair,
        };
        HitLocation region = slot.Slot;
        repair.Pressed += Guarded(_dojo, () => Repair(region));
        buttons.AddChild(repair);

        foreach (ArmorOffer offer in slot.Offers)
        {
            Button buy = new()
            {
                Text = $"{offer.Piece.Name} ({offer.Price})",
                Disabled = !offer.CanBuy,
                TooltipText = Reason(offer.Refusal),
            };

            ArmorOffer chosen = offer;
            buy.Pressed += Guarded(_dojo, () => Fit(chosen));
            buttons.AddChild(buy);
        }

        return panel;
    }

    private Control ForgeRow(CounterCard card)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 6);

        Label label = UiKit.Body($"{card.WeaponName}  ·  mending the whole kit costs {card.FullRepairPrice} gold");
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(label);

        Button forge = new()
        {
            Text = $"Reforge ({card.ForgePrice})",
            Disabled = !card.CanForge,
            TooltipText = Reason(card.ForgeRefusal),
        };
        forge.Pressed += Guarded(_dojo, Forge);
        row.AddChild(forge);

        return row;
    }

    private Control ThrownRow(CounterCard card, ThrownOffer offer)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 6);

        ThrownWeapon thrown = offer.Thrown;
        Label label = UiKit.Body(
            $"{thrown.Name}  ·  {thrown.Ammo} × {thrown.Damage:0} damage  ·  range {thrown.Range:0}"
            + (offer.ClassGated ? "  ·  wasted in an untrained hand" : string.Empty),
            offer.ClassGated ? PendingColor : InkColor);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(label);

        Button buy = new()
        {
            Text = $"Buy ({offer.Price})",
            Disabled = !offer.CanBuy,
            TooltipText = Reason(offer.Refusal),
        };

        buy.Pressed += Guarded(_dojo, () => Carry(card.Id, thrown));
        row.AddChild(buy);

        return row;
    }

    private void Fit(ArmorOffer offer)
    {
        if (Warrior() is not Warrior warrior)
        {
            return;
        }

        Say(
            _dojo.Quartermaster.Equip(_dojo, warrior, offer.Slot, offer.Piece),
            $"{offer.Piece.Name} fitted for {offer.Price} gold.",
            "The counter would not sell it.");

        After();
    }

    /// <summary>Mends the worn piece in one slot.</summary>
    private void Repair(HitLocation slot)
    {
        if (Warrior() is not Warrior warrior)
        {
            return;
        }

        int price = _dojo.Quartermaster.RepairPrice(warrior, slot);
        bool mended = _dojo.Quartermaster.Repair(_dojo, warrior, slot);

        Say(
            mended,
            $"{SlotName(slot)} mended for {price} gold.",
            "It could not be mended.");

        if (!mended)
        {
            Undone(
                $"{warrior.Name} · {SlotName(slot)} mended · {price} koku",
                "The counter would not mend it: there is nothing worn enough to mend there, or the "
                + "purse will not cover the work.",
                $"He marches with the {SlotName(slot).ToLowerInvariant()} as it is.");
        }

        After();
    }

    private void Forge()
    {
        if (Warrior() is not Warrior warrior)
        {
            return;
        }

        int price = _dojo.Quartermaster.ForgePrice(warrior);

        // The mastery he built on the old blade does not come with the new one, and the screen says so
        // rather than letting a veteran lose it quietly (docs/GDD.md §10).
        bool forged = _dojo.Quartermaster.Forge(_dojo, warrior);

        Say(
            forged,
            $"Reforged for {price} gold — the years he spent on the old blade are gone with it.",
            "The forge would not take it.");

        if (!forged)
        {
            Undone(
                $"{warrior.Name} · the blade reforged · {price} koku",
                "The forge would not take it: there is no sword forge with a smith in it, or the blade "
                + "has been through the fire once already.",
                "He marches with the blade he has.");
        }

        After();
    }

    private void Carry(WarriorId id, ThrownWeapon thrown)
    {
        if (_dojo.Roster.Find(id)?.Warrior is not Warrior warrior)
        {
            return;
        }

        int price = _dojo.Quartermaster.ThrownPrice(thrown);
        string had = warrior.Thrown?.Name ?? string.Empty;

        Say(
            _dojo.Quartermaster.EquipThrown(_dojo, warrior, thrown),
            $"{thrown.Name} bought for {price} gold."
            + (had.Length == 0 ? string.Empty : $" What he carried ({had}) is gone."),
            "The stall would not sell it.");

        After();
    }

    private Warrior? Warrior() =>
        _selected is WarriorId id ? _dojo.Roster.Find(id)?.Warrior : null;

    private void After()
    {
        Persist();
        Refresh();
    }

    /// <summary>Writes the counter's answer, in green when it sold and in red when it refused.</summary>
    private void Say(bool sold, string done, string refused)
    {
        _notice.Text = sold ? done : refused;
        _notice.AddThemeColorOverride("font_color", sold ? UiKit.Good : UiKit.Warning);
    }

    /// <summary>
    /// An order the counter took and then would not fill.
    /// </summary>
    /// <remarks>
    /// A line of red text under the counter is what this used to be, and it is the one thing a player
    /// does not read while he is looking at the rack. An order that came back undone is reported as
    /// what it is (design canvas -> 10a): the order quoted, what struck it, and what is still in the
    /// chest because of it.
    /// </remarks>
    private void Undone(string ordered, string struckBy, string cost) =>
        Returned(
            ordered,
            struckBy,
            cost,
            "Nothing. The money never left the chest, no day of the term was spent on it, and the "
            + "counter will take the same order again tomorrow.");

    private string ThrownName(WarriorId id) =>
        _dojo.Roster.Find(id)?.Warrior.Thrown?.Name ?? "nothing to throw";

    private static string Reason(CounterRefusal refusal) => refusal switch
    {
        CounterRefusal.TooExpensive => "The purse will not cover it.",
        CounterRefusal.AlreadyCarried => "He already has it.",
        CounterRefusal.NeedsSmith => "Plate is fitted by a smith of your own, in a plate works.",
        CounterRefusal.NeedsForge => "The sword forge and its smith — and a blade forged once is not forged twice.",
        CounterRefusal.Nothing => "There is nothing to mend.",
        _ => string.Empty,
    };

    private static string SlotName(HitLocation slot) => slot switch
    {
        HitLocation.Head => "Head",
        HitLocation.Torso => "Torso",
        HitLocation.SwordArm => "Sword arm",
        HitLocation.OffArm => "Off arm",
        HitLocation.RightLeg => "Right leg",
        _ => "Left leg",
    };
}
