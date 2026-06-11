# Justin OSINT (OsintPro)

Десктопний OSINT-інструмент для пошуку по відкритих джерелах (Україна): суди, борги, декларації, бізнес, соцмережі, телефони.

## Вимоги

- Windows 10+
- [.NET 10](https://dotnet.microsoft.com/download) (для збірки)
- WebView2 Runtime (зазвичай вже встановлений)

## Збірка

```powershell
dotnet publish OsintPro.UI/OsintPro.UI.csproj -c Release -r win-x64 -o release/JustinOSINT-1.0.5
dotnet build ../JustinOSINT_Launcher/JustinOSINT_Launcher/JustinOSINT_Launcher.csproj -c Release
.\scripts\pack-release.ps1 -Version "1.0.5"
```

## Тести

```powershell
dotnet test OsintPro.UI.Tests/OsintPro.UI.Tests.csproj -c Release
```

## Релізи

Завантаження: [GitHub Releases](https://github.com/Normalnuy/OsintPro/releases)

Історія змін: [CHANGELOG.md](CHANGELOG.md)

## Архів досьє

`%UserProfile%\Documents\JustinOSINT\Archives\`