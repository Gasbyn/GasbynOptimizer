using System.ComponentModel;
using System.Runtime.CompilerServices;
using GasbynOptimizer.Core;

namespace GasbynOptimizer.UI;

/// <summary>Řádek v seznamu optimalizací (model pro zobrazení).</summary>
public sealed class TweakItem : INotifyPropertyChanged
{
    private TweakState _state;
    private bool _isBusy;
    private bool _advanced;
    private string _status = "";
    private bool _statusIsError;

    public TweakItem(Tweak tweak, TweakState state, string? unsupportedReason, bool recommended, bool advanced)
    {
        Tweak = tweak;
        _state = state;
        UnsupportedReason = unsupportedReason;
        IsRecommended = recommended;
        _advanced = advanced;
    }

    public Tweak Tweak { get; }

    public string Name => Tweak.Name;
    public string Description => Tweak.Description;
    public string Category => Tweak.Category;
    public string Note => Tweak.Note;
    public bool HasNote => !string.IsNullOrEmpty(Tweak.Note);
    public bool IsExperimental => Tweak.Experimental;
    public bool IsApplyOnly => Tweak.ApplyOnly;
    public string ApplyText => IsOn ? "Aktivní" : "Použít";
    public bool IsNew => Tweak.IsNew;

    /// <summary>Doporučeno pro tento počítač (podle hardwaru a Windows).</summary>
    public bool IsRecommended { get; }

    // --- podpora --------------------------------------------------------------------------------

    public string? UnsupportedReason { get; }
    public bool IsSupported => UnsupportedReason == null;
    public string UnsupportedText => IsSupported ? "" : "Nepodporováno: " + UnsupportedReason;

    /// <summary>Nepodporovaná a pokročilý režim je vypnutý: ztmavit, zámek, nejde zapnout.</summary>
    public bool ShowLock => !IsSupported && !_advanced;

    /// <summary>Nepodporovaná, ale pokročilý režim je zapnutý: jen štítek, jde zapnout po potvrzení.</summary>
    public bool ShowUnsupportedBadge => !IsSupported && _advanced;

    public void SetAdvanced(bool value)
    {
        if (_advanced == value) return;
        _advanced = value;
        Raise(nameof(ShowLock));
        Raise(nameof(ShowUnsupportedBadge));
        Raise(nameof(CanToggle));
        Raise(nameof(StateText));
    }

    // --- stav -----------------------------------------------------------------------------------

    public TweakState State
    {
        get => _state;
        set
        {
            if (_state == value) return;
            _state = value;
            Raise();
            Raise(nameof(IsOn));
            Raise(nameof(IsPartial));
            Raise(nameof(IsAvailable));
            Raise(nameof(CanToggle));
            Raise(nameof(StateText));
            Raise(nameof(ApplyText));
            Raise(nameof(ShowUnavailable));
        }
    }

    public bool IsOn => State == TweakState.On;
    public bool IsPartial => State == TweakState.Partial;
    public bool IsAvailable => State != TweakState.NotApplicable;

    public bool CanToggle =>
        IsAvailable && !IsBusy && (IsSupported || _advanced || IsOn || IsPartial) && !(IsApplyOnly && IsOn);

    /// <summary>Podporovaná optimalizace, ale na tomto PC chybí to, co mění (služba, úloha, program).</summary>
    public bool ShowUnavailable => IsSupported && !IsAvailable;

    public string UnavailableText =>
        "Nelze zapnout: na tomto počítači nebylo nalezeno nic, co by tato optimalizace měnila (např. chybí služba nebo program).";

    public string StateText
    {
        get
        {
            if (ShowLock) return "🔒 Nepodporováno";
            if (IsApplyOnly)
                return State == TweakState.On ? "Aktivní plán" : State == TweakState.NotApplicable ? "Nedostupné" : "Neaktivní";

            return State switch
            {
                TweakState.On => "Zapnuto",
                TweakState.Off => "Vypnuto",
                TweakState.Partial => "Částečně",
                _ => "Nedostupné"
            };
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            Raise();
            Raise(nameof(CanToggle));
        }
    }

    public string Status
    {
        get => _status;
        private set
        {
            _status = value;
            Raise();
            Raise(nameof(HasStatus));
        }
    }

    public bool HasStatus => !string.IsNullOrEmpty(_status);

    public bool StatusIsError
    {
        get => _statusIsError;
        private set
        {
            _statusIsError = value;
            Raise();
        }
    }

    public void SetStatus(string text, bool isError)
    {
        StatusIsError = isError;
        Status = text;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
