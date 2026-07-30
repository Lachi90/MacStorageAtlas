using System.IO;

namespace MacStorageAtlas.Core;

public static class FileCategoryMap
{
    public const int Version = 1;

    private static readonly Dictionary<string, FileCategory> Categories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".7z"] = FileCategory.Archive,
            [".bz2"] = FileCategory.Archive,
            [".gz"] = FileCategory.Archive,
            [".rar"] = FileCategory.Archive,
            [".tar"] = FileCategory.Archive,
            [".tgz"] = FileCategory.Archive,
            [".xz"] = FileCategory.Archive,
            [".zip"] = FileCategory.Archive,
            [".zst"] = FileCategory.Archive,

            [".avi"] = FileCategory.Video,
            [".flv"] = FileCategory.Video,
            [".m4v"] = FileCategory.Video,
            [".mkv"] = FileCategory.Video,
            [".mov"] = FileCategory.Video,
            [".mp4"] = FileCategory.Video,
            [".mpeg"] = FileCategory.Video,
            [".mpg"] = FileCategory.Video,
            [".webm"] = FileCategory.Video,
            [".wmv"] = FileCategory.Video,

            [".bmp"] = FileCategory.Image,
            [".cr2"] = FileCategory.Image,
            [".gif"] = FileCategory.Image,
            [".heic"] = FileCategory.Image,
            [".heif"] = FileCategory.Image,
            [".jpeg"] = FileCategory.Image,
            [".jpg"] = FileCategory.Image,
            [".nef"] = FileCategory.Image,
            [".png"] = FileCategory.Image,
            [".svg"] = FileCategory.Image,
            [".tif"] = FileCategory.Image,
            [".tiff"] = FileCategory.Image,
            [".webp"] = FileCategory.Image,

            [".aac"] = FileCategory.Audio,
            [".aif"] = FileCategory.Audio,
            [".aiff"] = FileCategory.Audio,
            [".flac"] = FileCategory.Audio,
            [".m4a"] = FileCategory.Audio,
            [".mp3"] = FileCategory.Audio,
            [".ogg"] = FileCategory.Audio,
            [".opus"] = FileCategory.Audio,
            [".wav"] = FileCategory.Audio,
            [".wma"] = FileCategory.Audio,

            [".doc"] = FileCategory.Document,
            [".docx"] = FileCategory.Document,
            [".epub"] = FileCategory.Document,
            [".key"] = FileCategory.Document,
            [".md"] = FileCategory.Document,
            [".numbers"] = FileCategory.Document,
            [".odt"] = FileCategory.Document,
            [".pages"] = FileCategory.Document,
            [".pdf"] = FileCategory.Document,
            [".ppt"] = FileCategory.Document,
            [".pptx"] = FileCategory.Document,
            [".rtf"] = FileCategory.Document,
            [".txt"] = FileCategory.Document,
            [".xls"] = FileCategory.Document,
            [".xlsx"] = FileCategory.Document,

            [".dmg"] = FileCategory.DiskImage,
            [".img"] = FileCategory.DiskImage,
            [".iso"] = FileCategory.DiskImage,
            [".pkg"] = FileCategory.DiskImage,
            [".sparsebundle"] = FileCategory.DiskImage,
            [".sparseimage"] = FileCategory.DiskImage,

            [".c"] = FileCategory.Code,
            [".cpp"] = FileCategory.Code,
            [".cs"] = FileCategory.Code,
            [".css"] = FileCategory.Code,
            [".go"] = FileCategory.Code,
            [".h"] = FileCategory.Code,
            [".hpp"] = FileCategory.Code,
            [".html"] = FileCategory.Code,
            [".java"] = FileCategory.Code,
            [".js"] = FileCategory.Code,
            [".json"] = FileCategory.Code,
            [".jsx"] = FileCategory.Code,
            [".kt"] = FileCategory.Code,
            [".m"] = FileCategory.Code,
            [".mm"] = FileCategory.Code,
            [".php"] = FileCategory.Code,
            [".py"] = FileCategory.Code,
            [".rb"] = FileCategory.Code,
            [".rs"] = FileCategory.Code,
            [".scss"] = FileCategory.Code,
            [".sh"] = FileCategory.Code,
            [".sql"] = FileCategory.Code,
            [".swift"] = FileCategory.Code,
            [".ts"] = FileCategory.Code,
            [".tsx"] = FileCategory.Code,
            [".xml"] = FileCategory.Code,
            [".yaml"] = FileCategory.Code,
            [".yml"] = FileCategory.Code
        };

    public static FileCategory? Find(string? extension)
    {
        var normalized = NormalizeExtension(extension);
        return normalized is not null && Categories.TryGetValue(normalized, out var category)
            ? category
            : null;
    }

    public static FileCategory? FindForFileName(string? fileName) =>
        string.IsNullOrWhiteSpace(fileName)
            ? null
            : Find(Path.GetExtension(fileName));

    public static string? NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        var trimmed = extension.Trim();
        if (trimmed.Length == 0 || trimmed == ".")
        {
            return null;
        }

        return trimmed.StartsWith('.')
            ? trimmed.ToLowerInvariant()
            : $".{trimmed.ToLowerInvariant()}";
    }
}
