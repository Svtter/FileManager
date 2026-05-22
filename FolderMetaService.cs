using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileManager;

public class FolderMetaService
{
    private readonly string _dataDir;
    private readonly string _filePath;

    private FolderMetaData _data;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FolderMetaService(string appDir)
    {
        _dataDir = Path.Combine(appDir, "data");
        _filePath = Path.Combine(_dataDir, "folder-meta.json");
        Directory.CreateDirectory(_dataDir);
        _data = Load();
    }

    public IReadOnlyList<Category> GetCategories() => _data.Categories.AsReadOnly();
    public IReadOnlyList<Tag> GetTags() => _data.Tags.AsReadOnly();

    public Category AddCategory(string name, string color)
    {
        if (_data.Categories.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("分类名称已存在");
        var cat = new Category { Id = $"cat-{Guid.NewGuid():N}", Name = name, Color = color };
        _data.Categories.Add(cat);
        Save();
        return cat;
    }

    public void UpdateCategory(string id, string name, string color)
    {
        var cat = _data.Categories.FirstOrDefault(c => c.Id == id) ?? throw new KeyNotFoundException("分类不存在");
        if (_data.Categories.Any(c => c.Id != id && c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("分类名称已存在");
        cat.Name = name;
        cat.Color = color;
        Save();
    }

    public void DeleteCategory(string id)
    {
        _data.Categories.RemoveAll(c => c.Id == id);
        foreach (var assignment in _data.FolderAssignments.Values)
            assignment.CategoryIds.Remove(id);
        Save();
    }

    public Tag AddTag(string name, string color)
    {
        if (_data.Tags.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("标签名称已存在");
        var tag = new Tag { Id = $"tag-{Guid.NewGuid():N}", Name = name, Color = color };
        _data.Tags.Add(tag);
        Save();
        return tag;
    }

    public void UpdateTag(string id, string name, string color)
    {
        var tag = _data.Tags.FirstOrDefault(t => t.Id == id) ?? throw new KeyNotFoundException("标签不存在");
        if (_data.Tags.Any(t => t.Id != id && t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("标签名称已存在");
        tag.Name = name;
        tag.Color = color;
        Save();
    }

    public void DeleteTag(string id)
    {
        _data.Tags.RemoveAll(t => t.Id == id);
        foreach (var assignment in _data.FolderAssignments.Values)
            assignment.TagIds.Remove(id);
        Save();
    }

    public FolderAssignment? GetFolderMeta(string folderPath)
    {
        var normalized = NormalizePath(folderPath);
        return _data.FolderAssignments.TryGetValue(normalized, out var assignment) ? assignment : null;
    }

    public void AssignCategory(string folderPath, string categoryId)
    {
        var normalized = NormalizePath(folderPath);
        if (!_data.FolderAssignments.TryGetValue(normalized, out var assignment))
        {
            assignment = new FolderAssignment();
            _data.FolderAssignments[normalized] = assignment;
        }
        if (!assignment.CategoryIds.Contains(categoryId))
            assignment.CategoryIds.Add(categoryId);
        Save();
    }

    public void RemoveCategory(string folderPath, string categoryId)
    {
        var normalized = NormalizePath(folderPath);
        if (_data.FolderAssignments.TryGetValue(normalized, out var assignment))
        {
            assignment.CategoryIds.Remove(categoryId);
            if (assignment.CategoryIds.Count == 0 && assignment.TagIds.Count == 0)
                _data.FolderAssignments.Remove(normalized);
            Save();
        }
    }

    public void AddFolderTag(string folderPath, string tagId)
    {
        var normalized = NormalizePath(folderPath);
        if (!_data.FolderAssignments.TryGetValue(normalized, out var assignment))
        {
            assignment = new FolderAssignment();
            _data.FolderAssignments[normalized] = assignment;
        }
        if (!assignment.TagIds.Contains(tagId))
            assignment.TagIds.Add(tagId);
        Save();
    }

    public void RemoveFolderTag(string folderPath, string tagId)
    {
        var normalized = NormalizePath(folderPath);
        if (_data.FolderAssignments.TryGetValue(normalized, out var assignment))
        {
            assignment.TagIds.Remove(tagId);
            if (assignment.CategoryIds.Count == 0 && assignment.TagIds.Count == 0)
                _data.FolderAssignments.Remove(normalized);
            Save();
        }
    }

    public bool FolderMatchesCategory(string folderPath, string categoryId)
    {
        var meta = GetFolderMeta(folderPath);
        return meta != null && meta.CategoryIds.Contains(categoryId);
    }

    public bool FolderMatchesTag(string folderPath, string tagId)
    {
        var meta = GetFolderMeta(folderPath);
        return meta != null && meta.TagIds.Contains(tagId);
    }

    public bool FolderMatchesAnyFilter(string folderPath, string? categoryFilter, string? tagFilter)
    {
        if (categoryFilter == null && tagFilter == null) return true;
        var meta = GetFolderMeta(folderPath);
        if (meta == null) return false;
        bool catMatch = categoryFilter == null || meta.CategoryIds.Contains(categoryFilter);
        bool tagMatch = tagFilter == null || meta.TagIds.Contains(tagFilter);
        return catMatch && tagMatch;
    }

    private static string NormalizePath(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
    }

    private FolderMetaData Load()
    {
        if (!File.Exists(_filePath))
        {
            var empty = new FolderMetaData();
            File.WriteAllText(_filePath, JsonSerializer.Serialize(empty, JsonOptions));
            return empty;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<FolderMetaData>(json, JsonOptions) ?? new FolderMetaData();
        }
        catch (JsonException)
        {
            var bakPath = _filePath + ".bak";
            try { if (File.Exists(_filePath)) File.Move(_filePath, bakPath, overwrite: true); } catch { }
            return new FolderMetaData();
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_data, JsonOptions);
        File.WriteAllText(_filePath, json);
    }
}

public class FolderMetaData
{
    [JsonPropertyName("categories")]
    public List<Category> Categories { get; set; } = new();

    [JsonPropertyName("tags")]
    public List<Tag> Tags { get; set; } = new();

    [JsonPropertyName("folderAssignments")]
    public Dictionary<string, FolderAssignment> FolderAssignments { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class Category
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#4CAF50";
}

public class Tag
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#F44336";
}

public class FolderAssignment
{
    [JsonPropertyName("categoryIds")]
    public List<string> CategoryIds { get; set; } = new();

    [JsonPropertyName("tagIds")]
    public List<string> TagIds { get; set; } = new();
}
