using System.ComponentModel;
using System.Runtime.CompilerServices;
using GasbynOptimizer.Core;

namespace GasbynOptimizer.UI;

/// <summary>Řádek v seznamu optimalizací (model pro zobrazení).</summary>
public sealed class TweakItem : INotifyPropertyChanged
{
    private TweakState _state;
    private bool _isBusy;
    private string _status = "";
    private bool _statusIsError;

    public TweakItem(Tweak tweak, TweakState state)
    {
        Tweak = tweak;
        _state = state;
    }

    public Tweak Tweak { get; }

    public string Name => Tweak.Name;
    public string Description => Tweak.Description;
    public string Category => Tweak.Category;
    public string Note => Tweak.Note;
    public bool HasNote => !string.IsNullOrEmpty(Tweak.Note);
    public bool Recommended => Tweak.Recommended;

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
        }
    }

    public bool IsOn => State == TweakState.On;
    public bool IsPartial => State == TweakState.Partial;
    public bool IsAvailable => State != TweakState.NotApplicable;
    public bool CanToggle => IsAvailable && !IsBusy;

    public string StateText => State switch
    {
        TweakState.On => "Zapnuto",
        TweakState.Off => "Vypnuto",
        TweakState.Partial => "Částečně",
        _ => "Nedostupné"
    };

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
