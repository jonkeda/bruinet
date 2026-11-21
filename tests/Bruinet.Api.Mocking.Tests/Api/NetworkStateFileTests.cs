namespace Bruinet.Api.Mocking.Tests.Api;

public sealed class NetworkStateFileTests
{
    [Fact]
    public void Create_WritesOnline()
    {
        using var file = NetworkStateFile.Create();

        Assert.Equal(NetworkStateFile.Online, File.ReadAllText(file.Path));
        Assert.True(file.IsOnline);
    }

    [Fact]
    public void SetOffline_ThenSetOnline_RewritesTheFile()
    {
        using var file = NetworkStateFile.Create();

        file.SetOffline();
        Assert.Equal(NetworkStateFile.Offline, File.ReadAllText(file.Path));
        Assert.False(file.IsOnline);

        file.SetOnline();
        Assert.True(file.IsOnline);
    }

    [Fact]
    public void Create_AtAGivenPath_MakesTheFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "Bruinet", "tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "net.txt");

        using (var file = NetworkStateFile.Create(path))
        {
            Assert.Equal(path, file.Path);
            Assert.True(File.Exists(path));
        }

        Directory.Delete(folder);
    }

    [Fact]
    public void Dispose_DeletesTheFile_AndLaterWritesThrow()
    {
        var file = NetworkStateFile.Create();
        file.Dispose();

        Assert.False(File.Exists(file.Path));
        Assert.Throws<ObjectDisposedException>(file.SetOffline);
    }
}
