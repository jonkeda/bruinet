using Bruinet.Samples.Shared.Commands;
using Bruinet.Samples.Shared.ViewModels;
using Bruinet.Samples.Todo.Core.Models;
using Bruinet.Samples.Todo.Core.Rules;
using Bruinet.Samples.Todo.Core.Services;
using System.Globalization;

namespace Bruinet.Samples.Todo.Core.ViewModels;

/// <summary>
/// The detail page: one todo, its status (changeable in place), Edit and Delete.
/// </summary>
public sealed class TodoDetailViewModel : ParentViewModel
{
    /// <summary>The delete confirmation's title.</summary>
    public const string DeleteTitle = "Delete todo?";

    /// <summary>The delete confirmation's accept button.</summary>
    public const string DeleteAccept = "Delete";

    /// <summary>The delete confirmation's cancel button.</summary>
    public const string DeleteCancel = "Cancel";

    private readonly TodoService _todos;
    private readonly INavigator _navigator;
    private readonly IDialogs _dialogs;

    private TodoItem? _item;
    private bool _loading;
    private TodoStatus _status;

    /// <summary>Creates the view model.</summary>
    public TodoDetailViewModel(TodoService todos, INavigator navigator, IDialogs dialogs)
    {
        _todos = todos;
        _navigator = navigator;
        _dialogs = dialogs;

        EditCommand = new TargetAsyncCommand(this, EditAsync, () => _item is not null);
        DeleteCommand = new TargetAsyncCommand(this, DeleteAsync, () => _item is not null);
    }

    /// <summary>The todo shown, once loaded.</summary>
    public Guid? Id => _item?.Id;

    /// <summary>The title.</summary>
    public string Title => _item?.Title ?? string.Empty;

    /// <summary>The notes, or empty.</summary>
    public string Notes => _item?.Notes ?? string.Empty;

    /// <summary>Whether there are notes to show.</summary>
    public bool HasNotes => !string.IsNullOrEmpty(_item?.Notes);

    /// <summary>The due date, or "No due date". The card already labels it "Due".</summary>
    public string DueText => TodoFormatting.Date(_item?.DueDate, "No due date", CultureInfo.CurrentCulture);

    /// <summary>When it was created, local time.</summary>
    public string CreatedText => _item is null ? string.Empty : TodoFormatting.Timestamp(_item.CreatedAt, culture: CultureInfo.CurrentCulture);

    /// <summary>When it last changed, local time.</summary>
    public string UpdatedText => _item is null ? string.Empty : TodoFormatting.Timestamp(_item.UpdatedAt, culture: CultureInfo.CurrentCulture);

    /// <summary>"Synced", "Waiting to sync" or "Local only".</summary>
    public string SyncText => _item is null ? string.Empty : TodoFormatting.SyncState(_item.SyncState);

    /// <summary>Whether the status control shows Overdue.</summary>
    public bool IsOverdue => _item is not null && TodoStatusRules.IsOverdue(_item.Status, _item.DueDate, _todos.Today);

    /// <summary>Whether the due date has passed; the status control shows Overdue for it unless Done.</summary>
    public bool IsPastDue => _item?.DueDate < _todos.Today;

    /// <summary>
    /// The status. The status control's Next sets it, and the change is saved at once, without
    /// opening Edit (TOD.05.5).
    /// </summary>
    public TodoStatus Status
    {
        get => _status;
        set
        {
            if (!SetProperty(ref _status, value) || _loading || _item is null)
            {
                return;
            }

            StatusSave = SaveStatusAsync(value);
        }
    }

    /// <summary>The last status save, awaitable for tests.</summary>
    public Task StatusSave { get; private set; } = Task.CompletedTask;

    /// <summary>Opens the edit page for this todo.</summary>
    public IAsyncRelayCommand EditCommand { get; }

    /// <summary>Asks, then deletes and returns to the list.</summary>
    public IAsyncRelayCommand DeleteCommand { get; }

    /// <summary>Loads (or reloads, after an edit) the todo.</summary>
    public async Task LoadAsync(Guid id)
    {
        Show(await _todos.GetAsync(id));
    }

    /// <summary>Reloads on returning from the edit page.</summary>
    public override void OnViewAppearing()
    {
        base.OnViewAppearing();
        if (_item is not null)
        {
            _ = LoadAsync(_item.Id);
        }
    }

    private async Task SaveStatusAsync(TodoStatus status)
    {
        Show(await _todos.SetStatusAsync(_item!.Id, status));
    }

    private Task EditAsync() => _navigator.GoToAsync(TodoRoutes.Edit, TodoRoutes.ForTodo(_item!.Id));

    private async Task DeleteAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(
            DeleteTitle,
            $"\"{_item!.Title}\" will be deleted.",
            DeleteAccept,
            DeleteCancel);

        if (!confirmed)
        {
            return;
        }

        await _todos.DeleteAsync(_item.Id);
        await _navigator.GoBackAsync();
    }

    private void Show(TodoItem? item)
    {
        _item = item;

        _loading = true;
        Status = item?.Status ?? TodoStatus.Open;
        _loading = false;

        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(DueText));
        OnPropertyChanged(nameof(CreatedText));
        OnPropertyChanged(nameof(UpdatedText));
        OnPropertyChanged(nameof(SyncText));
        OnPropertyChanged(nameof(IsOverdue));
        OnPropertyChanged(nameof(IsPastDue));
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }
}
