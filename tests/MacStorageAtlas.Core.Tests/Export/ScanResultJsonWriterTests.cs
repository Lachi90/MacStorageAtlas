using System.IO;
using System.Text;
using System.Text.Json;
using MacStorageAtlas.Core.Export;
using MacStorageAtlas.Core.Filtering;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Tests.Export;

public class ScanResultJsonWriterTests
{
    private static readonly DateTimeOffset Created =
        new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset Completed =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task TheEnvelopeStatesItsSchemaVersionAndScanMetadata()
    {
        var json = await WriteAsync(Metadata(), []);
        using var document = JsonDocument.Parse(json);
        var scan = document.RootElement.GetProperty("scan");

        Assert.Multiple(() =>
        {
            Assert.That(
                document.RootElement.GetProperty("schemaVersion").GetInt32(),
                Is.EqualTo(1));
            Assert.That(scan.GetProperty("rootPath").GetString(), Is.EqualTo("/scan"));
            Assert.That(
                scan.GetProperty("completedAt").GetString(),
                Is.EqualTo("2026-07-30T12:00:00.0000000Z"));
            Assert.That(
                scan.GetProperty("measurementMode").GetString(),
                Is.EqualTo("SharedAwareAllocated"));
            Assert.That(
                scan.GetProperty("cloneAccountingCoverage").GetString(),
                Is.EqualTo("Available"));
            Assert.That(scan.GetProperty("scope").GetString(), Is.EqualTo("Full"));
            Assert.That(
                scan.GetProperty("options").GetProperty("treatPackagesAsDirectories")
                    .GetBoolean(),
                Is.True);
        });
    }

