using System.IO;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public class ScanResultCsvWriterTests
{
    private static readonly DateTimeOffset Created =
        new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset Modified =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task TheHeaderNamesAndOrderAreTheDocumentedFieldList()
    {
        var csv = await WriteAsync([]);

        Assert.Multiple(() =>
        {
            Assert.That(
                csv,
                Is.EqualTo(
                    "Path,Name,Kind,Depth,MeasurementMode,MeasuredSizeBytes,"
                    + "CountedSizeBytes,SharedSizeBytes,IsSharedStorage,Extension,"
                    + "Category,CreatedUtc,ModifiedUtc,LastAccessedUtc\r\n"));
            Assert.That(ScanResultCsvWriter.Headers, Has.Count.EqualTo(14));
        });
    }

    [Test]
    public async Task AFileRowStatesEveryFieldInOrder()
    {
        var csv = await WriteAsync([
            new ScanExportRow(
                "/scan/videos/clip.mov",
                "clip.mov",
                DiskItemKind.File,
                Depth: 2,
                StorageMeasurementMode.SharedAwareAllocated,
                MeasuredSizeBytes: 4096,
                CountedSizeBytes: 1024,
                SharedSizeBytes: 3072,
                IsSharedStorage: true,
                ".mov",
                FileCategory.Video,
                Created,
                Modified,
                LastAccessedUtc: null)
        ]);

        Assert.That(
            RawBody(csv),
            Is.EqualTo(
                "/scan/videos/clip.mov,clip.mov,File,2,SharedAwareAllocated,4096,1024,3072,"
                + "true,.mov,Video,2026-01-02T03:04:05.0000000Z,"
                + "2026-07-30T12:00:00.0000000Z,\r\n"));
    }

    [Test]
    public async Task ANameContainingSeparatorCharactersIsQuotedAndEscaped()
    {
        var csv = await WriteAsync([
            FileRow("/scan/a, \"b\"\nc.txt", "a, \"b\"\nc.txt")
        ]);

        Assert.That(
            RawBody(csv),
            Does.StartWith(
                "\"/scan/a, \"\"b\"\"\nc.txt\",\"a, \"\"b\"\"\nc.txt\",File,1,"));
    }

    [Test]
    public async Task AQuotedFieldContainingLineBreaksStaysOneLogicalRow()
    {
        var csv = await WriteAsync([
            FileRow("/scan/a\nb.txt", "a\nb.txt"),
            FileRow("/scan/plain.txt", "plain.txt")
        ]);

        var fields = ParseCsv(csv);

        Assert.Multiple(() =>
        {
            Assert.That(fields, Has.Count.EqualTo(3));
            Assert.That(fields[1][0], Is.EqualTo("/scan/a\nb.txt"));
            Assert.That(fields[1], Has.Length.EqualTo(14));
            Assert.That(fields[2][0], Is.EqualTo("/scan/plain.txt"));
        });
    }

    [TestCase('=')]
    [TestCase('+')]
    [TestCase('-')]
    [TestCase('@')]
    [TestCase('\t')]
    public async Task ANameBeginningWithAFormulaCharacterIsNeutralized(char trigger)
    {
        var name = $"{trigger}danger.txt";
        var csv = await WriteAsync([FileRow($"/scan/{name}", name)]);

        var fields = ParseCsv(csv)[1];

        Assert.Multiple(() =>
        {
            Assert.That(fields[1], Does.StartWith("'"));
            Assert.That(fields[1], Is.EqualTo($"'{name}"));
            Assert.That(fields[0], Is.EqualTo($"'/scan/{name}").Or.EqualTo($"/scan/{name}"));
        });
    }

    [Test]
    public async Task ACarriageReturnLeadingANameIsBothNeutralizedAndQuoted()
    {
        var csv = await WriteAsync([FileRow("/scan/\rname.txt", "\rname.txt")]);

        Assert.That(RawBody(csv), Does.Contain("\"'\rname.txt\""));
    }

    [Test]
    public async Task AnExtensionBeginningWithAFormulaCharacterIsNeutralized()
    {
        var row = FileRow("/scan/odd", "odd") with { Extension = "-weird" };

        var csv = await WriteAsync([row]);

        Assert.That(ParseCsv(csv)[1][9], Is.EqualTo("'-weird"));
    }

    [Test]
    public async Task NonAsciiNamesArePreservedVerbatim()
    {
        var csv = await WriteAsync([FileRow("/scan/Übersicht — 日本語.txt", "Übersicht — 日本語.txt")]);

        Assert.That(ParseCsv(csv)[1][1], Is.EqualTo("Übersicht — 日本語.txt"));
    }

    [Test]
    public async Task UnknownTimestampsAreWrittenAsEmptyFields()
    {
        var row = FileRow("/scan/a.txt", "a.txt") with
        {
            CreatedUtc = null,
            ModifiedUtc = null,
            LastAccessedUtc = null
        };

        var csv = await WriteAsync([row]);
        var fields = ParseCsv(csv)[1];

        Assert.Multiple(() =>
        {
            Assert.That(fields[11], Is.Empty);
            Assert.That(fields[12], Is.Empty);
            Assert.That(fields[13], Is.Empty);
        });
    }

    [Test]
    public async Task ADirectoryRowLeavesExtensionAndCategoryEmpty()
    {
        var csv = await WriteAsync([
            new ScanExportRow(
                "/scan/videos",
                "videos",
                DiskItemKind.Directory,
                Depth: 1,
                StorageMeasurementMode.Logical,
                MeasuredSizeBytes: 3000,
                CountedSizeBytes: 3000,
                SharedSizeBytes: 0,
                IsSharedStorage: false,
                string.Empty,
                Category: null,
                Created,
                Modified,
                Modified)
        ]);

        var fields = ParseCsv(csv)[1];

        Assert.Multiple(() =>
        {
            Assert.That(fields[2], Is.EqualTo("Directory"));
            Assert.That(fields[8], Is.EqualTo("false"));
            Assert.That(fields[9], Is.Empty);
            Assert.That(fields[10], Is.Empty);
        });
    }

    [Test]
    public async Task ASharedStorageRowReportsItsSharedBytesAndFlag()
    {
        var row = FileRow("/scan/clone.bin", "clone.bin") with
        {
            MeasuredSizeBytes = 8192,
            CountedSizeBytes = 0,
            SharedSizeBytes = 8192,
            IsSharedStorage = true
        };

        var csv = await WriteAsync([row]);
        var fields = ParseCsv(csv)[1];

        Assert.Multiple(() =>
        {
            Assert.That(fields[6], Is.EqualTo("0"));
            Assert.That(fields[7], Is.EqualTo("8192"));
            Assert.That(fields[8], Is.EqualTo("true"));
        });
    }

    [Test]
    public async Task EveryRowRepeatsTheMeasurementMode()
    {
        var csv = await WriteAsync([
            FileRow("/scan/a.txt", "a.txt"),
            FileRow("/scan/b.txt", "b.txt")
        ]);

        var rows = ParseCsv(csv).Skip(1).ToArray();

        Assert.That(rows.Select(fields => fields[4]).ToArray(), Is.EqualTo(new[]
        {
            "SharedAwareAllocated",
            "SharedAwareAllocated"
        }));
    }

    [Test]
    public async Task AnEmptyResultWritesOnlyTheHeader()
    {
        var csv = await WriteAsync([]);

        Assert.That(RawBody(csv), Is.Empty);
    }

    [Test]
    public void CancellationStopsWritingRows()
    {
        var writer = new StringWriter();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() =>
            ScanResultCsvWriter.WriteAsync(
                new ScanExportRequest(Metadata(), [FileRow("/scan/a.txt", "a.txt")]),
                writer,
                cancellation.Token));
    }

    private static async Task<string> WriteAsync(IEnumerable<ScanExportRow> rows)
    {
        var writer = new StringWriter();
        await ScanResultCsvWriter.WriteAsync(new ScanExportRequest(Metadata(), rows), writer);
        return writer.ToString();
    }

    private static ScanExportRow FileRow(string path, string name) =>
        new(
            path,
            name,
            DiskItemKind.File,
            Depth: 1,
            StorageMeasurementMode.SharedAwareAllocated,
            MeasuredSizeBytes: 100,
            CountedSizeBytes: 100,
            SharedSizeBytes: 0,
            IsSharedStorage: false,
            ".txt",
            FileCategory.Document,
            Created,
            Modified,
            Modified);

    private static ScanExportMetadata Metadata() =>
        new(
            "/scan",
            Modified,
            ScanOptions.Default,
            StorageMeasurementMode.SharedAwareAllocated,
            CloneAccountingCoverage.Unavailable,
            ScanExportScope.Full,
            Filter: null,
            ItemCount: 0,
            TotalCountedSizeBytes: 0);

    private static string RawBody(string csv) =>
        csv[(csv.IndexOf("\r\n", StringComparison.Ordinal) + 2)..];

    private static List<string[]> ParseCsv(string csv)
    {
        var rows = new List<string[]>();
        var fields = new List<string>();
        var field = new System.Text.StringBuilder();
        var quoted = false;

        for (var index = 0; index < csv.Length; index++)
        {
            var current = csv[index];

            if (quoted)
            {
                if (current == '"')
                {
                    if (index + 1 < csv.Length && csv[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                        continue;
                    }

                    quoted = false;
                    continue;
                }

                field.Append(current);
                continue;
            }

            switch (current)
            {
                case '"':
                    quoted = true;
                    continue;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    continue;
                case '\r' when index + 1 < csv.Length && csv[index + 1] == '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    rows.Add(fields.ToArray());
                    fields.Clear();
                    index++;
                    continue;
                default:
                    field.Append(current);
                    continue;
            }
        }

        return rows;
    }
}
