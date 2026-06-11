using OsintPro.UI.Models;
using OsintPro.UI.Services;
using Xunit;

namespace OsintPro.UI.Tests;

public class ArchiveManagerTests : IDisposable
{
    private readonly string _folder;
    private readonly ArchiveManager _manager;

    public ArchiveManagerTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "JustinOSINT-tests", Guid.NewGuid().ToString());
        _manager = new ArchiveManager(_folder);
    }

    [Fact]
    public void SaveDossier_TwoDifferentIds_KeepsBothFiles()
    {
        var first = new Dossier { Id = Guid.NewGuid().ToString(), FullName = "Перший" };
        var second = new Dossier { Id = Guid.NewGuid().ToString(), FullName = "Другий" };

        _manager.SaveDossier(first);
        _manager.SaveDossier(second);

        var all = _manager.GetAllDossiers();
        Assert.Equal(2, all.Count);
        Assert.Contains(all, d => d.Id == first.Id);
        Assert.Contains(all, d => d.Id == second.Id);
    }

    [Fact]
    public void SaveDossier_SameIdTwice_UpdatesSingleFile()
    {
        var dossier = new Dossier { Id = Guid.NewGuid().ToString(), FullName = "Оригінал" };
        _manager.SaveDossier(dossier);

        dossier.FullName = "Оновлено";
        _manager.SaveDossier(dossier);

        Assert.Single(_manager.GetAllDossiers());
        Assert.Equal("Оновлено", _manager.GetById(dossier.Id)!.FullName);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }
        catch { }
    }
}