    [Test]
    public async Task AFilterThatMatchesNothingStillWritesItsEnvelope()
    {
        var metadata = Metadata() with
        {
            Scope = ScanExportScope.Filtered,
            Filter = new DiskItemFilter { MinimumSizeBytes = 1_000_000_000 },
            ItemCount = 0,
            TotalCountedSizeBytes = 0
        };

        var json = await WriteAsync(metadata, []);
        using var document = JsonDocument.Parse(json);

        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("items").GetArrayLength(), Is.Zero);
            Assert.That(
                document.RootElement.GetProperty("scan").GetProperty("scope").GetString(),
                Is.EqualTo("Filtered"));
            Assert.That(
                document.RootElement.GetProperty("scan").GetProperty("filter")
                    .GetProperty("minimumSizeBytes").GetInt64(),
                Is.EqualTo(1_000_000_000));
        });
    }

    [Test]
    public async Task RecoverableScanErrorsAreListedWithTheirPaths()
    {
        var json = await WriteAsync(
            Metadata(),
            [],
            [
                new ScanError("/scan/private", "Access denied.", "UnauthorizedAccessException"),
                new ScanError("/scan/gone", "Not found.", "DirectoryNotFoundException")
            ]);

        using var document = JsonDocument.Parse(json);
        var errors = document.RootElement.GetProperty("errors");

        Assert.Multiple(() =>
        {
            Assert.That(errors.GetArrayLength(), Is.EqualTo(2));
            Assert.That(errors[0].GetProperty("path").GetString(), Is.EqualTo("/scan/private"));
            Assert.That(
                errors[0].GetProperty("exceptionType").GetString(),
                Is.EqualTo("UnauthorizedAccessException"));
            Assert.That(errors[1].GetProperty("message").GetString(), Is.EqualTo("Not found."));
        });
    }

    [Test]
    public async Task AScanWithoutErrorsWritesAnEmptyErrorList()
    {
        var json = await WriteAsync(Metadata(), []);
        using var document = JsonDocument.Parse(json);

        Assert.That(document.RootElement.GetProperty("errors").GetArrayLength(), Is.Zero);
    }

    [Test]
    public async Task APathBeginningWithAFormulaCharacterIsWrittenExactly()
    {
        var row = FileRow("/scan/=danger.txt", "=danger.txt");

        var json = await WriteAsync(Metadata(), [row]);
        using var document = JsonDocument.Parse(json);
        var item = document.RootElement.GetProperty("items")[0];

        Assert.Multiple(() =>
        {
            Assert.That(item.GetProperty("path").GetString(), Is.EqualTo("/scan/=danger.txt"));
            Assert.That(item.GetProperty("name").GetString(), Is.EqualTo("=danger.txt"));
        });
    }

    [Test]
    public async Task UnknownTimestampsAndCategoriesAreWrittenAsNull()
    {
        var row = FileRow("/scan/a", "a") with
        {
            Extension = string.Empty,
            Category = null,
            CreatedUtc = null,
            ModifiedUtc = null,
            LastAccessedUtc = null
        };

        var json = await WriteAsync(Metadata(), [row]);
        using var document = JsonDocument.Parse(json);
        var item = document.RootElement.GetProperty("items")[0];

        Assert.Multiple(() =>
        {
            Assert.That(item.GetProperty("category").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(item.GetProperty("createdUtc").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(
                item.GetProperty("lastAccessedUtc").ValueKind,
                Is.EqualTo(JsonValueKind.Null));
        });
    }

    [Test]
    public async Task AnExportReadsBackUnchanged()
    {
        var metadata = Metadata() with
        {
            Scope = ScanExportScope.Filtered,
            Filter = new DiskItemFilter
            {
                TextTerm = "report",
                MinimumSizeBytes = 1024,
                MaximumSizeBytes = 2048,
                CreatedAfter = new AbsoluteDateCriterion(Created),
                ModifiedBefore = new RelativeDateCriterion(18, RelativeDateUnit.Months),
                Extensions = [".zip", ".mov"],
                Categories = [FileCategory.Archive, FileCategory.Video],
                SharedStorageOnly = true
            },
            ItemCount = 2,
            TotalCountedSizeBytes = 300
        };
        var rows = new[]
        {
            FileRow("/scan/=danger.txt", "=danger.txt"),
            FileRow("/scan/Übersicht.txt", "Übersicht.txt") with
            {
                CreatedUtc = null,
                LastAccessedUtc = null,
                Category = null,
                Extension = string.Empty
            }
        };
        var errors = new[]
        {
            new ScanError("/scan/private", "Access denied.", "UnauthorizedAccessException")
        };

        var json = await WriteAsync(metadata, rows, errors);
        var document = ScanResultJsonReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        Assert.Multiple(() =>
        {
            Assert.That(document.Metadata, Is.EqualTo(metadata));
            Assert.That(document.Items, Is.EqualTo(rows));
            Assert.That(document.Errors, Is.EqualTo(errors));
        });
    }

    [Test]
    public async Task AFullExportWithoutAFilterReadsBackWithoutOne()
    {
        var json = await WriteAsync(Metadata(), [FileRow("/scan/a.txt", "a.txt")]);

        var document = ScanResultJsonReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        Assert.Multiple(() =>
        {
            Assert.That(document.Metadata.Filter, Is.Null);
            Assert.That(document.Metadata.Scope, Is.EqualTo(ScanExportScope.Full));
            Assert.That(document.Metadata.Options.MeasurementMode, Is.EqualTo(
                StorageMeasurementMode.SharedAwareAllocated));
        });
    }

    [Test]
    public void CancellationStopsWritingItems()
    {
        using var stream = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() =>
            ScanResultJsonWriter.WriteAsync(
                new ScanExportRequest(Metadata(), [FileRow("/scan/a.txt", "a.txt")]),
                stream,
                cancellation.Token));
    }

    private static async Task<string> WriteAsync(
        ScanExportMetadata metadata,
        IEnumerable<ScanExportRow> rows,
        IReadOnlyList<ScanError>? errors = null)
    {
        using var stream = new MemoryStream();
        await ScanResultJsonWriter.WriteAsync(
            new ScanExportRequest(metadata, rows, errors),
            stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static ScanExportRow FileRow(string path, string name) =>
        new(
            path,
            name,
            DiskItemKind.File,
            Depth: 1,
            StorageMeasurementMode.SharedAwareAllocated,
            MeasuredSizeBytes: 200,
            CountedSizeBytes: 150,
            SharedSizeBytes: 50,
            IsSharedStorage: true,
            ".txt",
            FileCategory.Document,
            Created,
            Completed,
            Completed);

    private static ScanExportMetadata Metadata() =>
        new(
            "/scan",
            Completed,
            ScanOptions.Default with
            {
                MeasurementMode = StorageMeasurementMode.SharedAwareAllocated
            },
            StorageMeasurementMode.SharedAwareAllocated,
            CloneAccountingCoverage.Available,
            ScanExportScope.Full,
            Filter: null,
            ItemCount: 0,
            TotalCountedSizeBytes: 0);
}
