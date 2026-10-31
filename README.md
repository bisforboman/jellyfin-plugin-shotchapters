# Shot Chapters for Jellyfin

A [Jellyfin](https://jellyfin.org) plugin that adds chapters at shot cuts to videos that have no chapters of their own,
such as most rips and home videos. With those chapters, Jellyfin can show chapter images and skip by chapter.

The cuts come from [ShotDetector](https://github.com/bisforboman/shotdetector), a C# port of PySceneDetect. Its adaptive
detector is PySceneDetect's default.

**Status:** first version, not released yet. Needs Jellyfin 12.0 or newer.

## What it does

- **The task:** the scheduled task **Generate chapters from shots** runs daily at 01:00 and can also be run from
  Dashboard → Scheduled Tasks. It finishes before Jellyfin's *Extract Chapter Images* task (02:00), which then makes
  images for the new chapters.
- **Which videos:** every video file in your libraries that has no chapters, or only Jellyfin's evenly spaced dummy
  chapters (*Dummy chapter duration* in the server's settings). A file's own chapters, and chapters another plugin
  wrote, are never replaced.
- **Where chapters go:** one chapter about every *N* minutes (5 by default). Each one sits at the strongest cut within
  half an interval of that point, so a chapter starts where the picture changes most. None starts in the last minute.
- **Each file is scanned once.** A file is scanned again only when it changes or the settings do. When a metadata
  refresh puts Jellyfin's own chapters back, the next run restores the plugin's chapters without scanning again.
  The plugin keeps its records in `chapters.json` in its data folder.
- **Decoding:** files are decoded by Jellyfin's own ffmpeg (jellyfin-ffmpeg 8; `ffprobe` must be next to it). Nothing
  native ships with the plugin.

## Settings

Dashboard → Plugins → Shot Chapters:

- **Minutes between chapters:** default 5.
- **Downscale factor:** 0 is automatic, as in PySceneDetect. A larger value scans faster but is coarser.
- **Scan each file on several cores:** off by default. When on, ShotDetector scans long files in chunks on several
  cores (files of two minutes or more, on 4 CPUs or more).

Scans need every frame (ShotDetector can't skip frames while it collects the per-frame values used to rank cuts).

## Building

```sh
dotnet test --solution Jellyfin.Plugin.ShotChapters.slnx    # SHOTCHAPTERS_FFMPEG=<folder with FFmpeg 8's ffmpeg and ffprobe> for the scan tests
dotnet publish src/Jellyfin.Plugin.ShotChapters -c Release -o artifacts/publish
python3 tools/package.py artifacts/publish artifacts/shot-chapters.zip
```

To install by hand, unzip `shot-chapters.zip` into a folder `Shot Chapters_0.1.0.0` under the server's `plugins`
folder and restart Jellyfin. CI builds the zip on every push; download it from the run's artifacts.

## Licence

GPL-3.0 (LICENSE), as Jellyfin requires of plugins built against its GPL-3.0 packages.

The plugin uses these MIT packages: ShotDetector, FrameReader and FFmpeg.AutoGen. It ships none of the LGPL packages
(ShotDetector.FastYuv, FrameReader.Native) and no FFmpeg of its own.
