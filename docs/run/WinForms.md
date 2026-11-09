# Run WinForms Tests

Working directory: Bruinet root.

## Build

```powershell
dotnet build src\Bruinet.WinForms\Bruinet.WinForms.csproj -f net10.0-windows -v:minimal /nr:false
dotnet build samples\Bruinet.Samples.WinForms.App\Bruinet.Samples.WinForms.App.csproj -f net10.0-windows -v:minimal /nr:false
```

## Unit Tests

```powershell
dotnet test tests\Bruinet.WinForms.Tests\Bruinet.WinForms.Tests.csproj -v:minimal /nr:false
```

## UI Tests

```powershell
dotnet test tests\Bruinet.WinForms.UITests\Bruinet.WinForms.UITests.csproj -v:minimal /nr:false
```
