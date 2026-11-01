using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using Bruinet.Samples.Shared.Commands;
using Bruinet.Samples.Shared.ViewModels;
using Bruinet.Samples.Todo.Core.Models;
using Bruinet.Samples.Todo.Core.Rules;
using Bruinet.Samples.Todo.Core.Services;

namespace Bruinet.Samples.Todo.Core.ViewModels;

/// <summary>
/// The user edit page, for a new user or an existing one.
/// </summary>
/// <remarks>
/// Save stays enabled while the form is invalid: pressing it shows every error at once, each on
/// its field, so "why can I not save" is visible. Save is disabled only offline, because users are
/// changed on the server. Typing in a field clears that field's error.
/// </remarks>
public sealed class UserEditViewModel : ParentViewModel
{
    private readonly UserService _users;
    private readonly IConnectivityState _connectivity;
    private readonly INavigator _navigator;

    private Guid? _id;
    private DateTimeOffset? _updatedAt;
    private string _name = string.Empty;
    private string _email = string.Empty;
    private UserStatus _status;
    private bool _hasEndDate;
    private DateTime _allowedUntil;
    private int _roleIndex;
    private double _hoursPerWeek = 40;
    private string _rateText = UserValidation.FormatRate(0);
    private TimeSpan _startOfDay = new(9, 0, 0);
    private bool _receivesReminders = true;
    private double _workload;
    private string _notes = string.Empty;
    private bool _isOnline;
    private readonly Dictionary<UserField, string> _errors = [];
    private string? _saveError;

    /// <summary>Creates the view model, as an empty new user.</summary>
    public UserEditViewModel(UserService users, IConnectivityState connectivity, INavigator navigator)
    {
        _users = users;
        _connectivity = connectivity;
        _navigator = navigator;

        _allowedUntil = _users.Today.ToDateTime(TimeOnly.MinValue);
        _isOnline = _connectivity.IsOnline;
        _connectivity.Changed += (_, _) => IsOnline = _connectivity.IsOnline;

        SaveCommand = new TargetAsyncCommand(this, SaveAsync, () => IsOnline);
        CancelCommand = new TargetAsyncCommand(this, () => _navigator.GoBackAsync());
    }

    /// <summary>Whether this is a new user.</summary>
    public bool IsNew => _id is null;

    /// <summary>"New user" or "Edit user".</summary>
    public string PageTitle => IsNew ? "New user" : "Edit user";

