using Bruinet.Core.Exceptions;
using Bruinet.Stride.Communication;
using Bruinet.Stride.Interfaces;

namespace Bruinet.Stride.Controls;

/// <summary>
/// Base class for selector controls (ListBox, ComboBox).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent method chaining.</typeparam>
public abstract class SelectorControlBase<TScope> : ControlBase<TScope>
    where TScope : IStrideScope<TScope>
{
    protected SelectorControlBase(IStrideScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    public IReadOnlyList<string> GetItems() => GetState().Items ?? [];

    public int GetItemCount() => GetItems().Count;

    public string? GetSelectedText() => GetState().SelectedText;

    public int GetSelectedIndex() => GetState().SelectedIndex;

    public string GetItemText(int index)
    {
        var items = GetItems();
        if (index < 0 || index >= items.Count)
            throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is outside range [0, {items.Count - 1}]");
        return items[index];
    }

    public bool HasItem(string text) => GetItems().Contains(text);

    public virtual TScope SelectByIndex(int index)
    {
        var items = GetItems();
        if (index < 0 || index >= items.Count)
            throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is outside range [0, {items.Count - 1}]");

        Context.SendCommand(AutomationCommand.Action("SelectIndex", AutomationId, index));
        return ContainingScope;
    }

    public virtual TScope SelectByText(string text)
    {
        var items = GetItems();
        var index = items.ToList().IndexOf(text);
        if (index < 0)
            throw new InvalidOperationException($"Item '{text}' not found. Available: {string.Join(", ", items)}");

        return SelectByIndex(index);
    }

    public bool WaitSelectedText(string? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        return Poll(() => GetSelectedText() == expected, timeoutMs ?? Context.Timeouts.DefaultWait);
    }

    public TScope AssertSelectedText(string? expected, string? message = null)
    {
        if (expected == null)
            return ContainingScope;

        var actual = GetSelectedText();
        if (actual != expected)
        {
            throw new AssertionException(
                message ?? $"Control '{AutomationId}' selection mismatch. Expected: '{expected}', Actual: '{actual}'");
        }
        return ContainingScope;
    }

    public TScope AssertItemCount(int? expected, string? message = null)
    {
        if (expected == null)
            return ContainingScope;

        var actual = GetItemCount();
        if (actual != expected.Value)
        {
            throw new AssertionException(
                message ?? $"Control '{AutomationId}' item count mismatch. Expected: {expected}, Actual: {actual}");
        }
        return ContainingScope;
    }

    public TScope AssertItemExists(string text, string? message = null)
    {
        if (!HasItem(text))
        {
            throw new AssertionException(
                message ?? $"Control '{AutomationId}' does not contain item '{text}'. Available: {string.Join(", ", GetItems())}");
        }
        return ContainingScope;
    }
}
