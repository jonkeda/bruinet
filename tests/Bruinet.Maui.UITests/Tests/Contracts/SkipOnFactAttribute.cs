using Bruinet.Core.Testing;

namespace Bruinet.Maui.UITests.Tests.Contracts;

/// <summary>The MAUI heads a contract binding runs on.</summary>
[Flags]
public enum ContractHeads
{
    /// <summary>MauiHeadless (<c>BRUINET_HEAD=headless</c>).</summary>
    Headless = 1,

    /// <summary>The Windows app through FlaUI and the UI Automation bridge.</summary>
    Windows = 2,

    /// <summary>Android or iOS through Appium (the mobile suite).</summary>
    Mobile = 4,
}

/// <summary>
/// A contract test a head cannot keep: skipped on <see cref="ContractHeads"/> with why, and run on the others, so
/// each binding runs on every head and says where it differs (review 04, finding 5). The heads are told apart as
/// <see cref="WindowsOnlyFactAttribute"/> tells them: the Windows head defines <c>WINDOWS</c>.
/// </summary>
public sealed class SkipOnFactAttribute : FactAttribute
{
    /// <param name="heads">Where the test cannot run.</param>
    /// <param name="reason">Why, shown as the skip.</param>
    public SkipOnFactAttribute(ContractHeads heads, string reason)
    {
        if ((heads & Current) != 0)
        {
            Skip = $"Not on {Current}: {reason}";
        }
    }

    /// <summary>The head of this run.</summary>
    public static ContractHeads Current =>
        MauiHeads.IsHeadless ? ContractHeads.Headless
#if WINDOWS
        : ContractHeads.Windows;
#else
        : ContractHeads.Mobile;
#endif
}
