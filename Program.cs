using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

Console.WriteLine("============================================================");
Console.WriteLine("  AUDIO METADATA PURGE TOOL (C# Cross-Platform Edition)");
Console.WriteLine("============================================================");

string targetFolder = "";

if (args.Length > 0)
{
    targetFolder = args[0];
}
else
{
    Console.WriteLine("Enter or paste the absolute path to your music folder:");
    Console.Write("> ");
    targetFolder = Console.ReadLine() ?? "";
}

targetFolder = targetFolder.Trim('"', '\'', ' ').Replace("\\ ", " ");

if (string.IsNullOrWhiteSpace(targetFolder) || !Directory.Exists(targetFolder))
{
    Console.WriteLine("\n[!] Error: Invalid or non-existent target directory.");
    ExitPrompt();
    return;
}

Console.WriteLine($"\nScanning target directory: {targetFolder}\n" + new string('-', 60));

int totalProcessed = ProcessDirectory(targetFolder);

Console.WriteLine("\n" + new string('-', 60));
Console.WriteLine($"Processing complete! Evaluated {totalProcessed} target files.");
ExitPrompt();


static int ProcessDirectory(string directoryPath)
{
    int fileCount = 0;
    string[] extensions = { ".mp3", ".m4a", ".mp4", ".aac", ".flac", ".ape", ".wma", ".asf" };

    try
    {
        var allFiles = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories);

        foreach (var filePath in allFiles)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            if (!extensions.Contains(ext)) continue;

            PurgeHiddenTags(filePath);
            fileCount++;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Directory Error] Structural access fault: {ex.Message}");
    }

    return fileCount;
}

