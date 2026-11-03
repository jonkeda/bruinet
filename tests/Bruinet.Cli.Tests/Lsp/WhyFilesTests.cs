using Xunit;

namespace Bruinet.Bdd.LanguageServer.Tests;

// The files changed since a green run, sorted for why (AI9): by the pages and the id-like controls the steps drove.
public sealed class WhyFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Bruinet", nameof(WhyFilesTests), Guid.NewGuid().ToString("N"));

    public WhyFilesTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void AFileNamedForAPage_OrQuotingAControlsId_TouchesTheStepThatDroveIt()
    {
        Write("src/ViewModels/TodoDetailViewModel.cs", "class TodoDetailViewModel { }");
        Write("src/Pages/TodoListPage.xaml", """<CollectionView AutomationId="TodoList_Items" />""");
        Write("src/Tools/Helper.cs", """var label = "Delete";""");
        Write("README.md", "Todo");

        var (touching, earlier, other) = WhyFiles.Sort(
            ["src/Pages/TodoListPage.xaml", "src/ViewModels/TodoDetailViewModel.cs", "src/Tools/Helper.cs", "README.md"],
            file => Path.Combine(_root, file),
            [("Todo List", true), ("TodoList_Items", false)],
            [("TodoDetailPage", true), ("Delete", false)]);

        Assert.Equal(["src/Pages/TodoListPage.xaml"], touching);
        Assert.Equal(["src/ViewModels/TodoDetailViewModel.cs"], earlier);
        Assert.Equal(["src/Tools/Helper.cs", "README.md"], other);
    }

    [Fact]
    public void AChangeOfCommentsOnly_IsSetApart_ACodeChangeIsNot()
    {
        var diff = """
            diff --git a/src/TodoDetailViewModel.cs b/src/TodoDetailViewModel.cs
            index 1..2 100644
            --- a/src/TodoDetailViewModel.cs
            +++ b/src/TodoDetailViewModel.cs
            @@ -140,0 +141 @@
            +    // Shows a todo, or none.
            diff --git a/src/PlatformServices.cs b/src/PlatformServices.cs
            index 3..4 100644
            --- a/src/PlatformServices.cs
            +++ b/src/PlatformServices.cs
            @@ -23 +23 @@
            -        => CurrentPage.DisplayAlertAsync(title, message, accept, cancel);
            +        => CurrentPage.DisplayAlertAsync(title, message, cancel, accept);
            """;

        Assert.Equal(["src/TodoDetailViewModel.cs"], WhyFiles.CommentsOnlyIn(diff));
    }

    [Theory]
    [InlineData("TodoDetail_Delete", true)]
    [InlineData("counter-title", true)]
    [InlineData("SaveButton", true)]
    [InlineData("Delete", false)]
    public void AnIdLikeName_IsOneNoCodeQuotesByChance(string name, bool idLike) => Assert.Equal(idLike, WhyFiles.IdLike(name));

    private void Write(string relative, string text)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
