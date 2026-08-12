using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using MacStorageAtlas.Core.Export;

namespace MacStorageAtlas.App.Services;

public sealed class AvaloniaSaveFilePickerService(IStorageProvider storageProvider)
    : ISaveFilePickerService
{
    private static readonly FilePickerFileType CsvFileType = new("Comma-separated values")
    {
        Patterns = ["*.csv"],
        MimeTypes = ["text/csv"],
        AppleUniformTypeIdentifiers = ["public.comma-separated-values-text"]
    };

    private static readonly FilePickerFileType JsonFileType = new("JSON")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
        AppleUniformTypeIdentifiers = ["public.json"]
    };

    public async Task<string?> SelectSaveFileAsync(
        ScanExportFormat format,
        string suggestedFileName)
    {
        var isCsv = format == ScanExportFormat.Csv;
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = isCsv ? "Export scan result as CSV" : "Export scan result as JSON",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = isCsv ? "csv" : "json",
            FileTypeChoices = [isCsv ? CsvFileType : JsonFileType],
            ShowOverwritePrompt = true
        });

        return file?.TryGetLocalPath();
    }
}
