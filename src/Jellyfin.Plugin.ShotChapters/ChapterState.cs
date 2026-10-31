using System.Text.Json;

namespace Jellyfin.Plugin.ShotChapters;

/// <summary>What the plugin did for one video, so the next run can skip it.</summary>
internal sealed record ChapterRecord
{
    /// <summary>Gets the video file's modification time (ticks) when it was scanned.</summary>
    public long FileModifiedTicks { get; init; }

    /// <summary>Gets the settings it was scanned with (<see cref="Configuration.PluginConfiguration.Key"/>).</summary>
    public string Settings { get; init; } = "";

    /// <summary>Gets the chapter starts saved (ticks); empty when the scan gave no chapters to save.</summary>
    public IReadOnlyList<long> Chapters { get; init; } = [];

    /// <summary>Gets the error, when the scan failed: retried only when the file or the settings change.</summary>
    public string? Error { get; init; }
}

/// <summary>The plugin's records, by item id, in a JSON file in the plugin's data folder.</summary>
internal sealed class ChapterState
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
    readonly string _path;
    readonly Dictionary<Guid, ChapterRecord> _records;

    ChapterState(string path, Dictionary<Guid, ChapterRecord> records)
    {
        _path = path;
        _records = records;
    }

    /// <summary>Loads the records from a file; a missing or unreadable file gives none.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The records.</returns>
    public static ChapterState Load(string path)
    {
        Dictionary<Guid, ChapterRecord>? records = null;
        try
        {
            if (File.Exists(path))
                records = JsonSerializer.Deserialize<Dictionary<Guid, ChapterRecord>>(File.ReadAllText(path), Json);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            records = null;
        }

        return new ChapterState(path, records ?? []);
    }

    /// <summary>Gets an item's record, or null.</summary>
    /// <param name="itemId">The item.</param>
    /// <returns>Its record.</returns>
    public ChapterRecord? Get(Guid itemId) => _records.GetValueOrDefault(itemId);

    /// <summary>Sets an item's record (in memory; <see cref="Save"/> writes the file).</summary>
    /// <param name="itemId">The item.</param>
    /// <param name="record">Its record.</param>
    public void Set(Guid itemId, ChapterRecord record) => _records[itemId] = record;

    /// <summary>Writes the records, through a temporary file so a crash never leaves half a file.</summary>
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_records, Json));
        File.Move(temp, _path, overwrite: true);
    }
}
