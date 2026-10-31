using System.Text.Json;

namespace Jellyfin.Plugin.ShotChapters;

/// <summary>A stretch of black frames, in ticks: from its first black frame to the first frame after it.</summary>
/// <param name="StartTicks">Where it starts.</param>
/// <param name="EndTicks">Where it ends.</param>
internal sealed record BlackSpan(long StartTicks, long EndTicks);

/// <summary>What the plugin found and did for one video, so the next run can skip it.</summary>
internal sealed record ChapterRecord
{
    /// <summary>Gets the video file's modification time (ticks) when it was scanned.</summary>
    public long FileModifiedTicks { get; init; }

    /// <summary>Gets the settings it was scanned with (<see cref="Configuration.PluginConfiguration.Key"/>).</summary>
    public string Settings { get; init; } = "";

    /// <summary>Gets the chapter starts saved (ticks); empty when none were saved (none found, or the file's own kept).</summary>
    public IReadOnlyList<long> Chapters { get; init; } = [];

    /// <summary>Gets the black stretches found, which the media segment provider reports; empty when segments are off.</summary>
    public IReadOnlyList<BlackSpan> BlackSpans { get; init; } = [];

    /// <summary>Gets the error, when the scan failed: retried only when the file or the settings change.</summary>
    public string? Error { get; init; }
}

/// <summary>
/// The plugin's records, by item id, in a JSON file in the plugin's data folder. Thread-safe: the scheduled task writes
/// them while Jellyfin's media segment scan reads them through the provider.
/// </summary>
internal sealed class ChapterState
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
    static readonly Lock SharedLock = new();
    static ChapterState? _shared;
    readonly string _path;
    readonly Dictionary<Guid, ChapterRecord> _records;
    readonly Lock _lock = new();

    ChapterState(string path, Dictionary<Guid, ChapterRecord> records)
    {
        _path = path;
        _records = records;
    }

    /// <summary>Gets the loaded plugin's records (chapters.json in its data folder), loaded once.</summary>
    public static ChapterState Shared
    {
        get
        {
            lock (SharedLock)
            {
                var plugin = Plugin.Instance ?? throw new InvalidOperationException("The plugin isn't loaded.");
                return _shared ??= Load(Path.Combine(plugin.DataFolderPath, "chapters.json"));
            }
        }
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
    public ChapterRecord? Get(Guid itemId)
    {
        lock (_lock)
            return _records.GetValueOrDefault(itemId);
    }

    /// <summary>Sets an item's record (in memory; <see cref="Save"/> writes the file).</summary>
    /// <param name="itemId">The item.</param>
    /// <param name="record">Its record.</param>
    public void Set(Guid itemId, ChapterRecord record)
    {
        lock (_lock)
            _records[itemId] = record;
    }

    /// <summary>Forgets an item (in memory; <see cref="Save"/> writes the file).</summary>
    /// <param name="itemId">The item.</param>
    /// <returns>Whether it had a record.</returns>
    public bool Remove(Guid itemId)
    {
        lock (_lock)
            return _records.Remove(itemId);
    }

    /// <summary>Writes the records, through a temporary file so a crash never leaves half a file.</summary>
    public void Save()
    {
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            string temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_records, Json));
            File.Move(temp, _path, overwrite: true);
        }
    }
}
