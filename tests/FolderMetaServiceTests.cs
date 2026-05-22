using System.Text.Json;

namespace FileManager.Tests;

public class FolderMetaServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FolderMetaService _service;

    public FolderMetaServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"fms-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _service = new FolderMetaService(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void Constructor_CreatesDataDirectory()
    {
        var dir = Path.Combine(_tempDir, "data");
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public void Constructor_CreatesEmptyJsonFile()
    {
        var file = Path.Combine(_tempDir, "data", "folder-meta.json");
        Assert.True(File.Exists(file));
        var json = File.ReadAllText(file);
        Assert.Contains("categories", json);
        Assert.Contains("tags", json);
        Assert.Contains("folderAssignments", json);
    }

    [Fact]
    public void AddCategory_CreatesCategory()
    {
        var cat = _service.AddCategory("工作", "#4CAF50");
        Assert.Equal("工作", cat.Name);
        Assert.Equal("#4CAF50", cat.Color);
        Assert.StartsWith("cat-", cat.Id);
        Assert.Single(_service.GetCategories());
    }

    [Fact]
    public void AddCategory_DuplicateName_Throws()
    {
        _service.AddCategory("工作", "#4CAF50");
        Assert.Throws<ArgumentException>(() => _service.AddCategory("工作", "#FF0000"));
    }

    [Fact]
    public void UpdateCategory_ChangesName()
    {
        var cat = _service.AddCategory("工作", "#4CAF50");
        _service.UpdateCategory(cat.Id, "公司", "#FF0000");
        var result = _service.GetCategories().First();
        Assert.Equal("公司", result.Name);
        Assert.Equal("#FF0000", result.Color);
    }

    [Fact]
    public void DeleteCategory_RemovesCategory()
    {
        var cat = _service.AddCategory("工作", "#4CAF50");
        _service.DeleteCategory(cat.Id);
        Assert.Empty(_service.GetCategories());
    }

    [Fact]
    public void DeleteCategory_RemovesAssignments()
    {
        var cat = _service.AddCategory("工作", "#4CAF50");
        var testDir = Path.Combine(_tempDir, "MyFolder");
        Directory.CreateDirectory(testDir);
        _service.AssignCategory(testDir, cat.Id);
        _service.DeleteCategory(cat.Id);
        var meta = _service.GetFolderMeta(testDir);
        Assert.True(meta == null || !meta.CategoryIds.Contains(cat.Id));
    }

    [Fact]
    public void AddTag_CreatesTag()
    {
        var tag = _service.AddTag("重要", "#F44336");
        Assert.Equal("重要", tag.Name);
        Assert.Equal("#F44336", tag.Color);
        Assert.StartsWith("tag-", tag.Id);
    }

    [Fact]
    public void AddTag_DuplicateName_Throws()
    {
        _service.AddTag("重要", "#F44336");
        Assert.Throws<ArgumentException>(() => _service.AddTag("重要", "#00FF00"));
    }

    [Fact]
    public void DeleteTag_RemovesTagAssignments()
    {
        var tag = _service.AddTag("重要", "#F44336");
        var testDir = Path.Combine(_tempDir, "MyFolder");
        Directory.CreateDirectory(testDir);
        _service.AddFolderTag(testDir, tag.Id);
        _service.DeleteTag(tag.Id);
        var meta = _service.GetFolderMeta(testDir);
        Assert.True(meta == null || !meta.TagIds.Contains(tag.Id));
    }

    [Fact]
    public void AssignCategory_AddsAssociation()
    {
        var cat = _service.AddCategory("工作", "#4CAF50");
        var testDir = Path.Combine(_tempDir, "FolderA");
        Directory.CreateDirectory(testDir);
        _service.AssignCategory(testDir, cat.Id);
        Assert.True(_service.FolderMatchesCategory(testDir, cat.Id));
    }

    [Fact]
    public void RemoveCategory_RemovesAssociation()
    {
        var cat = _service.AddCategory("工作", "#4CAF50");
        var testDir = Path.Combine(_tempDir, "FolderA");
        Directory.CreateDirectory(testDir);
        _service.AssignCategory(testDir, cat.Id);
        _service.RemoveCategory(testDir, cat.Id);
        Assert.False(_service.FolderMatchesCategory(testDir, cat.Id));
    }

    [Fact]
    public void AddFolderTag_And_RemoveFolderTag()
    {
        var tag = _service.AddTag("重要", "#F44336");
        var testDir = Path.Combine(_tempDir, "FolderA");
        Directory.CreateDirectory(testDir);
        _service.AddFolderTag(testDir, tag.Id);
        Assert.True(_service.FolderMatchesTag(testDir, tag.Id));
        _service.RemoveFolderTag(testDir, tag.Id);
        Assert.False(_service.FolderMatchesTag(testDir, tag.Id));
    }

    [Fact]
    public void RemoveCategory_CleansUpEmptyAssignment()
    {
        var cat = _service.AddCategory("工作", "#4CAF50");
        var testDir = Path.Combine(_tempDir, "FolderA");
        Directory.CreateDirectory(testDir);
        _service.AssignCategory(testDir, cat.Id);
        _service.RemoveCategory(testDir, cat.Id);
        Assert.Null(_service.GetFolderMeta(testDir));
    }

    [Fact]
    public void FolderMatchesAnyFilter_NoFilter_ReturnsTrue()
    {
        var testDir = Path.Combine(_tempDir, "FolderA");
        Directory.CreateDirectory(testDir);
        Assert.True(_service.FolderMatchesAnyFilter(testDir, null, null));
    }

    [Fact]
    public void FolderMatchesAnyFilter_WithCategoryFilter()
    {
        var cat = _service.AddCategory("工作", "#4CAF50");
        var testDir = Path.Combine(_tempDir, "FolderA");
        Directory.CreateDirectory(testDir);
        Assert.False(_service.FolderMatchesAnyFilter(testDir, cat.Id, null));
        _service.AssignCategory(testDir, cat.Id);
        Assert.True(_service.FolderMatchesAnyFilter(testDir, cat.Id, null));
    }

    [Fact]
    public void FolderMatchesAnyFilter_WithBothFilters()
    {
        var cat = _service.AddCategory("工作", "#4CAF50");
        var tag = _service.AddTag("重要", "#F44336");
        var testDir = Path.Combine(_tempDir, "FolderA");
        Directory.CreateDirectory(testDir);
        _service.AssignCategory(testDir, cat.Id);
        Assert.False(_service.FolderMatchesAnyFilter(testDir, cat.Id, tag.Id));
        _service.AddFolderTag(testDir, tag.Id);
        Assert.True(_service.FolderMatchesAnyFilter(testDir, cat.Id, tag.Id));
    }

    [Fact]
    public void Persistence_DataSurvivesReload()
    {
        var cat = _service.AddCategory("工作", "#4CAF50");
        var tag = _service.AddTag("重要", "#F44336");
        var testDir = Path.Combine(_tempDir, "FolderA");
        Directory.CreateDirectory(testDir);
        _service.AssignCategory(testDir, cat.Id);
        _service.AddFolderTag(testDir, tag.Id);

        var service2 = new FolderMetaService(_tempDir);
        Assert.Single(service2.GetCategories());
        Assert.Single(service2.GetTags());
        Assert.Equal("工作", service2.GetCategories().First().Name);
        Assert.True(service2.FolderMatchesCategory(testDir, cat.Id));
        Assert.True(service2.FolderMatchesTag(testDir, tag.Id));
    }

    [Fact]
    public void Persistence_CorruptJson_CreatesBackup()
    {
        var file = Path.Combine(_tempDir, "data", "folder-meta.json");
        File.WriteAllText(file, "{ corrupt json !!!");
        var service = new FolderMetaService(_tempDir);
        Assert.Empty(service.GetCategories());
        Assert.True(File.Exists(file + ".bak"));
    }

    [Fact]
    public void PathNormalization_TrailingSlashMatch()
    {
        var cat = _service.AddCategory("工作", "#4CAF50");
        var testDir = Path.Combine(_tempDir, "FolderA");
        Directory.CreateDirectory(testDir);
        _service.AssignCategory(testDir + "\\", cat.Id);
        Assert.True(_service.FolderMatchesCategory(testDir, cat.Id));
    }
}
