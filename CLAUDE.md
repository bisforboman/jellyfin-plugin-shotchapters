# Shot Chapters (Jellyfin plugin)

A Jellyfin 12 plugin (GPL-3.0) that adds chapters at shot cuts, using ShotDetector from NuGet
(github.com/bisforboman/shotdetector, MIT). The owner decided the scope on 2026-10-31: this repository, public, Jellyfin 12
only, chapters first and then black/fade media segments. That decision is in the shotdetector repository at
docs/decisions/2026-10-31-jellyfin-plugin.md; the scoping note is docs/ideas/jellyfin-plugin.md there.

## Layout

- `src/Jellyfin.Plugin.ShotChapters`:
  - `Plugin` and `Configuration/` (the settings and the dashboard page).
  - `GenerateChaptersTask`: the scheduled task. It scans, saves chapters, records black stretches and runs the
    segment providers for the scanned item.
  - `BlackSegmentProvider` (registered in `PluginServiceRegistrator`): reports the recorded stretches as media
    segments. It never scans.
  - Pure logic, tested without Jellyfin:
    - `ChapterPicker`: chapter starts from the cuts.
    - `ExistingChapters`/`ChapterDecision`: whose chapters a video has, and what to do with it.
    - `BlackSpans`: black stretches from the per-frame `average_rgb`.
    - `ChapterState`: the records, as JSON. `Shared` is the instance the task and the provider share; it is
      thread-safe.
  - `ShotScanner`: the ShotDetector call.
- `tests/`: xunit v3 on Microsoft.Testing.Platform (`dotnet test --solution ...`). `ShotScannerTests` needs
  `SHOTCHAPTERS_FFMPEG`, a folder holding FFmpeg 8's ffmpeg and ffprobe. CI downloads a pinned BtbN build.
- `build.yaml`: the plugin's manifest. `tools/package.py` turns it into the zip's `meta.json`, with `assemblies` set.

## Jellyfin facts (from its source, v12.0-v12.4)

- **Compile target:** Jellyfin.Controller/Model 12.0.0 with `ExcludeAssets=runtime`. IChapterManager,
  IMediaSegmentProvider and IScheduledTask are the same from 12.0 to 12.4, so `targetAbi` is 12.0.0.0.
- **Loading:** IScheduledTask implementations are found by reflection. An IMediaSegmentProvider has to be registered
  in an IPluginServiceRegistrator.
- **No native libraries:** the plugin load context resolves no native libraries, and without `assemblies` it loads
  every .dll in the folder as managed. So ship none, and keep `assemblies` in meta.json.
- **Refreshes replace chapters:** a metadata refresh (Default/FullRefresh) saves ffprobe's chapters again, or dummy
  ones. Dummy chapters start at multiples of `DummyChapterDuration` from 0. `SaveChapters` drops chapters past the
  runtime, so the task records what `GetChapters` returns after saving.
- **FFmpeg version:** ShotDetector needs a recent FFmpeg through the executable: 6.1 lacks `in_chroma_loc`. 8.1 works,
  and that is jellyfin-ffmpeg's version.
- **Segment ids:** `MediaSegmentDto.Id` is stored as the row's key, so each segment needs its own (`Guid.NewGuid()`).
  `RunSegmentPluginProviders` compares start, end and type, and leaves unchanged segments alone.
  `CleanupExtractedData` (12.x) is called when an item's file changes; the plugin forgets the item there.
- **Black frames:** they come from ThresholdDetector's `average_rgb`, so that detector runs alongside the adaptive one,
  and its fade cuts are chapter candidates too.
- **No frame skip:** `DetectionOptions.FrameSkip` can't be combined with `CollectStats`, and the cut strengths
  (`content_val`) come from the stats.

## Working here

- **Branches:** work on a branch and open a PR; don't push to main.
- **Merging:** merge your own PR yourself once CI is green (the owner's choice, 2026-10-31). Auto-merge is off in the
  repository's settings.
- **Before pushing:** run `dotnet test` with SHOTCHAPTERS_FFMPEG set, then package once to check the zip.
- **Releases:** the owner says when to release. Bump `build.yaml` (`X.Y.Z.0`, with its changelog) and the csproj
  (`X.Y.Z`) in a PR, then push the tag `vX.Y.Z` on main, or dispatch `release.yml` on main (cloud sessions: their git
  proxy refuses tag pushes and the refs API, but `workflow_dispatch` works), which creates the tag. `release.yml` publishes the zip and `manifest.json`; the
  stable URL is `releases/latest/download/manifest.json`. Jellyfin checks each zip's MD5 against the manifest's
  `checksum`.
- **Pinned actions:** actions are pinned by commit SHA, with the tag in a comment.
- **Commit messages:** say what and why. Keep commits small.
