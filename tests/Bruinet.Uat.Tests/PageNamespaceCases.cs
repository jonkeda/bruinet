using Bruinet.Bdd;
using Bruinet.Core.Composition;
using Bruinet.Core.Testing;

namespace Bruinet.Uat.Tests.PageNamespaceCases;

// Two pages named Ledger, one per namespace: the composition keeps both.
[TestPage("Ledger")]
[BddNamespace("Invoicing")]
[BddIgnore]
public sealed class InvoicingLedgerPage
{
}

[TestPage("Ledger")]
[BddNamespace("Accounts")]
[BddIgnore]
public sealed class AccountsLedgerPage
{
}

// A scanned page that declares its step name: the steps say "the Contacts page", not "the Contact page".
[TestPage]
[BddPage("Contacts")]
[BddIgnore]
public sealed class ContactPage
{
}
