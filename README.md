# Shot Chapters for Jellyfin

A [Jellyfin](https://jellyfin.org) plugin that adds chapters at shot cuts to videos that have no chapters of their own,
such as most rips and home videos. With those chapters, Jellyfin can show chapter images and skip by chapter. It also
marks black stretches (fades to black, black gaps) as media segments.

The cuts come from [ShotDetector](https://github.com/bisforboman/shotdetector), a C# port of PySceneDetect. Its adaptive
detector is PySceneDetect's default.

Needs Jellyfin 12.0 or newer.

## Installing

In Jellyfin: Dashboard → Plugins → Repositories → add (+). Name it *Shot Chapters*, and give it this URL:

```
https://github.com/bisforboman/jellyfin-plugin-shotchapters/releases/latest/download/manifest.json
```

Then go to Catalog → Shot Chapters → Install, and restart the server. Updates show up in the same catalog.

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
- **Black stretches:** with *Mark black stretches as media segments* on (the default), the same scan also finds runs of
  frames whose mean brightness is under 12 of 255, PySceneDetect's fade threshold. Runs of at least 1 s by default
  are kept, in every video, including those with chapters of their own. The plugin reports them as media segments
  of the type you choose. The default type is *Unknown*, on which clients take no action unless told to; their skip
  settings are per type. They are stored as soon as a file is scanned, and Jellyfin's *Media Segment Scan* keeps
  them. Libraries can turn the provider off under their media segment settings, as with any other provider.
- **Decoding:** files are decoded by Jellyfin's own ffmpeg (jellyfin-ffmpeg 8; `ffprobe` must be next to it). Nothing
  native ships with the plugin.

## Settings

Dashboard → Plugins → Shot Chapters:

- **Minutes between chapters:** default 5.
- **Downscale factor:** 0 is automatic, as in PySceneDetect. A larger value scans faster but is coarser.
- **Scan each file on several cores:** off by default. When on, ShotDetector scans long files in chunks on several
  cores (files of two minutes or more, on 4 CPUs or more).
- **Mark black stretches as media segments:** on by default.
- **Shortest black stretch:** in seconds, default 1.
- **Segment type:** the type the black stretches get. The default is Unknown.

Changing a setting other than the segment type makes the next run scan every file again.

Scans need every frame (ShotDetector can't skip frames while it collects the per-frame values used to rank cuts).

## Building

```sh
dotnet test --solution Jellyfin.Plugin.ShotChapters.slnx    # SHOTCHAPTERS_FFMPEG=<folder with FFmpeg 8's ffmpeg and ffprobe> for the scan tests
dotnet publish src/Jellyfin.Plugin.ShotChapters -c Release -o artifacts/publish
python3 tools/package.py artifacts/publish artifacts/shot-chapters.zip
```

To install a build by hand, unzip `shot-chapters.zip` into a folder `Shot Chapters_<version>` under the server's
`plugins` folder and restart Jellyfin. CI builds the zip on every push; download it from the run's artifacts.

## Releasing

1. Set the version in `build.yaml` (`X.Y.Z.0`, with its `changelog`) and in the csproj (`<Version>X.Y.Z</Version>`),
   in a PR.
2. Once that has merged, push a tag `vX.Y.Z` on main.

`release.yml` checks that the versions agree, tests and packages the plugin, and publishes a GitHub release with
`shot-chapters_X.Y.Z.0.zip` and `manifest.json`. The manifest lists every version released so far
(`tools/manifest.py` adds the new one to the previous release's), so the URL above always serves the full list.

## Licence

GPL-3.0 (LICENSE), as Jellyfin requires of plugins built against its GPL-3.0 packages.

The plugin uses these MIT packages: ShotDetector, FrameReader and FFmpeg.AutoGen. It ships none of the LGPL packages
(ShotDetector.FastYuv, FrameReader.Native) and no FFmpeg of its own.
