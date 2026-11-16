namespace Bruinet.Samples.NativeIos.App.Screens;

/// <summary>
/// 200 invoices in a <c>UITableView</c>, so cells are reused as the list scrolls. Delete removes a
/// row; pulling to refresh is busy for a moment.
/// </summary>
public sealed class InvoicesScreen() : Screen("Invoices")
{
    private readonly List<(string Name, string Amount)> _invoices =
        [.. Enumerable.Range(1, 200).Select(n => ($"Invoice {n}", $"€ {n * 10},00"))];

    private UILabel _count = null!;
    private UITableView _table = null!;

    /// <inheritdoc />
    protected override void Build()
    {
        _count = Label("count", string.Empty);
        _count.TranslatesAutoresizingMaskIntoConstraints = false;
        _table = new UITableView { AccessibilityIdentifier = "invoices", TranslatesAutoresizingMaskIntoConstraints = false, RowHeight = 64 };
        _table.Source = new InvoiceSource(_invoices, Delete);
        var refresh = new UIRefreshControl { AccessibilityIdentifier = "refresh" };
        refresh.ValueChanged += (_, _) => Busy(1000, () =>
        {
            refresh.EndRefreshing();
            _count.Text = $"{_invoices.Count} invoices, refreshed";
        });
        _table.RefreshControl = refresh;

        var guide = View!.SafeAreaLayoutGuide;
        View.AddSubview(_count);
        View.AddSubview(_table);
        _count.TopAnchor.ConstraintEqualTo(guide.TopAnchor, 8).Active = true;
        _count.LeadingAnchor.ConstraintEqualTo(guide.LeadingAnchor, 16).Active = true;
        _count.TrailingAnchor.ConstraintEqualTo(guide.TrailingAnchor, -16).Active = true;
        _table.TopAnchor.ConstraintEqualTo(_count.BottomAnchor, 8).Active = true;
        _table.LeadingAnchor.ConstraintEqualTo(guide.LeadingAnchor).Active = true;
        _table.TrailingAnchor.ConstraintEqualTo(guide.TrailingAnchor).Active = true;
        _table.BottomAnchor.ConstraintEqualTo(guide.BottomAnchor).Active = true;
        ShowCount();
    }

    private void Delete(string name)
    {
        var position = _invoices.FindIndex(i => i.Name == name);
        if (position < 0)
        {
            return;
        }

        _invoices.RemoveAt(position);
        _table.DeleteRows([NSIndexPath.FromRowSection(position, 0)], UITableViewRowAnimation.Automatic);
        ShowCount();
    }

    private void ShowCount() => _count.Text = $"{_invoices.Count} invoices";

    private sealed class InvoiceSource(List<(string Name, string Amount)> invoices, Action<string> delete) : UITableViewSource
    {
        public override nint RowsInSection(UITableView tableview, nint section) => invoices.Count;

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            var cell = tableView.DequeueReusableCell(InvoiceCell.Key) as InvoiceCell ?? new InvoiceCell(delete);
            var (name, amount) = invoices[indexPath.Row];
            cell.Show(name, amount);
            return cell;
        }
    }

    private sealed class InvoiceCell : UITableViewCell
    {
        public const string Key = "invoice";

        private readonly UILabel _name = new() { AccessibilityIdentifier = "name" };
        private readonly UILabel _amount = new() { AccessibilityIdentifier = "amount" };

        public InvoiceCell(Action<string> delete)
            : base(UITableViewCellStyle.Default, Key)
        {
            SelectionStyle = UITableViewCellSelectionStyle.None;
            var remove = UIButton.FromType(UIButtonType.System);
            remove.SetImage(UIImage.GetSystemImage("trash"), UIControlState.Normal);
            remove.AccessibilityIdentifier = "delete";
            remove.AccessibilityLabel = "Delete";
            remove.TouchUpInside += (_, _) => delete(_name.Text!);
            var row = new UIStackView([_name, _amount, remove]) { Axis = UILayoutConstraintAxis.Horizontal, Spacing = 12, TranslatesAutoresizingMaskIntoConstraints = false };
            _name.SetContentHuggingPriority(1, UILayoutConstraintAxis.Horizontal);
            ContentView.AddSubview(row);
            row.CenterYAnchor.ConstraintEqualTo(ContentView.CenterYAnchor).Active = true;
            row.LeadingAnchor.ConstraintEqualTo(ContentView.LeadingAnchor, 16).Active = true;
            row.TrailingAnchor.ConstraintEqualTo(ContentView.TrailingAnchor, -8).Active = true;
        }

        public void Show(string name, string amount)
        {
            _name.Text = name;
            _amount.Text = amount;
        }
    }
}
