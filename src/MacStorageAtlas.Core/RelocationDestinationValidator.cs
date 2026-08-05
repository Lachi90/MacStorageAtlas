namespace MacStorageAtlas.Core;

public sealed class RelocationDestinationValidator(IRelocationDestinationProbe probe)
{
    public RelocationDestinationValidation Validate(
        RelocationDestination destination,
        long requiredBytes)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(requiredBytes);

        var path = destination.NormalizedPath;

        if (!probe.Exists(path))
        {
            return RelocationDestinationValidation.Blocked(
                RelocationDestinationStatusKind.Missing,
                "The destination no longer exists.",
                RelocationFreeSpace.Unknown);
        }

        if (!probe.IsDirectory(path))
        {
            return RelocationDestinationValidation.Blocked(
                RelocationDestinationStatusKind.NotADirectory,
                "The destination is not a folder.",
                RelocationFreeSpace.Unknown);
        }

        if (!probe.IsWritable(path))
        {
            return RelocationDestinationValidation.Blocked(
                RelocationDestinationStatusKind.NotWritable,
                "The destination cannot be written to.",
                RelocationFreeSpace.Unknown);
        }

        var freeSpace = probe.GetFreeSpace(path);
        if (freeSpace.IsKnown && requiredBytes > freeSpace.AvailableBytes)
        {
            return RelocationDestinationValidation.Blocked(
                RelocationDestinationStatusKind.InsufficientFreeSpace,
                $"The destination does not have enough free space. "
                + $"{FileSizeFormatter.Format(requiredBytes)} is needed and "
                + $"{FileSizeFormatter.Format(freeSpace.AvailableBytes)} is available.",
                freeSpace);
        }

        return RelocationDestinationValidation.Ready(freeSpace);
    }
}
