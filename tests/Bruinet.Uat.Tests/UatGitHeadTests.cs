using Xunit;

namespace Bruinet.Uat.Tests;

// The commit a trace records (AI9): read from .git without running git, in each layout a checkout can have.
public sealed class UatGitHeadTests : IDisposable
{
    private const string Commit = "00cbb4e1aa2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Bruinet", nameof(UatGitHeadTests), Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void ABranchRef_IsFollowed_FromAFolderBelow()
    {
        Write(".git/HEAD", "ref: refs/heads/move");
        Write(".git/refs/heads/move", Commit);

        Assert.Equal(Commit, UatScenarioTrace.GitHead(Folder("samples/Todo/tests")));
    }

    [Fact]
    public void APackedRef_ADetachedHead_AndAWorktreesGitFile_AreRead()
    {
        Write("repo/.git/HEAD", "ref: refs/heads/move");
        Write("repo/.git/packed-refs", $"# pack-refs with: peeled\n{Commit} refs/heads/move\n");
        Assert.Equal(Commit, UatScenarioTrace.GitHead(Folder("repo")));

        Write("detached/.git/HEAD", Commit);
        Assert.Equal(Commit, UatScenarioTrace.GitHead(Folder("detached")));

        // A worktree (or a submodule): .git is a file naming its git folder, whose commondir holds the refs.
        Write("repo/.git/worktrees/play/HEAD", "ref: refs/heads/move");
        Write("repo/.git/worktrees/play/commondir", "../..");
        Write("play/.git", $"gitdir: {Path.Combine(_root, "repo", ".git", "worktrees", "play")}");
        Assert.Equal(Commit, UatScenarioTrace.GitHead(Folder("play")));
    }

    [Fact]
    public void AReftableRepository_OrSomethingNotACommit_GivesNone()
    {
        Write("reftable/.git/HEAD", "ref: refs/heads/.invalid");
        Assert.Null(UatScenarioTrace.GitHead(Folder("reftable")));

        Write("odd/.git/HEAD", "--output=x");
        Assert.Null(UatScenarioTrace.GitHead(Folder("odd")));
    }

    private string Folder(string relative) => Directory.CreateDirectory(Path.Combine(_root, relative)).FullName;

    private void Write(string relative, string text)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
