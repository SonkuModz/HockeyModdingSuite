using System.Windows.Media.Imaging;
using NHL27Kit.Core;

namespace NHL27Kit.App;

/// <summary>One uniform slot on the Uniforms page's Jersey slots tab: the slot as the game has it and as edited.</summary>
public sealed class SlotRow : Observable
{
    private readonly Action<SlotRow> _changed;
    private JerseySlot _slot;
    private BitmapSource _thumb;
    private int _position;
    private bool _selected;

    public SlotRow(JerseySlot slot, Kit kit, string kitTeam, int position, List<PickItem> labels, Action<SlotRow> changed)
    {
        Original = slot.Row >= 0 ? slot : null;
        OriginalPosition = slot.Row >= 0 ? position : -1;
        _slot = slot;
        _position = position;
        Kit = kit;
        KitTeam = kitTeam;
        Labels = labels;
        _changed = changed;
    }

    /// <summary>The slot as the game has it (null for a slot added here).</summary>
    public JerseySlot Original { get; }
    public int OriginalPosition { get; }
    public JerseySlot Slot => _slot;
    /// <summary>The kit folder the slot shows (null when no folder matches its art, brand and number).</summary>
    public Kit Kit { get; }
    /// <summary>The kit's team when it isn't the slot's own (a slot can show another team's kit).</summary>
    public string KitTeam { get; }
    public List<PickItem> Labels { get; }

    public bool IsNew => Original == null;
    public string Title => Kit == null ? $"{JerseySlots.Brands.ElementAtOrDefault(_slot.Style)} #{_slot.Variant} (no kit folder)"
        : $"{UniformNames.Pretty(Kit.Brand)}  ·  {UniformNames.Pretty(Kit.Name)}";
    public string Meta => (IsNew ? "new slot" : $"slot #{_slot.JerseyIndex}") + (KitTeam != null ? $"  ·  kit from {KitTeam}" : "")
        + (Original != null && _position != OriginalPosition ? $"  ·  was {OriginalPosition + 1}." : "")
        + (IsNameChanged ? $"  ·  was “{_originalName}”" : "");
    public string Number => $"{_position + 1}.";
    public BitmapSource Thumb { get => _thumb; set => Set(ref _thumb, value); }
    public bool IsSelected { get => _selected; set => Set(ref _selected, value); }

    public int Position
    {
        get => _position;
        set { if (Set(ref _position, value)) { Raise(nameof(Number)); Raise(nameof(Meta)); Raise(nameof(IsSlotChanged)); Raise(nameof(IsChanged)); } }
    }

    public PickItem LabelPick
    {
        get => Labels.FirstOrDefault(l => l.Value == _slot.Label);
        set { if (value != null && value.Value != _slot.Label) Edit(_slot with { Label = value.Value }); }
    }

    public bool IsDefault { get => _slot.IsDefault; set { if (value != _slot.IsDefault) Edit(_slot with { IsDefault = value }); } }
    public bool IsLight { get => _slot.IsLight; set { if (value != _slot.IsLight) Edit(_slot with { IsLight = value }); } }

    /// <summary>Added, moved, or with other values than the game's (its name aside).</summary>
    public bool IsSlotChanged => Original == null || _position != OriginalPosition || _slot != Original;
    public bool IsChanged => IsSlotChanged || IsNameChanged;

    // ---------------------------------------------------------------- the kit's name (language databases)

    private string _name, _originalName;
    private Func<string, (int Need, int Have)> _budget;

    /// <summary>The kit's name id (vintage_…) when the kit has a name in the game, else null.</summary>
    public string NameKey { get; private set; }
    public bool HasName => NameKey != null;
    public bool NoName => NameKey == null;

    /// <summary>Gives the row its kit's name as the game has it, and how to measure a new one (English strings).</summary>
    public void SetName(string key, string name, Func<string, (int Need, int Have)> budget, string edited = null)
    {
        NameKey = key;
        _originalName = name;
        _name = edited ?? name;
        _budget = budget;
        foreach (string p in new[] { nameof(HasName), nameof(NoName), nameof(Name), nameof(NameRoom), nameof(NameError), nameof(HasNameError), nameof(IsNameChanged), nameof(IsChanged) }) Raise(p);
    }

    public string OriginalName => _originalName;

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            foreach (string p in new[] { nameof(Name), nameof(NameRoom), nameof(NameError), nameof(HasNameError), nameof(IsNameChanged), nameof(IsChanged), nameof(Meta) }) Raise(p);
            _changed?.Invoke(this);
        }
    }

    public bool IsNameChanged => HasName && _name != _originalName;

    private (int Need, int Have) Room => _budget?.Invoke(_name ?? "") ?? (0, 0);

    /// <summary>"12 of 14 bytes": how much of the old name's space the new one takes (names are rewritten in place).</summary>
    public string NameRoom => !HasName ? "" : Room.Need < 0 ? "" : $"{Room.Need} of {Room.Have}";

    public string NameError => !HasName ? null
        : string.IsNullOrWhiteSpace(_name) ? "A name can't be empty"
        : Room.Need < 0 ? "Has a character the game's English text never uses"
        : Room.Need > Room.Have ? $"Too long: {Room.Need - Room.Have} byte(s) over the old name's space"
        : null;

    public bool HasNameError => NameError != null;

    private void Edit(JerseySlot s)
    {
        _slot = s;
        foreach (string p in new[] { nameof(LabelPick), nameof(IsDefault), nameof(IsLight), nameof(IsSlotChanged), nameof(IsChanged), nameof(Meta) }) Raise(p);
        _changed?.Invoke(this);
    }
}

/// <summary>A kit the Add slot picker offers.</summary>
public sealed record SlotKitItem(Kit Kit, JerseySlots.KitTeam Team, string Label)
{
    public override string ToString() => Label;
}
