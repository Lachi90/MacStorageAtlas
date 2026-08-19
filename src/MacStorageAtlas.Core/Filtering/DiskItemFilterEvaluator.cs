using System.IO;
using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.Core.Filtering;

public sealed class DiskItemFilterEvaluator
{
    public FilterResult Evaluate(
        DiskItem root,
        DiskItemFilter filter,
        DateTimeOffset referenceTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(filter);

        var criteria = new Criteria(filter, referenceTime);
        var matchedFiles = new List<DiskItem>();
        var matchedBytesByDirectory = new Dictionary<DiskItem, long>(
            ReferenceEqualityComparer.Instance);
        var unknownDateExclusionCount = 0L;

        _ = Visit(
            root,
            criteria,
            matchedFiles,
            matchedBytesByDirectory,
            ref unknownDateExclusionCount,
            cancellationToken);

        return new FilterResult(
            filter,
            referenceTime,
            matchedFiles,
            matchedBytesByDirectory,
            unknownDateExclusionCount);
    }

    private static Contribution Visit(
        DiskItem item,
        Criteria criteria,
        List<DiskItem> matchedFiles,
        Dictionary<DiskItem, long> matchedBytesByDirectory,
        ref long unknownDateExclusionCount,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!item.IsDirectory)
        {
            var outcome = criteria.Evaluate(item);
            if (outcome == MatchOutcome.ExcludedByUnknownDate)
            {
                unknownDateExclusionCount++;
                return Contribution.None;
            }

            if (outcome == MatchOutcome.Excluded)
            {
                return Contribution.None;
            }

            matchedFiles.Add(item);
            return new Contribution(HasMatch: true, item.SizeBytes);
        }

        var matchedBytes = 0L;
        var hasMatchingDescendant = false;

        foreach (var child in item.Children)
        {
            var contribution = Visit(
                child,
                criteria,
                matchedFiles,
                matchedBytesByDirectory,
                ref unknownDateExclusionCount,
                cancellationToken);

            matchedBytes += contribution.MatchedBytes;
            hasMatchingDescendant |= contribution.HasMatch;
        }

        if (hasMatchingDescendant)
        {
            matchedBytesByDirectory[item] = matchedBytes;
        }

        return new Contribution(hasMatchingDescendant, matchedBytes);
    }

    private readonly record struct Contribution(bool HasMatch, long MatchedBytes)
    {
        public static Contribution None { get; } = new(false, 0);
    }

    private enum MatchOutcome
    {
        Matched,

        Excluded,

        ExcludedByUnknownDate
    }

    private sealed class Criteria
    {
        private readonly DiskItemFilter _filter;
        private readonly string? _textTerm;
        private readonly HashSet<string>? _extensions;
        private readonly HashSet<FileCategory>? _categories;
        private readonly DateTimeOffset? _createdAfter;
        private readonly DateTimeOffset? _createdBefore;
        private readonly DateTimeOffset? _modifiedAfter;
        private readonly DateTimeOffset? _modifiedBefore;
        private readonly DateTimeOffset? _lastAccessedAfter;
        private readonly DateTimeOffset? _lastAccessedBefore;

        public Criteria(DiskItemFilter filter, DateTimeOffset referenceTime)
        {
            _filter = filter;
            _createdAfter = filter.CreatedAfter?.Resolve(referenceTime);
            _createdBefore = filter.CreatedBefore?.Resolve(referenceTime);
            _modifiedAfter = filter.ModifiedAfter?.Resolve(referenceTime);
            _modifiedBefore = filter.ModifiedBefore?.Resolve(referenceTime);
            _lastAccessedAfter = filter.LastAccessedAfter?.Resolve(referenceTime);
            _lastAccessedBefore = filter.LastAccessedBefore?.Resolve(referenceTime);
            _textTerm = string.IsNullOrWhiteSpace(filter.TextTerm)
                ? null
                : filter.TextTerm.Trim();

            var normalizedExtensions = filter.NormalizedExtensions;
            _extensions = normalizedExtensions.Count == 0
                ? null
                : new HashSet<string>(normalizedExtensions, StringComparer.Ordinal);

            _categories = filter.Categories.Count == 0
                ? null
                : [.. filter.Categories];
        }

        public MatchOutcome Evaluate(DiskItem file)
        {
            if (_textTerm is not null
                && !file.Name.Contains(_textTerm, StringComparison.OrdinalIgnoreCase)
                && !file.Path.Contains(_textTerm, StringComparison.OrdinalIgnoreCase))
            {
                return MatchOutcome.Excluded;
            }

            if (_filter.MinimumSizeBytes is { } minimum && file.SizeBytes < minimum)
            {
                return MatchOutcome.Excluded;
            }

            if (_filter.MaximumSizeBytes is { } maximum && file.SizeBytes > maximum)
            {
                return MatchOutcome.Excluded;
            }

            if (_filter.SharedStorageOnly && !file.IsSizeCountedElsewhere)
            {
                return MatchOutcome.Excluded;
            }

            if (_extensions is not null || _categories is not null)
            {
                var extension = FileCategoryMap.NormalizeExtension(
                    Path.GetExtension(file.Name));

                var matchesExtension = _extensions is not null
                    && extension is not null
                    && _extensions.Contains(extension);

                var matchesCategory = _categories is not null
                    && FileCategoryMap.Find(extension) is { } category
                    && _categories.Contains(category);

                if (!matchesExtension && !matchesCategory)
                {
                    return MatchOutcome.Excluded;
                }
            }

            return EvaluateDates(file.Metadata);
        }

        private MatchOutcome EvaluateDates(DiskItemMetadata metadata)
        {
            var created = EvaluateRange(
                metadata.CreatedTimeUtc,
                _createdAfter,
                _createdBefore);
            if (created != MatchOutcome.Matched)
            {
                return created;
            }

            var modified = EvaluateRange(
                metadata.ModifiedTimeUtc,
                _modifiedAfter,
                _modifiedBefore);
            if (modified != MatchOutcome.Matched)
            {
                return modified;
            }

            return EvaluateRange(
                metadata.LastAccessTimeUtc,
                _lastAccessedAfter,
                _lastAccessedBefore);
        }

        private static MatchOutcome EvaluateRange(
            DateTimeOffset? value,
            DateTimeOffset? after,
            DateTimeOffset? before)
        {
            if (after is null && before is null)
            {
                return MatchOutcome.Matched;
            }

            if (value is not { } actual)
            {
                return MatchOutcome.ExcludedByUnknownDate;
            }

            if (after is { } lower && actual < lower)
            {
                return MatchOutcome.Excluded;
            }

            if (before is { } upper && actual > upper)
            {
                return MatchOutcome.Excluded;
            }

            return MatchOutcome.Matched;
        }
    }
}
