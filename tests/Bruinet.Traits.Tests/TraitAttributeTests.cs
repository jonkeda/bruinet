using System.Reflection;
using Bruinet.Traits;
using Xunit.Sdk;

namespace Bruinet.Traits.Tests;

public class TraitAttributeTests
{
    [Theory]
    [InlineData(nameof(Holder.Unit), "Pyramid=Unit;Category=Unit")]
    [InlineData(nameof(Holder.Integration), "Pyramid=Integration;Category=Integration")]
    [InlineData(nameof(Holder.Ui), "Pyramid=UI;Pyramid=Hermetic UI")]
    [InlineData(nameof(Holder.HermeticUi), "Pyramid=UI;Pyramid=Hermetic UI")]
    [InlineData(nameof(Holder.Architecture), "Category=Architecture")]
    [InlineData(nameof(Holder.Review), "Category=Review")]
    [InlineData(nameof(Holder.Covers), "Covers=CMN-CLIENTS-T04")]
    [InlineData(nameof(Holder.Head), "Head=Android")]
    [InlineData(nameof(Holder.Live), "Pyramid=Live")]
    [InlineData(nameof(Holder.Video), "Pyramid=Video;Category=Video")]
    [InlineData(nameof(Holder.Smoke), "Category=Smoke")]
    [InlineData(nameof(Holder.Uat), "Category=UAT")]
    [InlineData(nameof(Holder.Contract), "Category=Contract")]
    [InlineData(nameof(Holder.Module), "Module=Contacts")]
    [InlineData(nameof(Holder.Feature), "Feature=Checkout")]
    [InlineData(nameof(Holder.Component), "Component=Favorites")]
    [InlineData(nameof(Holder.CommonArea), "CommonArea=Sync")]
    [InlineData(nameof(Holder.Journey), "Journey=CTS.02.1")]
    public void Discoverer_ReadsTheTraitsXunitReports(string member, string expected)
    {
        var data = Assert.Single(typeof(Holder).GetMethod(member)!.GetCustomAttributesData());
        var traits = new BruinetTraitDiscoverer().GetTraits(Reflector.Wrap(data));

        Assert.Equal(expected, string.Join(";", traits.Select(t => $"{t.Key}={t.Value}")));
    }

    [Fact]
    public void NamedAttributes_RejectAnEmptyName()
        => Assert.Throws<ArgumentException>(() => new ModuleAttribute(" "));

    [Fact]
    public void EveryAttribute_NamesTheDiscovererXunitLooksFor()
    {
        var attributes = typeof(BruinetTraitAttribute).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(BruinetTraitAttribute)));

        Assert.All(attributes, t =>
        {
            var discoverer = Assert.Single(t.GetCustomAttributesData(), a => a.AttributeType == typeof(TraitDiscovererAttribute));
            Assert.Equal(typeof(BruinetTraitDiscoverer).FullName, discoverer.ConstructorArguments[0].Value);
            Assert.Equal(typeof(BruinetTraitDiscoverer).Assembly.GetName().Name, discoverer.ConstructorArguments[1].Value);
        });
    }

    private static class Holder
    {
        [Unit] public static void Unit() { }
        [Integration] public static void Integration() { }
        [Ui] public static void Ui() { }
        [HermeticUi] public static void HermeticUi() { }
        [Architecture] public static void Architecture() { }
        [Review] public static void Review() { }
        [Covers("CMN-CLIENTS-T04")] public static void Covers() { }
        [Head("Android")] public static void Head() { }
        [Live] public static void Live() { }
        [Video] public static void Video() { }
        [Smoke] public static void Smoke() { }
        [Uat] public static void Uat() { }
        [Contract] public static void Contract() { }
        [Module("Contacts")] public static void Module() { }
        [Feature("Checkout")] public static void Feature() { }
        [Component("Favorites")] public static void Component() { }
        [CommonArea("Sync")] public static void CommonArea() { }
        [Journey("CTS.02.1")] public static void Journey() { }
    }
}