static void PurgeHiddenTags(string filePath)
{
    string fileName = Path.GetFileName(filePath);
    try
    {
        using (var audioFile = TagLib.File.Create(filePath))
        {
            bool tagsModified = false;

            var artwork = audioFile.Tag.Pictures ?? Array.Empty<TagLib.IPicture>();
            if (artwork.Length > 0)
            {
                if (TryExtractArtwork(filePath, artwork, out int savedArtworkCount))
                {
                    audioFile.Tag.Pictures = Array.Empty<TagLib.IPicture>();
                    tagsModified = true;
                    Console.WriteLine($"[Artwork] {fileName} -> Exported {savedArtworkCount} new image(s); removed embedded artwork.");
                }
                else
                {
                    Console.WriteLine($"[Artwork retained] {fileName} -> Could not safely export every image.");
                }
            }

            // 1. Core ID3v2 (MP3) Layout Filtering
            if (audioFile.GetTag(TagLib.TagTypes.Id3v2) is TagLib.Id3v2.Tag id3v2Tag)
            {
                var framesToDelete = id3v2Tag.Where(frame =>
                    frame.FrameId == "PRIV" || 
                    frame.FrameId == "TXXX" || 
                    frame.FrameId == "POPM" || 
                    (frame.FrameId == "COMM" && frame.ToString().ToUpper().Contains("ITUN")) || 
                    frame.ToString().ToUpper().Contains("ITUNNORM") || 
                    frame.ToString().ToUpper().Contains("ITUNSMPB")
                ).ToList();

                if (framesToDelete.Any())
                {
                    foreach (var frame in framesToDelete)
                    {
                        id3v2Tag.RemoveFrame(frame);
                    }
                    tagsModified = true;
                }
            }

            // 2. Apple / iTunes Atoms (MP4/M4A containers) - FIXED FOR CS1061
            if (audioFile.GetTag(TagLib.TagTypes.Apple) is TagLib.Mpeg4.AppleTag appleTag)
            {
                // Find all structural boxes containing iTunes custom identifier values
                var atomsToDelete = appleTag.Where(box => 
                    box.BoxType.ToString().Contains("com.apple.iTunes") || 
                    box.BoxType.ToString().ToUpper().Contains("ITUN")
                ).Select(box => box.BoxType).ToList();

                if (atomsToDelete.Any())
                {
                    foreach (var boxType in atomsToDelete)
                    {
                        // TagLib uses ClearData to purge child objects matching a ByteVector key
                        appleTag.ClearData(boxType);
                    }
                    tagsModified = true;
                }
            }

            // 3. WMA / Windows Media Server Injections (ASF containers)
            if (audioFile.GetTag(TagLib.TagTypes.Asf) is TagLib.Asf.Tag asfTag)
            {
                string[] wmaTrackingTargets = { 
                    "WM/UniqueFileID", "WM/WMCollectionID", "WM/WMCollectionGroupID", 
                    "WM/Provider", "WM/ToolName", "WM/ToolVersion" 
                };

                foreach (var target in wmaTrackingTargets)
                {
                    asfTag.RemoveDescriptors(target);
                    tagsModified = true;
                }
            }

            // 4. APE and FLAC / Vorbis generic text field mappings
            var genericTag = audioFile.Tag;
            if (genericTag != null)
            {
                if (!double.IsNaN(genericTag.ReplayGainAlbumGain) || !double.IsNaN(genericTag.ReplayGainTrackGain))
                {
                    genericTag.ReplayGainAlbumGain = double.NaN;
                    genericTag.ReplayGainTrackGain = double.NaN;
                    tagsModified = true;
                }
            }

            if (tagsModified)
            {
                audioFile.Save();
                Console.WriteLine($"[Stripped] {fileName} -> Purged hidden proprietary/iTunes metadata blocks.");
            }
            else
            {
                Console.WriteLine($"[Clean] {fileName}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] Could not process {fileName}: {ex.Message}");
    }
}

static bool TryExtractArtwork(string audioPath, TagLib.IPicture[] pictures, out int savedArtworkCount)
{
    savedArtworkCount = 0;
    bool allArtworkSaved = true;
    string directory = Path.GetDirectoryName(audioPath) ?? Directory.GetCurrentDirectory();

    foreach (var picture in pictures)
    {
        byte[]? imageData = picture.Data?.Data;
        if (imageData == null || imageData.Length == 0)
        {
            allArtworkSaved = false;
            continue;
        }

        string hash = Convert.ToHexString(SHA256.HashData(imageData));
        try
        {
            if (Directory.EnumerateFiles(directory)
                .Any(path => FileHasHash(path, hash, imageData.Length)))
            {
                continue;
            }

            string extension = GetArtworkExtension(picture.MimeType, imageData);
            string outputPath = Path.Combine(directory, $"cover{extension}");
            int suffix = 2;
            while (File.Exists(outputPath))
            {
                outputPath = Path.Combine(directory, $"cover-{suffix}{extension}");
                suffix++;
            }

            File.WriteAllBytes(outputPath, imageData);
            savedArtworkCount++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Artwork error] {Path.GetFileName(audioPath)}: {ex.Message}");
            allArtworkSaved = false;
        }
    }

    return allArtworkSaved;
}

static bool FileHasHash(string path, string expectedHash, int expectedLength)
{
    try
    {
        using (var stream = File.OpenRead(path))
        {
            if (stream.Length != expectedLength)
                return false;

            using (var sha256 = SHA256.Create())
            {
                string fileHash = Convert.ToHexString(sha256.ComputeHash(stream));
                return string.Equals(fileHash, expectedHash, StringComparison.Ordinal);
            }
        }
    }
    catch
    {
        return false;
    }
}

static string GetArtworkExtension(string mimeType, byte[] imageData)
{
    switch (mimeType?.ToLowerInvariant())
    {
        case "image/jpeg":
        case "image/jpg":
            return ".jpg";
        case "image/png":
            return ".png";
        case "image/gif":
            return ".gif";
        case "image/bmp":
            return ".bmp";
        case "image/tiff":
            return ".tiff";
        case "image/webp":
            return ".webp";
        case "image/avif":
            return ".avif";
        case "image/heic":
            return ".heic";
        case "image/svg+xml":
            return ".svg";
    }

    if (imageData.Length >= 3 && imageData[0] == 0xFF && imageData[1] == 0xD8 && imageData[2] == 0xFF)
        return ".jpg";
    if (imageData.Length >= 8 && imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
        return ".png";
    if (imageData.Length >= 6 && imageData[0] == 'G' && imageData[1] == 'I' && imageData[2] == 'F')
        return ".gif";
    if (imageData.Length >= 12 && imageData[0] == 'R' && imageData[1] == 'I' && imageData[2] == 'F' && imageData[8] == 'W' && imageData[9] == 'E' && imageData[10] == 'B' && imageData[11] == 'P')
        return ".webp";

    return ".img";
}

static void ExitPrompt()
{
    Console.WriteLine("\nPress ENTER to exit...");
    Console.ReadLine();
}
