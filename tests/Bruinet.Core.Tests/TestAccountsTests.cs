using Bruinet.Core.Testing;
using Bruinet.Core.Utilities;

namespace Bruinet.Core.Tests;

public sealed class TestAccountsTests
{
    private const string Accounts = """
        # Test accounts

        ## Accounts

        | Name | Role | Organization | Writes |
        | --- | --- | --- | --- |
        | NormalUser | user | Demolitions | yes |
        | Reader | user | Demo | no |
        | Admin | admin | Demolitions | |
        """;

    private const string Secrets = """
        ## Accounts

        | Name | Username | Password | Organization |
        | --- | --- | --- | --- |
        | NormalUser | user@example.com | pa\|ss | |
        | Reader | reader@example.com | secret | Demo two |
        | Admin | admin@example.com | | |
        """;

    [Fact]
    public void Get_JoinsTheTwoFiles_ByName()
    {
        var account = TestAccounts.Read(Accounts, Secrets).Get("NormalUser");

        Assert.Equal("user@example.com", account.Username);
        Assert.Equal("pa|ss", account.Password);
        Assert.Equal("Demolitions", account.Organization);
        Assert.True(account.Writes);
        Assert.Equal("user", account["Role"]);
    }

    [Fact]
    public void Get_TheLocalFilesOrganization_WinsOverTheTables()
        => Assert.Equal("Demo two", TestAccounts.Read(Accounts, Secrets).Get("Reader").Organization);

    [Fact]
    public void Get_WritesNo_IsAnAccountThatNeverWrites()
        => Assert.False(TestAccounts.Read(Accounts, Secrets).Get("Reader").Writes);

    [Fact]
    public void ToString_IsTheName_NeverASecret()
    {
        var account = TestAccounts.Read(Accounts, Secrets).Get("Reader");

        Assert.Equal("Reader", account.ToString());
        Assert.DoesNotContain("secret", $"{account}");
    }

    [Fact]
    public void Get_AnUnknownName_FailsNamingTheKnownOnes()
    {
        var error = Assert.Throws<TestAccountException>(() => TestAccounts.Read(Accounts, Secrets).Get("Nobody"));

        Assert.Equal("Account Nobody is not in TestAccounts.md. Known: NormalUser, Reader, Admin.", error.Message);
    }

    [Fact]
    public void Get_WithNoLocalFile_FailsSayingItIsMissing()
    {
        var error = Assert.Throws<TestAccountException>(() => TestAccounts.Read(Accounts, null).Get("Reader"));

        Assert.Contains("TestAccounts.local.md is missing", error.Message);
    }

    [Fact]
    public void Get_WithNoPassword_FailsNamingTheAccount()
    {
        var error = Assert.Throws<TestAccountException>(() => TestAccounts.Read(Accounts, Secrets).Get("Admin"));

        Assert.Equal("Account Admin has no password in TestAccounts.local.md.", error.Message);
    }

    [Fact]
    public void Get_WithNoRowInTheLocalFile_FailsNamingTheAccount()
    {
        const string secrets = """
            ## Accounts

            | Name | Username | Password |
            | --- | --- | --- |
            | Reader | r | s |
            """;

        var error = Assert.Throws<TestAccountException>(() => TestAccounts.Read(Accounts, secrets).Get("NormalUser"));

        Assert.Equal("Account NormalUser has no row in TestAccounts.local.md.", error.Message);
    }

    [Fact]
    public void Read_WithNoAccountsTable_Fails()
        => Assert.Throws<TestAccountException>(() => TestAccounts.Read("# Nothing here", Secrets));

    [Fact]
    public void FindFrom_ReadsTheFilesAboveTheFolder()
    {
        var root = Directory.CreateTempSubdirectory("bruinet-accounts-");
        try
        {
            File.WriteAllText(Path.Combine(root.FullName, TestAccounts.FileName), Accounts);
            File.WriteAllText(Path.Combine(root.FullName, TestAccounts.SecretsFileName), Secrets);
            var below = Directory.CreateDirectory(Path.Combine(root.FullName, "bin", "Debug"));

            var accounts = TestAccounts.FindFrom(below.FullName);

            Assert.Equal(["NormalUser", "Reader", "Admin"], accounts.Names);
            Assert.Equal("reader@example.com", accounts.Get("Reader").Username);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void SplitRow_AnEscapedPipe_StaysInItsCell()
        => Assert.Equal(["a", "b|c", "d"], MarkdownTables.SplitRow(@"| a | b\|c | d |"));
}
