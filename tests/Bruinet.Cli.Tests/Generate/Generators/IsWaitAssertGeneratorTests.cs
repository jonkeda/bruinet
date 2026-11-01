using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Bruinet.Generator.Analysis;
using Bruinet.Generator.Generators;
using Bruinet.Generator.Models;

namespace Bruinet.Generator.Tests.Generators;

/// <summary>
/// Tests for <see cref="IsWaitAssertGenerator"/>. Validates that Is/Wait/Assert
/// trios are correctly generated from Is*Core state queries.
/// </summary>
public class IsWaitAssertGeneratorTests
{
    private readonly IsWaitAssertGenerator _generator = new();

    private static MethodDeclarationSyntax FirstMethod(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = (CompilationUnitSyntax)tree.GetRoot();
        return root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
    }

    private static ControlObjectContext ContextFor(string code, string? className = null)
    {
        var analyzer = new ControlObjectAnalyzer();
        var (classDecl, root) = analyzer.FindTarget(code, className);
        return analyzer.BuildContext(classDecl!, root);
    }

    #region Matching Tests

    [Fact]
    public void Matches_WithIsVisibleCore_ReturnsTrue()
    {
        var method = FirstMethod(@"
public class Test {
    protected virtual bool? IsVisibleCore(IMauiElement? element) => element?.Visible;
}");

        Assert.True(_generator.Matches(method));
    }

    [Fact]
    public void Matches_WithIsEnabledCore_ReturnsTrue()
    {
        var method = FirstMethod(@"
public class Test {
    protected virtual bool? IsEnabledCore(IMauiElement? element) => element?.Enabled;
}");

        Assert.True(_generator.Matches(method));
    }

    [Fact]
    public void Matches_WithIsExistsCore_ReturnsTrue()
    {
        var method = FirstMethod(@"
public class Test {
    protected virtual bool? IsExistsCore(IMauiElement? element) => element != null;
}");

        Assert.True(_generator.Matches(method));
    }

    [Fact]
    public void Matches_WithSendKeysCore_ReturnsFalse()
    {
        // Wrong prefix (no "Is"), wrong return type (void)
        var method = FirstMethod(@"
public class Test {
    protected virtual void SendKeysCore(IMauiElement element, string keys) => element.SendKeys(keys);
}");

        Assert.False(_generator.Matches(method));
    }

    [Fact]
    public void Matches_WithWrongPrefix_ReturnsFalse()
    {
        var method = FirstMethod(@"
public class Test {
    protected virtual bool? HasVisibleCore(IMauiElement? element) => element?.Visible;
}");

        Assert.False(_generator.Matches(method));
    }

    [Fact]
    public void Matches_WithGetTextCore_ReturnsTrue()
    {
        var method = FirstMethod(@"
public class Test {
    protected virtual string? GetTextCore(IMauiElement element) => element.Text;
}");

        Assert.True(_generator.Matches(method));
    }

    [Fact]
    public void Matches_WithGetVoidCore_ReturnsFalse()
    {
        // Get*Core must return a value; void is not a query.
        var method = FirstMethod(@"
public class Test {
    protected virtual void GetTextCore(IMauiElement element) { }
}");

        Assert.False(_generator.Matches(method));
    }

    [Fact]
    public void Matches_WithoutVirtualModifier_ReturnsFalse()
    {
        var method = FirstMethod(@"
public class Test {
    protected bool? IsVisibleCore(IMauiElement? element) => element?.Visible;
}");

        Assert.False(_generator.Matches(method));
    }

    #endregion

    #region Extraction Tests

    [Fact]
    public void Extract_WithIsVisibleCore_ExtractsPropertyNameVisible()
    {
        var method = FirstMethod(@"
public class Test {
    protected virtual bool? IsVisibleCore(IMauiElement? element) => element?.Visible;
}");

        var info = _generator.Extract(method);

        Assert.Equal("IsVisibleCore", info.MethodName);
        Assert.Equal("Visible", info.PublicMethodName);
        Assert.Equal("bool?", info.ReturnType);
    }

    [Fact]
    public void Extract_WithIsEnabledCore_ExtractsPropertyNameEnabled()
    {
        var method = FirstMethod(@"
public class Test {
    protected virtual bool? IsEnabledCore(IMauiElement? element) => element?.Enabled;
}");

        var info = _generator.Extract(method);

        Assert.Equal("IsEnabledCore", info.MethodName);
        Assert.Equal("Enabled", info.PublicMethodName);
    }

    [Fact]
    public void Extract_WithIsExistsCore_ExtractsPropertyNameExists()
    {
        var method = FirstMethod(@"
public class Test {
    protected virtual bool? IsExistsCore(IMauiElement? element) => element != null;
}");

        var info = _generator.Extract(method);

        Assert.Equal("IsExistsCore", info.MethodName);
        Assert.Equal("Exists", info.PublicMethodName);
    }

    [Fact]
    public void Extract_WithGetTextCore_ExtractsPropertyNameAndReturnType()
    {
        var method = FirstMethod(@"
public class Test {
    protected virtual string? GetTextCore(IMauiElement element) => element.Text;
}");

        var info = _generator.Extract(method);

        Assert.Equal("GetTextCore", info.MethodName);
        Assert.Equal("Text", info.PublicMethodName);
        Assert.Equal("string?", info.ReturnType);
        Assert.Empty(info.Parameters);
    }

    [Fact]
    public void Extract_WithGetAttributeCore_CapturesExtraParameters()
    {
        var method = FirstMethod(@"
public class Test {
    protected virtual string? GetAttributeCore(IMauiElement element, string? name) => null;
}");

        var info = _generator.Extract(method);

        Assert.Equal("Attribute", info.PublicMethodName);
        Assert.Single(info.Parameters);
        Assert.Equal("string?", info.Parameters[0].TypeName);
        Assert.Equal("name", info.Parameters[0].ParameterName);
    }

    #endregion

    #region Generation Tests

    [Fact]
    public void Generate_WithIsVisibleCore_GeneratesThreeMethods()
    {
        var code = @"
public class Test<TScope> {
    protected virtual bool? IsVisibleCore(IMauiElement? element) => element?.Visible;
}";
        var method = FirstMethod(code);
        var info = _generator.Extract(method);
        var context = ContextFor(code, "Test");

        var generated = _generator.Generate(info, context);

        Assert.Contains("public bool? IsVisible()", generated);
        Assert.Contains("public bool WaitVisible(bool? expected = true, int? timeoutMs = null)", generated);
        Assert.Contains("public TScope AssertVisible(bool? expected = true, string? message = null, int? timeoutMs = null)", generated);
    }

    [Fact]
    public void Generate_IsMethod_ObservesStateImmediately()
    {
        var code = @"
public class Test {
    protected virtual bool? IsVisibleCore(IMauiElement? element) => element?.Visible;
}";
        var method = FirstMethod(code);
        var info = _generator.Extract(method);
        var context = ContextFor(code, "Test");

        var generated = _generator.Generate(info, context);

        Assert.Contains(
            "return IsVisibleCore(TryFindElement()) == true;",
            generated);
        Assert.DoesNotContain("RunWait", generated[..generated.IndexOf("public bool WaitVisible", StringComparison.Ordinal)]);
    }

    [Fact]
    public void Generate_AssertMethod_UsesRunAssertWithElement()
    {
        var code = @"
public class Test {
    protected virtual bool? IsVisibleCore(IMauiElement? element) => element?.Visible;
}";
        var method = FirstMethod(code);
        var info = _generator.Extract(method);
        var context = ContextFor(code, "Test");

        var generated = _generator.Generate(info, context);

        Assert.Contains("RunAssertWithElement(expected,", generated);
        Assert.Contains("IsVisibleCore, (actual, expected1) => (actual == expected1),", generated);
    }

    [Fact]
    public void Generate_WithGetTextCore_GeneratesGetWaitAssert()
    {
        var code = @"
public class Test<TScope> {
    protected virtual string? GetTextCore(IMauiElement element) => element.Text;
}";
        var method = FirstMethod(code);
        var info = _generator.Extract(method);
        var context = ContextFor(code, "Test");

        var generated = _generator.Generate(info, context);

        Assert.Contains("public string? GetText(int? timeoutMs = null)", generated);
        Assert.Contains("return RunGetWithElement(element => GetTextCore(element), timeoutMs);", generated);
        Assert.Contains("public bool WaitText(string? expected, int? timeoutMs = null)", generated);
        Assert.Contains("public TScope AssertText(string? expected, string? message = null, int? timeoutMs = null)", generated);
    }

    [Fact]
    public void Generate_WithGetAttributeCore_IncludesExtraParameters()
    {
        var code = @"
public class Test {
    protected virtual string? GetAttributeCore(IMauiElement element, string? name) => null;
}";
        var method = FirstMethod(code);
        var info = _generator.Extract(method);
        var context = ContextFor(code, "Test");

        var generated = _generator.Generate(info, context);

        Assert.Contains("public string? GetAttribute(string? name, int? timeoutMs = null)", generated);
        Assert.Contains("GetAttributeCore(element, name)", generated);
        Assert.Contains("public bool WaitAttribute(string? name, string? expected, int? timeoutMs = null)", generated);
    }

    #endregion

    #region Registered Generation Tests

    [Fact]
    public void ControlObjectGenerator_GeneratesNineStateMethodsForThreeIsCore()
    {
        // Arrange
        var code = @"namespace Bruinet.Maui.Controls;

public abstract class StateControl<TScope>
{
    protected virtual bool? IsVisibleCore(IMauiElement? element) => element?.Visible;
    protected virtual bool? IsEnabledCore(IMauiElement? element) => element?.Enabled;
    protected virtual bool? IsExistsCore(IMauiElement? element) => element != null;
}";
        var generator = new ControlObjectGenerator().Register(new IsWaitAssertGenerator());

        // Act
        var generated = generator.Generate(code, new GeneratorOptions());

        // Assert
        Assert.Contains("public bool? IsVisible()", generated);
        Assert.Contains("public bool? IsEnabled()", generated);
        Assert.Contains("public bool? IsExists()", generated);

        Assert.Contains("public bool WaitVisible(", generated);
        Assert.Contains("public bool WaitEnabled(", generated);
        Assert.Contains("public bool WaitExists(", generated);

        Assert.Contains("public TScope AssertVisible(", generated);
        Assert.Contains("public TScope AssertEnabled(", generated);
        Assert.Contains("public TScope AssertExists(", generated);
    }

    #endregion

    #region Test Data File Tests

    [Fact]
    public void TestData_ControlBaseInput_CanBeParsed()
    {
        var inputFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Input", "ControlBase.input.cs");

        var content = File.ReadAllText(inputFile);
        var tree = CSharpSyntaxTree.ParseText(content);
        var root = (CompilationUnitSyntax)tree.GetRoot();

        Assert.NotNull(root);
        var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        Assert.NotNull(classDecl);
    }

    [Fact]
    public void TestData_ControlBaseInput_ContainsSendKeysAndIsCoreMethods()
    {
        var inputFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Input", "ControlBase.input.cs");
        var content = File.ReadAllText(inputFile);
        var tree = CSharpSyntaxTree.ParseText(content);
        var root = (CompilationUnitSyntax)tree.GetRoot();
        var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();

        var methods = classDecl.Members.OfType<MethodDeclarationSyntax>().ToList();

        Assert.Contains(methods, m => m.Identifier.Text == "SendKeysCore");
        Assert.Contains(methods, m => m.Identifier.Text == "IsVisibleCore");
        Assert.Contains(methods, m => m.Identifier.Text == "IsEnabledCore");
        Assert.Contains(methods, m => m.Identifier.Text == "IsExistsCore");
    }

    [Fact]
    public void TestData_ControlBaseExpected_ContainsBothGeneratedMethods()
    {
        var expectedFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Expected", "ControlBase.expected.cs");
        var content = File.ReadAllText(expectedFile);

        // Action family
        Assert.Contains("public TScope SendKeys(string keys)", content);

        // Is/Wait/Assert family
        Assert.Contains("public bool? IsVisible()", content);
        Assert.Contains("public bool? IsEnabled()", content);
        Assert.Contains("public bool IsExists()", content);

        Assert.Contains("public bool WaitVisible(bool? expected = true, int? timeoutMs = null)", content);
        Assert.Contains("public bool WaitEnabled(bool? expected = true, int? timeoutMs = null)", content);
        Assert.Contains("public bool WaitExists(bool? expected = true, int? timeoutMs = null)", content);

        Assert.Contains("public TScope AssertVisible(bool? expected = true, string? message = null, int? timeoutMs = null)", content);
        Assert.Contains("public TScope AssertEnabled(bool? expected = true, string? message = null, int? timeoutMs = null)", content);
        Assert.Contains("public TScope AssertExists(bool? expected = true, string? message = null, int? timeoutMs = null)", content);
    }

    #endregion
}
