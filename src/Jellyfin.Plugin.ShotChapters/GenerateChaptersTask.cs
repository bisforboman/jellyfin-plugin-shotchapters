using System.Globalization;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.ShotChapters.Configuration;
using MediaBrowser.Controller.Chapters;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShotChapters;

/// <summary>
/// The scheduled task "Generate chapters from shots": chapters at shot cuts for every video file that has none, or only
/// Jellyfin's dummy chapters. A video's own chapters are never touched. Each video is scanned once per file version and
/// settings; when a metadata refresh puts Jellyfin's chapters back, the next run saves the recorded ones again without
/// a new scan.
/// </summary>
public sealed partial class GenerateChaptersTask : IScheduledTask
{
    readonly ILibraryManager _libraryManager;
    readonly IChapterManager _chapterManager;
    readonly IMediaEncoder _mediaEncoder;
    readonly IServerConfigurationManager _serverConfiguration;
    readonly ILocalizationManager _localization;
    readonly ILogger<GenerateChaptersTask> _logger;

    /// <summary>Initializes a new instance of the <see cref="GenerateChaptersTask"/> class.</summary>
    /// <param name="libraryManager">The library.</param>
    /// <param name="chapterManager">Reads and saves chapters.</param>
    /// <param name="mediaEncoder">Where jellyfin-ffmpeg is.</param>
    /// <param name="serverConfiguration">The server's settings (its dummy chapter length).</param>
    /// <param name="localization">Chapter names in the server's language.</param>
    /// <param name="logger">The log.</param>
    public GenerateChaptersTask(
        ILibraryManager libraryManager,
        IChapterManager chapterManager,
        IMediaEncoder mediaEncoder,
        IServerConfigurationManager serverConfiguration,
        ILocalizationManager localization,
        ILogger<GenerateChaptersTask> logger)
    {
        _libraryManager = libraryManager;
        _chapterManager = chapterManager;
        _mediaEncoder = mediaEncoder;
        _serverConfiguration = serverConfiguration;
        _localization = localization;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Generate chapters from shots";

    /// <inheritdoc />
    public string Key => "ShotChaptersGenerate";

    /// <inheritdoc />
    public string Description => "Adds chapters at shot cuts to videos without chapters of their own.";

    /// <inheritdoc />
    public string Category => "Library";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Before Jellyfin's "Extract Chapter Images" (02:00), which then makes the new chapters' images.
        yield return new TaskTriggerInfo { Type = TaskTriggerInfoType.DailyTrigger, TimeOfDayTicks = TimeSpan.FromHours(1).Ticks };
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var plugin = Plugin.Instance ?? throw new InvalidOperationException("The plugin isn't loaded.");
        var config = plugin.Configuration;
        var state = ChapterState.Load(Path.Combine(plugin.DataFolderPath, "chapters.json"));
        string? ffmpegDirectory = string.IsNullOrEmpty(_mediaEncoder.EncoderPath) ? null : Path.GetDirectoryName(_mediaEncoder.EncoderPath);

        var videos = _libraryManager.GetItemList(new InternalItemsQuery
            {
                MediaTypes = [MediaType.Video],
                IsFolder = false,
                Recursive = true,
                IsVirtualItem = false,
                SourceTypes = [SourceType.Library],
                DtoOptions = new DtoOptions(false) { EnableImages = false },
            })
            .OfType<Video>()
            .Where(v => !v.IsShortcut && v.VideoType == VideoType.VideoFile && !string.IsNullOrEmpty(v.Path))
            .ToList();

        for (int i = 0; i < videos.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await ProcessAsync(videos[i], config, state, ffmpegDirectory, cancellationToken).ConfigureAwait(false))
                state.Save();
            progress.Report(100.0 * (i + 1) / videos.Count);
        }
    }

    /// <summary>Adds chapters to one video when it needs them.</summary>
    /// <returns>Whether its record changed.</returns>
    async Task<bool> ProcessAsync(Video video, PluginConfiguration config, ChapterState state, string? ffmpegDirectory, CancellationToken cancellationToken)
    {
        var current = _chapterManager.GetChapters(video.Id).Select(c => c.StartPositionTicks).ToList();
        var record = state.Get(video.Id);
        var kind = ExistingChapters.Classify(current, _serverConfiguration.Configuration.DummyChapterDuration, record?.Chapters);
        long fileTicks = video.DateModified.Ticks;
        string settings = config.Key;
        switch (ChapterDecision.Decide(kind, record, fileTicks, settings))
        {
            case ChapterAction.Skip:
                return false;
            case ChapterAction.Restore:
                Save(video, record!.Chapters);
                return false;
        }

        if (!File.Exists(video.Path))
            return false;

        ChapterRecord updated;
        try
        {
            int? downscale = config.Downscale > 0 ? config.Downscale : null;
            var (cuts, scannedSeconds) = await ShotScanner.ScanAsync(video.Path, ffmpegDirectory, downscale, config.UseSeveralCores, cancellationToken)
                .ConfigureAwait(false);
            double duration = video.RunTimeTicks is > 0 and long ticks ? TimeSpan.FromTicks(ticks).TotalSeconds : scannedSeconds;
            double interval = config.IntervalMinutes * 60.0;
            var starts = ChapterPicker.Pick(cuts, duration, interval, Math.Min(60, interval / 2))
                .Select(s => (long)Math.Round(s * TimeSpan.TicksPerSecond))
                .ToList();
            // As stored: Jellyfin drops chapters past the runtime, and the record must match what it keeps.
            starts = starts.Count <= 1 ? [] : Save(video, starts);
            LogScanned(video.Name, video.Path, cuts.Count, starts.Count);
            updated = new ChapterRecord { FileModifiedTicks = fileTicks, Settings = settings, Chapters = starts };
        }
#pragma warning disable CA1031 // One file that won't decode mustn't stop the task; it's recorded and logged.
        catch (Exception e) when (e is not OperationCanceledException)
#pragma warning restore CA1031
        {
            LogFailed(e, video.Name, video.Path);
            updated = new ChapterRecord { FileModifiedTicks = fileTicks, Settings = settings, Error = e.Message };
        }

        state.Set(video.Id, updated);
        return true;
    }

    /// <summary>Saves chapters at these starts.</summary>
    /// <returns>The starts Jellyfin stored.</returns>
    List<long> Save(Video video, IReadOnlyList<long> starts)
    {
        string format = _localization.GetLocalizedString("ChapterNameValue");
        var chapters = starts
            .Select((ticks, i) => new ChapterInfo
            {
                StartPositionTicks = ticks,
                Name = string.Format(CultureInfo.CurrentCulture, format, (i + 1).ToString(CultureInfo.InvariantCulture)),
            })
            .ToList();
        _chapterManager.SaveChapters(video, chapters);
        return _chapterManager.GetChapters(video.Id).Select(c => c.StartPositionTicks).ToList();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Shot chapters for {Name} ({Path}): {Cuts} cuts, {Chapters} chapters")]
    partial void LogScanned(string name, string path, int cuts, int chapters);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Shot chapters failed for {Name} ({Path})")]
    partial void LogFailed(Exception exception, string name, string path);
}
