using System;
using System.IO;

namespace MacStorageAtlas.App.Services;

public static class ScanHistoryStoreLocation
{
    public static string Default()
    {
        var applicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolderOption.Create);

        return Path.Combine(applicationData, "MacStorageAtlas", "history");
    }
}
