using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Model;
using MediaBrowser.Model.MediaSegments;

namespace Jellyfin.Plugin.ShotChapters;

/// <summary>
/// Reports the black stretches the scheduled task found as media segments (the type set in the settings). It never
/// scans: a video the task hasn't scanned yet, or whose file changed since, has none until the task's next run.
/// </summary>
public sealed class BlackSegmentProvider : IMediaSegmentProvider
{
    readonly ILibraryManager _libraryManager;

    /// <summary>Initializes a new instance of the <see cref="BlackSegmentProvider"/> class.</summary>
    /// <param name="libraryManager">The library.</param>
    public BlackSegmentProvider(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <inheritdoc />
    public string Name => "Shot Chapters";

    /// <inheritdoc />
    public Task<IReadOnlyList<MediaSegmentDto>> GetMediaSegments(MediaSegmentGenerationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var config = Plugin.Instance?.Configuration;
        var item = _libraryManager.GetItemById(request.ItemId);
        var record = config is { MarkBlackSegments: true } && item is not null ? ChapterState.Shared.Get(request.ItemId) : null;
        IReadOnlyList<MediaSegmentDto> segments = record is not null
            && record.FileModifiedTicks == item!.DateModified.Ticks
            && record.Settings == config!.Key
            ? record.BlackSpans
                .Select(s => new MediaSegmentDto
                {
                    Id = Guid.NewGuid(),
                    ItemId = request.ItemId,
                    Type = config.BlackSegmentType,
                    StartTicks = s.StartTicks,
                    EndTicks = s.EndTicks,
                })
                .ToList()
            : [];
        return Task.FromResult(segments);
    }

    /// <inheritdoc />
    public ValueTask<bool> Supports(BaseItem item) => ValueTask.FromResult(GenerateChaptersTask.IsScannable(item));

    /// <inheritdoc />
    public Task CleanupExtractedData(Guid itemId, CancellationToken cancellationToken)
    {
        // The file changed: forget it, so the task's next run scans it again.
        var state = ChapterState.Shared;
        if (state.Remove(itemId))
            state.Save();
        return Task.CompletedTask;
    }
}
