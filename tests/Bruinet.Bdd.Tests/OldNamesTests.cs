using Xunit;

namespace Bruinet.Bdd.Tests;

/// <summary>The names before AD-040 are still read, one release: the config's old file name and discovery key.</summary>
public sealed class OldNamesTests
{
    [Fact]
    public void A_folder_with_only_the_old_config_name_finds_it_and_the_new_name_wins_when_both_are_there()
    {
        var folder = Directory.CreateTempSubdirectory("old-names").FullName;
        try
        {
            Assert.Equal(Path.Combine(folder, BddFileFormat.ConfigFileName), BddFileFormat.FindConfig(folder));
            Assert.False(BddFileFormat.HasConfig(folder));

            File.WriteAllText(Path.Combine(folder, BddFileFormat.OldConfigFileName), "# UAT Config\n");
            Assert.Equal(Path.Combine(folder, BddFileFormat.OldConfigFileName), BddFileFormat.FindConfig(folder));
            Assert.True(BddFileFormat.HasConfig(folder));

            File.WriteAllText(Path.Combine(folder, BddFileFormat.ConfigFileName), "# BDD Config\n");
            Assert.Equal(Path.Combine(folder, BddFileFormat.ConfigFileName), BddFileFormat.FindConfig(folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Theory]
    [InlineData("RequireExplicitBddAttributes")]
    [InlineData("RequireExplicitUatAttributes")]
    public void The_explicit_attributes_switch_is_read_by_its_new_and_its_old_key(string key)
    {
        var config = BddConfigParser.Parse($"""
            # BDD Config

            ## Discovery

            | Field | Value |
            | --- | --- |
            | {key} | true |
            """);

        Assert.True(config.Discovery.RequireExplicitUatAttributes);
    }
}
