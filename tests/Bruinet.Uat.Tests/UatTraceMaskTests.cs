using Xunit;

namespace Bruinet.Uat.Tests;

// What a trace never writes (review 09, D4, R2-T20): a password field's value, a control the suite lists, and the
// quoted values of a step that names a secret.
public sealed class UatTraceMaskTests
{
    [Theory]
    [InlineData("Password")]
    [InlineData("NewPassword")]
    [InlineData("UserPin")]
    [InlineData("ApiKeyField")]
    [InlineData("AccessToken")]
    public void A_ControlNamedForASecret_IsHidden(string name)
        => Assert.True(UatTraceMask.Hides(name, typeof(FakeText), []));

    [Theory]
    [InlineData("Spinner")]
    [InlineData("ShippingAddress")]
    [InlineData("Email")]
    [InlineData("Tokenizer")]
    public void A_ControlWhoseNameOnlyContainsTheLetters_IsNotHidden(string name)
        => Assert.False(UatTraceMask.Hides(name, typeof(FakeText), []));

    [Fact]
    public void A_ControlOfAPasswordType_IsHidden_WhateverItsName()
        => Assert.True(UatTraceMask.Hides("Field1", typeof(PasswordBox<object>), []));

    [Fact]
    public void A_ControlTheSuiteLists_IsHidden()
        => Assert.True(UatTraceMask.Hides("Iban", typeof(FakeText), ["iban", "Bsn"]));

    [Theory]
    [InlineData("When I sign in with password \"hunter2\"", "When I sign in with password \"***\"")]
    [InlineData("Given my PIN is '1234'", "Given my PIN is \"***\"")]
    [InlineData("When I type \"Buy milk\" into Title", "When I type \"Buy milk\" into Title")]
    public void A_StepThatNamesASecret_HasItsQuotedValuesHidden(string text, string kept)
        => Assert.Equal(kept, UatTraceMask.Step(text));

    public sealed class FakeText
    {
    }

    public sealed class PasswordBox<TScope>
    {
    }
}