    /// <summary>"Last changed 10-Mar-2026 09:00", or empty for a new user.</summary>
    public string LastChangedText => _updatedAt is { } at
        ? "Last changed " + at.ToLocalTime().ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture)
        : string.Empty;

    /// <summary>The name as typed.</summary>
    public string Name
    {
        get => _name;
        set => SetField(ref _name, value ?? string.Empty, UserField.Name);
    }

    /// <summary>The email address as typed; empty for none.</summary>
    public string Email
    {
        get => _email;
        set => SetField(ref _email, value ?? string.Empty, UserField.Email);
    }

    /// <summary>The status, set through the three radio buttons.</summary>
    public UserStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(IsOnLeave));
                OnPropertyChanged(nameof(IsInactive));
            }
        }
    }

    /// <summary>The Active radio button.</summary>
    public bool IsActive
    {
        get => _status == UserStatus.Active;
        set
        {
            if (value)
            {
                Status = UserStatus.Active;
            }
        }
    }

    /// <summary>The On leave radio button.</summary>
    public bool IsOnLeave
    {
        get => _status == UserStatus.OnLeave;
        set
        {
            if (value)
            {
                Status = UserStatus.OnLeave;
            }
        }
    }

    /// <summary>The Inactive radio button.</summary>
    public bool IsInactive
    {
        get => _status == UserStatus.Inactive;
        set
        {
            if (value)
            {
                Status = UserStatus.Inactive;
            }
        }
    }

    /// <summary>Whether the user has an end date; off means <see cref="AllowedUntil"/> is ignored.</summary>
    public bool HasEndDate
    {
        get => _hasEndDate;
        set => SetProperty(ref _hasEndDate, value);
    }

    /// <summary>The picked end date. A <see cref="DateTime"/> because that is what DatePicker binds. May be in the past.</summary>
    public DateTime AllowedUntil
    {
        get => _allowedUntil;
        set => SetProperty(ref _allowedUntil, value.Date);
    }

    /// <summary>The role picker's options.</summary>
    public IReadOnlyList<string> RoleNames => UserRules.RoleNames;

    /// <summary>The role picker's selected index.</summary>
    public int RoleIndex
    {
        get => _roleIndex;
        set
        {
            if (value >= 0)
            {
                SetProperty(ref _roleIndex, value);
            }
        }
    }

    /// <summary>The hours per week. A <see cref="double"/> because that is what Stepper binds.</summary>
    public double HoursPerWeek
    {
        get => _hoursPerWeek;
        set
        {
            if (SetField(ref _hoursPerWeek, Math.Round(value), UserField.HoursPerWeek))
            {
                OnPropertyChanged(nameof(HoursText));
            }
        }
    }

    /// <summary>"38 hours", echoing the stepper.</summary>
    public string HoursText => $"{(int)_hoursPerWeek} hours";

    /// <summary>The hourly rate as typed: <c>1,250.75</c>.</summary>
    public string RateText
    {
        get => _rateText;
        set => SetField(ref _rateText, value ?? string.Empty, UserField.HourlyRate);
    }

    /// <summary>When the working day starts. A <see cref="TimeSpan"/> because that is what TimePicker binds.</summary>
    public TimeSpan StartOfDay
    {
        get => _startOfDay;
        set => SetProperty(ref _startOfDay, value);
    }

    /// <summary>Whether the user gets reminders.</summary>
    public bool ReceivesReminders
    {
        get => _receivesReminders;
        set => SetProperty(ref _receivesReminders, value);
    }

    /// <summary>The workload, snapped to steps of 5. A <see cref="double"/> because that is what Slider binds.</summary>
    public double Workload
    {
        get => _workload;
        set
        {
            var snapped = Math.Round(value / UserValidation.WorkloadStep) * UserValidation.WorkloadStep;
            if (SetField(ref _workload, snapped, UserField.Workload))
            {
                OnPropertyChanged(nameof(WorkloadText));
            }
        }
    }

    /// <summary>"35 %", echoing the slider.</summary>
    public string WorkloadText => $"{(int)_workload} %";

    /// <summary>The notes as typed.</summary>
    public string Notes
    {
        get => _notes;
        set => SetField(ref _notes, value ?? string.Empty, UserField.Notes);
    }

    /// <summary>The name's error, or <c>null</c>.</summary>
    public string? NameError => Error(UserField.Name);

    /// <summary>The email's error, or <c>null</c>.</summary>
    public string? EmailError => Error(UserField.Email);

    /// <summary>The hours' error, or <c>null</c>.</summary>
    public string? HoursError => Error(UserField.HoursPerWeek);

    /// <summary>The rate's error, or <c>null</c>.</summary>
    public string? RateError => Error(UserField.HourlyRate);

    /// <summary>The workload's error, or <c>null</c>.</summary>
    public string? WorkloadError => Error(UserField.Workload);

    /// <summary>The notes' error, or <c>null</c>.</summary>
    public string? NotesError => Error(UserField.Notes);

    /// <summary>Whether <see cref="NameError"/> shows.</summary>
    public bool HasNameError => NameError is not null;

    /// <summary>Whether <see cref="EmailError"/> shows.</summary>
    public bool HasEmailError => EmailError is not null;

    /// <summary>Whether <see cref="HoursError"/> shows.</summary>
    public bool HasHoursError => HoursError is not null;

    /// <summary>Whether <see cref="RateError"/> shows.</summary>
    public bool HasRateError => RateError is not null;

    /// <summary>Whether <see cref="WorkloadError"/> shows.</summary>
    public bool HasWorkloadError => WorkloadError is not null;

    /// <summary>Whether <see cref="NotesError"/> shows.</summary>
    public bool HasNotesError => NotesError is not null;

    /// <summary>Why the last save did not reach the server, or <c>null</c>.</summary>
    public string? SaveError
    {
        get => _saveError;
        private set
        {
            if (SetProperty(ref _saveError, value))
            {
                OnPropertyChanged(nameof(HasSaveError));
            }
        }
    }

    /// <summary>Whether <see cref="SaveError"/> shows.</summary>
    public bool HasSaveError => _saveError is not null;

    /// <summary>Whether changes can be sent; Save is disabled and the notice shows when not.</summary>
    public bool IsOnline
    {
        get => _isOnline;
        private set
        {
            if (SetProperty(ref _isOnline, value))
            {
                OnPropertyChanged(nameof(IsOffline));
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Whether the offline notice shows.</summary>
    public bool IsOffline => !IsOnline;

    /// <summary>The offline notice.</summary>
    public string OfflineNotice => UserService.OfflineMessage;

    /// <summary>Validates, saves on the server and returns. Disabled offline.</summary>
    public IAsyncRelayCommand SaveCommand { get; }

    /// <summary>Returns without saving.</summary>
    public IAsyncRelayCommand CancelCommand { get; }

    /// <summary>Loads a user to edit, or resets to a new one when <paramref name="id"/> is <c>null</c>.</summary>
    [SuppressMessage("Maintainability", "CA1502", Justification = "Known debt (AD-033): split when it next changes.")]
    public async Task LoadAsync(Guid? id)
    {
        var user = id is { } existing ? await _users.GetAsync(existing) : null;
        _id = user?.Id;
        _updatedAt = user?.UpdatedAt;

        _name = user?.Name ?? string.Empty;
        _email = user?.Email ?? string.Empty;
        _status = user?.Status ?? UserStatus.Active;
        _hasEndDate = user?.AllowedUntil is not null;
        _allowedUntil = (user?.AllowedUntil ?? _users.Today).ToDateTime(TimeOnly.MinValue);
        _roleIndex = (int)(user?.Role ?? UserRole.Member);
        _hoursPerWeek = user?.HoursPerWeek ?? 40;
        _rateText = UserValidation.FormatRate(user?.HourlyRate ?? 0);
        _startOfDay = (user?.StartOfDay ?? new TimeOnly(9, 0)).ToTimeSpan();
        _receivesReminders = user?.ReceivesReminders ?? true;
        _workload = user?.Workload ?? 0;
        _notes = user?.Notes ?? string.Empty;
        _errors.Clear();
        _saveError = null;
        _isOnline = _connectivity.IsOnline;

        OnPropertyChanged(string.Empty);
        SaveCommand.NotifyCanExecuteChanged();
    }

    private async Task SaveAsync()
    {
        SaveError = null;
        _errors.Clear();

        var rateError = UserValidation.ParseRate(RateText, out var rate);
        var user = new User
        {
            Id = _id ?? Guid.NewGuid(),
            Name = Name,
            Email = Email,
            Status = Status,
            AllowedUntil = HasEndDate ? DateOnly.FromDateTime(AllowedUntil) : null,
            Role = (UserRole)RoleIndex,
            HoursPerWeek = (int)HoursPerWeek,
            HourlyRate = rate,
            StartOfDay = TimeOnly.FromTimeSpan(StartOfDay),
            ReceivesReminders = ReceivesReminders,
            Workload = (int)Workload,
            Notes = Notes,
        };

        // A rate that cannot be read is never sent: the other fields are still checked, so every
        // error shows at once.
        if (rateError is not null)
        {
            ShowErrors(UserValidation.Validate(user, await _users.GetAllAsync()).Where(error => error.Field != UserField.HourlyRate));
            _errors[UserField.HourlyRate] = rateError;
            RaiseErrors();
            return;
        }

        var result = await _users.SaveAsync(user);
        ShowErrors(result.Errors);
        if (!result.Succeeded)
        {
            SaveError = result.Failure;
            return;
        }

        await _navigator.GoBackAsync();
    }

    private void ShowErrors(IEnumerable<UserFieldError> errors)
    {
        foreach (var error in errors)
        {
            _errors[error.Field] = error.Message;
        }

        RaiseErrors();
    }

    private string? Error(UserField field) => _errors.GetValueOrDefault(field);

    private bool SetField<T>(ref T storage, T value, UserField field, [CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref storage, value, propertyName))
        {
            return false;
        }

        if (_errors.Remove(field))
        {
            RaiseErrors();
        }

        return true;
    }

    private void RaiseErrors()
    {
        foreach (var name in new[]
        {
            nameof(NameError), nameof(EmailError), nameof(HoursError), nameof(RateError), nameof(WorkloadError), nameof(NotesError),
            nameof(HasNameError), nameof(HasEmailError), nameof(HasHoursError), nameof(HasRateError), nameof(HasWorkloadError), nameof(HasNotesError),
        })
        {
            OnPropertyChanged(name);
        }
    }
}
