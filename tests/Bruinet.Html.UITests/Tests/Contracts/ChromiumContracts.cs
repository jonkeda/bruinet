using Bruinet.ControlContracts.Html;
using Bruinet.Html.UITests.TestBase;

namespace Bruinet.Html.UITests.Tests.Contracts;

// The control contracts in Chromium (.my/coverage/plan.md, C3): the same bindings as the bUnit head's
// (Bruinet.Html.Bunit.Tests), on the pages Bruinet.ControlContracts.Html.Site serves. Always the browser,
// whatever BRUINET_HEAD says: the bUnit head runs them in Bruinet.Html.Bunit.Tests.

public sealed class ChromiumElementContract : HtmlElementBinding<ChromiumHead>;

public sealed class ChromiumClickableContract : HtmlClickableBinding<ChromiumHead>;

public sealed class ChromiumFocusableContract : HtmlFocusableBinding<ChromiumHead>;

public sealed class ChromiumTextContract : HtmlTextBinding<ChromiumHead>;

public sealed class ChromiumTextInputContract : HtmlTextInputBinding<ChromiumHead>;

public sealed class ChromiumTextAreaContract : HtmlTextAreaBinding<ChromiumHead>;

public sealed class ChromiumCheckBoxContract : HtmlCheckBoxBinding<ChromiumHead>;

public sealed class ChromiumRangeInputContract : HtmlRangeInputBinding<ChromiumHead>;

public sealed class ChromiumSelectContract : HtmlSelectBinding<ChromiumHead>;

public sealed class ChromiumRadioGroupContract : HtmlRadioGroupBinding<ChromiumHead>;

public sealed class ChromiumBlazorControls : BlazorControlsBinding<ChromiumHead>;
