namespace Bruinet.Contracts.Tests;

[Trait("Category", "Unit")]
[Trait("Feature", "Contract")]
[Trait("Pyramid", "Unit")]
public sealed class LiveContractGateTests
{
    private static readonly LiveContractSettings Live = new()
    {
        LiveApi = true,
        RealSignIn = true,
        Account = "reader",
        LiveApiSetting = "capabilities.liveApi",
        MockBackendSetting = "mockBackend.enabled",
        SignInSetting = "auth.mode",
        AccountSetting = "auth.account",
    };

    [Fact]
    public void SkipReason_RunsOnlyALiveRealRunForTheTestAccount()
    {
        Assert.Null(LiveContractGate.SkipReason(Live, "Reader"));
        Assert.Contains("capabilities.liveApi", LiveContractGate.SkipReason(Live with { LiveApi = false }, "reader"));
        Assert.Contains("mockBackend.enabled", LiveContractGate.SkipReason(Live with { MockBackend = true }, "reader"));
        Assert.Contains("auth.mode", LiveContractGate.SkipReason(Live with { RealSignIn = false }, "reader"));
        Assert.Contains("auth.account 'admin'", LiveContractGate.SkipReason(Live, "admin"));
        Assert.Contains(".env.local", LiveContractGate.SkipReason(Live with { AccountDefined = false }, "reader"));
    }

    [Fact]
    public void LocalAccountStore_ReadsNamedAndDefaultAccounts()
    {
        string[] lines =
        [
            "# comment",
            "APP_AUTH_USERNAME=default@example.test",
            "APP_AUTH_PASSWORD=pw0",
            "APP_AUTH_READER_USERNAME=\"reader@example.test\"",
            "APP_AUTH_READER_PASSWORD=pw=1",
            "APP_AUTH_READER_ORGANIZATION=Org A",
            "APP_AUTH_ADMIN_USERNAME=",
        ];
        var store = new LocalAccountStore("APP_AUTH_");

        Assert.Equal(new LocalAccount("reader@example.test", "pw=1", "Org A"), store.Parse(lines, "Reader"));
        Assert.Equal("default@example.test", store.Parse(lines, LocalAccountStore.DefaultAccountName)?.Username);
        Assert.Null(store.Parse(lines, "admin"));
    }

    [Fact]
    public void LocalAccountStore_FindsTheNearestFileUpwards()
    {
        var root = Directory.CreateTempSubdirectory("bruinet-accounts-");
        try
        {
            var nested = root.CreateSubdirectory("a").CreateSubdirectory("b");
            File.WriteAllLines(Path.Combine(root.FullName, ".env.local"), ["APP_AUTH_USERNAME=u", "APP_AUTH_PASSWORD=p"]);

            var account = new LocalAccountStore("APP_AUTH").Find("default", nested.FullName);

            Assert.Equal(new LocalAccount("u", "p", string.Empty), account);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
