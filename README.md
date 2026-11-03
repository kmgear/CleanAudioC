# CleanAudioC

A cross-platform .NET console utility that scans a music folder, exports embedded artwork beside audio files, and removes selected hidden or proprietary metadata.

> **Warning:** The tool modifies audio files in place. Back up your collection before running it. Artwork removal and metadata cleanup cannot be undone by this application.

## Features

- Recursively scans a directory and its subdirectories.
- Processes `.mp3`, `.m4a`, `.mp4`, `.aac`, `.flac`, `.ape`, `.wma`, and `.asf` files.
- Extracts embedded pictures to the same directory as the audio file.
- Names extracted images `cover.<ext>`, then `cover-2.<ext>`, `cover-3.<ext>`, and so on when those names already exist.
- Avoids duplicate artwork when an identical file already exists beside the audio file by comparing file length and SHA-256 content hash.
- Removes embedded artwork from the audio metadata only when every picture was successfully exported or an identical existing file was found.
- Removes selected ID3v2, iTunes/Apple, ASF/WMA, and ReplayGain metadata.
- Prints a status for each audio file and continues when an individual file cannot be processed.

Artwork extensions are derived from the embedded picture's MIME type, with JPEG, PNG, GIF, and WebP signature checks as a fallback. Unknown image formats are saved with the `.img` extension.

## Requirements

- .NET 6 SDK or later to build the project.
- The project uses [TagLibSharp](https://www.nuget.org/packages/TagLibSharp/) 2.3.0 for audio metadata access.

## Run

From the project directory, pass the target folder as the first argument:

```powershell
dotnet run -- "D:\Music Collection"
```

If no folder argument is provided, the application prompts for a directory path:

```powershell
dotnet run
```

The path must be an existing directory. The tool traverses subdirectories recursively.

## Build

```powershell
dotnet build
```

## Publish a self-contained executable

Example for Windows x64:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:PublishTrimmed=true
```

Change the runtime identifier as needed for another target platform, such as `linux-x64` or `osx-arm64`. Published output is written beneath `bin/Release/net6.0/<runtime-id>/publish/`.

## GitHub Actions

- **CI** runs on pushes to any branch, pull requests, or manual dispatch. It restores and builds the project on Windows, Linux, and macOS.
- **Publish Releases** runs when a `v*` tag is pushed, or when manually dispatched. It creates self-contained ZIP packages for Windows x64, Linux x64, and macOS ARM64. Tag pushes also create a GitHub Release and attach all three packages; manual runs upload the packages as workflow artifacts.

## Metadata affected

- **ID3v2:** removes `PRIV`, `TXXX`, and `POPM` frames, plus `COMM` frames and other frame text that contain the indicated iTunes markers.
- **Apple/iTunes atoms:** removes atoms whose identifiers contain `com.apple.iTunes` or `ITUN`.
- **ASF/WMA:** removes descriptors named `WM/UniqueFileID`, `WM/WMCollectionID`, `WM/WMCollectionGroupID`, `WM/Provider`, `WM/ToolName`, and `WM/ToolVersion`.
- **ReplayGain:** clears album and track gain fields.
- **Artwork:** exports pictures and clears the embedded picture collection after successful export/deduplication.

Other tags are not intentionally cleared by these operations.

## Output messages

- `[Artwork]` — picture(s) were exported or found as identical sibling files, and embedded artwork was removed.
- `[Artwork retained]` — at least one picture could not be safely exported, so embedded artwork was preserved.
- `[Stripped]` — metadata changes were saved to the audio file.
- `[Clean]` — no changes were saved.
- `[Error]` / `[Artwork error]` — a file or artwork operation failed; check the message and permissions.

## Limitations

- Duplicate checking is limited to files in the same directory as the audio file. Images in other directories are not considered.
- The process writes metadata changes directly to source files. Keep a backup until you have verified the results.
- Supported audio formats and metadata capabilities depend on TagLibSharp.
- The application targets .NET 6 (`net6.0`).
---

## License

Distributed under the **MIT License**. Feel free to branch, modify, and adjust tracking targets for personal backup archiving deployments.
