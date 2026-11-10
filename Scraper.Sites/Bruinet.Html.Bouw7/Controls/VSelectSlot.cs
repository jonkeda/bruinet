// <scraped control="div||v-select__slot|div+,input" from="2026-10-02-recording/008-department.nodes.jsonl#478" />
using Bruinet.Core.Collections;
using Bruinet.Core.Locators;
using Bruinet.Core.Scopes;
using Bruinet.Html.Collections;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Bouw7.Controls;

public sealed class VSelectSlot<TParent>(IHtmlScope<TParent> parent, Locator locator)
    : CollectionObjectBase<TParent, VSelectSlot<TParent>, VSelectSlotRow>(
        parent,
        locator,
        Locator.ByCss("div.v-input__append-inner"))
    where TParent : IHtmlScope<TParent>
{
    public VSelectSlot(IHtmlScope<TParent> parent, string selectorOrId)
        : this(parent, ResolveLocator(selectorOrId))
    {
    }

    public override VSelectSlot<TParent> Self => this;

    protected override VSelectSlotRow CreateItem(ItemKey key, int position) => new(this, key, position);

    public VSelectSlotRow Row(int index, int? timeoutMs = null) => Item(index, timeoutMs);

    private static Locator ResolveLocator(string selectorOrId)
    {
        if (string.IsNullOrWhiteSpace(selectorOrId))
        {
            throw new ArgumentNullException(nameof(selectorOrId));
        }

        return IsRawSelector(selectorOrId)
            ? Locator.ByCss(selectorOrId)
            : Locator.ByAutomationId(selectorOrId);
    }

    private static bool IsRawSelector(string value)
        => value.StartsWith('#')
           || value.StartsWith('.')
           || value.StartsWith('[')
           || value.Contains('#')
           || value.Contains('[')
           || value.Contains(':')
           || value.Contains('>')
           || value.Contains(' ');
}

public sealed class VSelectSlotRow(IHtmlCollection rows, ItemKey key, int position)
    : ItemObjectBase<VSelectSlotRow>(rows, key, position)
{
    public override VSelectSlotRow Self => this;
}
