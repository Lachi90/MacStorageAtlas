namespace MacStorageAtlas.Core.Scanning;

public interface ICloudAvailabilityReader
{
    CloudAvailabilityReading ReadCloudAvailability(string path);
}
