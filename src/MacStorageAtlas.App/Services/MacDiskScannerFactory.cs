using MacStorageAtlas.Core.Scanning;
using MacStorageAtlas.Platform.Mac;

namespace MacStorageAtlas.App.Services;

public static class MacDiskScannerFactory
{
    public static DiskScanner Create()
    {
        var metadataReader = new MacFileMetadataReader();
        return new DiskScanner(
            metadataReader,
            new MacFirmlinkEquivalentPathProvider(),
            metadataReader);
    }
}
