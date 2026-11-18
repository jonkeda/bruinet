# Run HTML Tests

Working directory: Bruinet root.

## Build

```powershell
dotnet build src\Bruinet.Html\Bruinet.Html.csproj -v:minimal /nr:false
dotnet build src\Bruinet.Html.Playwright\Bruinet.Html.Playwright.csproj -v:minimal /nr:false
```

## Unit Coverage

```powershell
dotnet test tests\Bruinet.Html.Tests\Bruinet.Html.Tests.csproj -v:minimal /nr:false
dotnet test tests\Bruinet.Blazor.Tests\Bruinet.Blazor.Tests.csproj -v:minimal /nr:false
```

`Bruinet.Html.Tests` holds the call-model tests: calls, scope readiness, confirmations,
collections, and the Playwright error mapping.

## Generate Controls

Html and Blazor controls are `.tpl.cs` templates and generated `.gen.cs` files, as on MAUI. After
changing a template:

```powershell
tools\Scripts\CreateHtml.Bat
```

## UI Tests

Install Playwright's Chromium once (after a build, from the Bruinet root):

```powershell
pwsh tests\Bruinet.Html.UITests\bin\Debug\net10.0\playwright.ps1 install chromium
```

Then:

```powershell
dotnet test tests\Bruinet.Html.UITests\Bruinet.Html.UITests.csproj -v:minimal /nr:false
dotnet test tests\Bruinet.Blazor.UITests\Bruinet.Blazor.UITests.csproj -v:minimal /nr:false
```

Nothing needs to be running: the tests build the Blazor sample and start it on a free port
(`Bruinet.Html.Testing.WebAppHost`). Set `BLAZOR_APP_URL` to use a sample that is already
running instead. The Todo web head has its own UI tests:
`dotnet test samples\Todo\tests\Bruinet.Samples.Todo.Web.UITests` (hermetic; add
`BRUINET_UAT_LIVE_API=1` for the live tier).

A suite that sets `AllowedHosts` routes its browser's requests, and a routed browser keeps no cache. Bruinet keeps the
app's scripts, styles and fonts for it: in the run, and in `%TEMP%/bruinet-kept-assets` between runs when the site
gives an asset an `ETag` (asked for first, with a HEAD request). `BRUINET_KEPT_ASSETS=off` keeps none on disk.

## UAT

```powershell
dotnet test tests\Bruinet.Html.UITests\Bruinet.Html.UITests.csproj -v:minimal /nr:false
dotnet test tests\Bruinet.Blazor.UITests\Bruinet.Blazor.UITests.csproj -v:minimal /nr:false
dotnet test samples\Todo\tests\Bruinet.Samples.Todo.Web.Uat -v:minimal /nr:false
```

The first two start the Blazor sample themselves. The third runs the Todo scenario files tagged
`@web` against the Todo web head, including a scenario with two devices on one backend.
