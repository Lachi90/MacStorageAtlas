namespace MacStorageAtlas.Core;

public static class BuiltInFilterPresets
{
    public const long OneGigabyte = 1024L * 1024 * 1024;

    public const long OneHundredMegabytes = 100L * 1024 * 1024;

    public const int OneYearInDays = 365;

    public const string LargerThanOneGigabyteName = "Larger than 1 GB";

    public const string NotModifiedForOneYearName = "Not modified for one year";

    public const string LargeArchivesName = "Large archives";

    public const string LargeDiskImagesAndInstallersName =
        "Large disk images and installers";

    public static IReadOnlyList<FilterPreset> Create(DateTimeOffset referenceTime) =>
    [
        new FilterPreset(
            LargerThanOneGigabyteName,
            new DiskItemFilter { MinimumSizeBytes = OneGigabyte },
            IsBuiltIn: true),
        new FilterPreset(
            NotModifiedForOneYearName,
            new DiskItemFilter
            {
                ModifiedBefore = referenceTime.AddDays(-OneYearInDays)
            },
            IsBuiltIn: true),
        new FilterPreset(
            LargeArchivesName,
            new DiskItemFilter
            {
                Categories = [FileCategory.Archive],
                MinimumSizeBytes = OneHundredMegabytes
            },
            IsBuiltIn: true),
        new FilterPreset(
            LargeDiskImagesAndInstallersName,
            new DiskItemFilter
            {
                Categories = [FileCategory.DiskImage],
                MinimumSizeBytes = OneHundredMegabytes
            },
            IsBuiltIn: true)
    ];
}
