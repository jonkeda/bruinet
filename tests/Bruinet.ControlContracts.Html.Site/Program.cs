using Bruinet.ControlContracts.Html.Site.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();

// What WebAppHost asks for until the site answers; the pages are /element, /click and the rest.
app.MapGet("/", () => "Control contracts");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Bruinet.ControlContracts.Html.ContractPage).Assembly);
app.Run();
