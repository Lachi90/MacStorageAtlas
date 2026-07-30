namespace MacStorageAtlas.Core;

public sealed record DiskItemFilter
{
    public static DiskItemFilter Empty { get; } = new();

    public string? TextTerm { get; init; }

    public long? MinimumSizeBytes { get; init; }

    public long? MaximumSizeBytes { get; init; }

    public DateTimeOffset? CreatedAfter { get; init; }

    public DateTimeOffset? CreatedBefore { get; init; }

    public DateTimeOffset? ModifiedAfter { get; init; }

    public DateTimeOffset? ModifiedBefore { get; init; }

    public DateTimeOffset? LastAccessedAfter { get; init; }

    public DateTimeOffset? LastAccessedBefore { get; init; }

    public IReadOnlyList<string> Extensions { get; init; } = [];

    public IReadOnlyList<FileCategory> Categories { get; init; } = [];

    public bool SharedStorageOnly { get; init; }

    public bool IsActive =>
        !string.IsNullOrWhiteSpace(TextTerm)
        || MinimumSizeBytes is not null
        || MaximumSizeBytes is not null
        || HasDateCriteria
        || Extensions.Count > 0
        || Categories.Count > 0
        || SharedStorageOnly;

    public bool HasDateCriteria =>
        CreatedAfter is not null
        || CreatedBefore is not null
        || ModifiedAfter is not null
        || ModifiedBefore is not null
        || LastAccessedAfter is not null
        || LastAccessedBefore is not null;

    public DiskItemFilterValidation Validate()
    {
        if (MinimumSizeBytes is { } minimum && minimum < 0)
        {
            return DiskItemFilterValidation.Invalid(
                "Minimum size cannot be negative.");
        }

        if (MaximumSizeBytes is { } maximum && maximum < 0)
        {
            return DiskItemFilterValidation.Invalid(
                "Maximum size cannot be negative.");
        }

        if (MinimumSizeBytes is { } lower
            && MaximumSizeBytes is { } upper
            && lower > upper)
        {
            return DiskItemFilterValidation.Invalid(
                "Minimum size is larger than maximum size.");
        }

        if (CreatedAfter is { } createdAfter
            && CreatedBefore is { } createdBefore
            && createdAfter > createdBefore)
        {
            return DiskItemFilterValidation.Invalid(
                "Created-after date is later than created-before date.");
        }

        if (ModifiedAfter is { } modifiedAfter
            && ModifiedBefore is { } modifiedBefore
            && modifiedAfter > modifiedBefore)
        {
            return DiskItemFilterValidation.Invalid(
                "Modified-after date is later than modified-before date.");
        }

        if (LastAccessedAfter is { } accessedAfter
            && LastAccessedBefore is { } accessedBefore
            && accessedAfter > accessedBefore)
        {
            return DiskItemFilterValidation.Invalid(
                "Accessed-after date is later than accessed-before date.");
        }

        return DiskItemFilterValidation.Valid;
    }

    public IReadOnlyList<string> NormalizedExtensions =>
        Extensions
            .Select(FileCategoryMap.NormalizeExtension)
            .Where(extension => extension is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public bool Equals(DiskItemFilter? other) =>
        other is not null
        && string.Equals(TextTerm, other.TextTerm, StringComparison.Ordinal)
        && MinimumSizeBytes == other.MinimumSizeBytes
        && MaximumSizeBytes == other.MaximumSizeBytes
        && CreatedAfter == other.CreatedAfter
        && CreatedBefore == other.CreatedBefore
        && ModifiedAfter == other.ModifiedAfter
        && ModifiedBefore == other.ModifiedBefore
        && LastAccessedAfter == other.LastAccessedAfter
        && LastAccessedBefore == other.LastAccessedBefore
        && SharedStorageOnly == other.SharedStorageOnly
        && Extensions.SequenceEqual(other.Extensions, StringComparer.Ordinal)
        && Categories.SequenceEqual(other.Categories);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TextTerm, StringComparer.Ordinal);
        hash.Add(MinimumSizeBytes);
        hash.Add(MaximumSizeBytes);
        hash.Add(CreatedAfter);
        hash.Add(CreatedBefore);
        hash.Add(ModifiedAfter);
        hash.Add(ModifiedBefore);
        hash.Add(LastAccessedAfter);
        hash.Add(LastAccessedBefore);
        hash.Add(SharedStorageOnly);

        foreach (var extension in Extensions)
        {
            hash.Add(extension, StringComparer.Ordinal);
        }

        foreach (var category in Categories)
        {
            hash.Add(category);
        }

        return hash.ToHashCode();
    }
}
