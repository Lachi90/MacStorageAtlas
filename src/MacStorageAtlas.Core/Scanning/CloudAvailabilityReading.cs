using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.Core.Scanning;

public readonly record struct CloudAvailabilityReading(
    CloudAvailability Availability,
    long LogicalSizeBytes)
{
    public static CloudAvailabilityReading Unknown { get; } =
        new(CloudAvailability.Unknown, 0);
}
