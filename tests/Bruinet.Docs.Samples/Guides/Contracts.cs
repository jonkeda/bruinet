// docs/guides/contracts.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
using Bruinet.Contracts;

namespace Bruinet.Docs.Samples.Guides;

#region docs:module
public sealed class ContactsContracts : IContractModule
{
    public static readonly DtoContract ContactDetail = new("GET /contact/{id}", typeof(ContactDto));

    public string Name => "Contacts";
    public IReadOnlyList<DtoContract> Contracts => [ContactDetail];
    public IReadOnlyList<MockContractRequest> MockRequests => [new(ContactDetail, "/contact/101")];
}
#endregion

public sealed record ContactDto(int Id, string Name);
