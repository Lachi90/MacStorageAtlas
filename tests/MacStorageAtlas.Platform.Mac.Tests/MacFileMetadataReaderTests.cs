using System.Buffers.Binary;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Platform.Mac;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Platform.Mac.Tests;

public sealed class MacFileMetadataReaderTests
{
    [Test]
    public void ReadReturnsVerifiedSharedIdentityFromSupportedMetadata()
    {
        RequireMacOs();
        var native = new FakeMacFileMetadataNative
        {
            Capability = CloneCapability.Supported,
            ExtendedMetadata = CompleteExtendedMetadata()
        };
        var reader = new MacFileMetadataReader(native);

        var metadata = reader.Read("/fixture/clone.bin");

        Assert.Multiple(() =>
        {
            Assert.That(metadata.AllocatedSizeBytes, Is.EqualTo(5120));
            Assert.That(metadata.DataAllocatedSizeBytes, Is.EqualTo(4096));
            Assert.That(
                metadata.SharedDataIdentity,
                Is.EqualTo(new SharedDataIdentity(7, 19)));
            Assert.That(
                metadata.CloneAccountingCoverage,
                Is.EqualTo(CloneAccountingCoverage.Available));
            Assert.That(native.FallbackReadCount, Is.Zero);
        });
    }

    [Test]
    public void ReadFallsBackWhenCloneMappingIsUnsupported()
    {
        RequireMacOs();
        var native = new FakeMacFileMetadataNative
        {
            Capability = CloneCapability.Unsupported
        };
        var reader = new MacFileMetadataReader(native);

        var metadata = reader.Read("/fixture/file.bin");

        Assert.Multiple(() =>
        {
            Assert.That(metadata.AllocatedSizeBytes, Is.EqualTo(8192));
            Assert.That(metadata.Identity, Is.EqualTo(new FileIdentity(7, 23)));
            Assert.That(metadata.DataAllocatedSizeBytes, Is.Null);
            Assert.That(metadata.SharedDataIdentity, Is.Null);
            Assert.That(
                metadata.CloneAccountingCoverage,
                Is.EqualTo(CloneAccountingCoverage.Unavailable));
        });
    }

    [Test]
    public void ReadRetainsRequiredExtendedMetadataWhenCloneFieldsAreIncomplete()
    {
        RequireMacOs();
        var complete = CompleteExtendedMetadata();
        var native = new FakeMacFileMetadataNative
        {
            Capability = CloneCapability.Supported,
            ExtendedMetadata = complete with { HasCloneId = false }
        };
        var reader = new MacFileMetadataReader(native);

        var metadata = reader.Read("/fixture/file.bin");

        Assert.Multiple(() =>
        {
            Assert.That(metadata.AllocatedSizeBytes, Is.EqualTo(5120));
            Assert.That(metadata.Identity, Is.EqualTo(new FileIdentity(7, 17)));
            Assert.That(metadata.SharedDataIdentity, Is.Null);
            Assert.That(
                metadata.CloneAccountingCoverage,
                Is.EqualTo(CloneAccountingCoverage.Partial));
            Assert.That(native.FallbackReadCount, Is.Zero);
        });
    }

    [TestCase((int)CloneCapability.Degraded)]
    [TestCase((int)CloneCapability.Supported)]
    public void ReadUsesRequiredFallbackForDegradedOrMalformedExtendedMetadata(
        int capabilityValue)
    {
        RequireMacOs();
        var capability = (CloneCapability)capabilityValue;
        var native = new FakeMacFileMetadataNative
        {
            Capability = capability,
            ExtendedMetadata = capability == CloneCapability.Supported
                ? CompleteExtendedMetadata() with { HasRequiredMetadata = false }
                : null
        };
        var reader = new MacFileMetadataReader(native);

        var metadata = reader.Read("/fixture/file.bin");

        Assert.Multiple(() =>
        {
            Assert.That(metadata.AllocatedSizeBytes, Is.EqualTo(8192));
            Assert.That(native.FallbackReadCount, Is.EqualTo(1));
            Assert.That(
                metadata.CloneAccountingCoverage,
                Is.EqualTo(CloneAccountingCoverage.Partial));
        });
    }

    [Test]
    public void ReadPropagatesRequiredFallbackFailure()
    {
        RequireMacOs();
        var native = new FakeMacFileMetadataNative
        {
            Capability = CloneCapability.Unsupported,
            FallbackException = new IOException("Required metadata failed.")
        };
        var reader = new MacFileMetadataReader(native);

        Assert.That(
            () => reader.Read("/fixture/file.bin"),
            Throws.InstanceOf<IOException>());
    }

    [Test]
    public void ReadCachesCapabilityByMountedVolumeIdentity()
    {
        RequireMacOs();
        var native = new FakeMacFileMetadataNative
        {
            Capability = CloneCapability.Unsupported
        };
        var reader = new MacFileMetadataReader(native);

        reader.Read("/fixture/first.bin");
        reader.Read("/fixture/second.bin");

        Assert.That(native.CapabilityProbeCount, Is.EqualTo(1));
    }

    [Test]
    public void TryParseExtendedBufferReadsFixedWidthFieldsAndReturnedMasks()
    {
        var buffer = CompleteExtendedBuffer();

        var parsed = MacFileMetadataNative.TryParseExtendedBuffer(
            buffer,
            out var metadata);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(metadata.HasRequiredMetadata, Is.True);
            Assert.That(metadata.HasCloneMetadata, Is.True);
            Assert.That(metadata.DeviceId, Is.EqualTo(7));
            Assert.That(metadata.FileId, Is.EqualTo(17));
            Assert.That(metadata.LinkCount, Is.EqualTo(2));
            Assert.That(metadata.AllocatedSizeBytes, Is.EqualTo(5120));
            Assert.That(metadata.DataAllocatedSizeBytes, Is.EqualTo(4096));
            Assert.That(metadata.CloneId, Is.EqualTo(19));
            Assert.That(metadata.ExtendedFlags, Is.EqualTo(0x40));
            Assert.That(metadata.CloneReferenceCount, Is.EqualTo(2));
        });
    }

    [TestCase(3)]
    [TestCase(77)]
    public void TryParseExtendedBufferRejectsMalformedReturnedLength(int returnedLength)
    {
        var buffer = CompleteExtendedBuffer();
        BinaryPrimitives.WriteUInt32LittleEndian(
            buffer.AsSpan(0, sizeof(uint)),
            (uint)returnedLength);

        var parsed = MacFileMetadataNative.TryParseExtendedBuffer(
            buffer,
            out _);

        Assert.That(parsed, Is.False);
    }

    [Test]
    public void ReadCloudAvailabilityReportsCloudOnlyWhenTheDatalessFlagIsSet()
    {
        RequireMacOs();
        var native = new FakeMacFileMetadataNative
        {
            Status = new MacFileStatus(0x40000000 | 0x20, 7_340_032)
        };
        var reader = new MacFileMetadataReader(native);

        var reading = reader.ReadCloudAvailability("/fixture/placeholder.pdf");

        Assert.Multiple(() =>
        {
            Assert.That(reading.Availability, Is.EqualTo(CloudAvailability.CloudOnly));
            Assert.That(reading.LogicalSizeBytes, Is.EqualTo(7_340_032));
        });
    }

    [Test]
    public void ReadCloudAvailabilityReportsLocalWhenTheDatalessFlagIsClear()
    {
        RequireMacOs();
        var native = new FakeMacFileMetadataNative
        {
            Status = new MacFileStatus(0x20, 4096)
        };
        var reader = new MacFileMetadataReader(native);

        var reading = reader.ReadCloudAvailability("/fixture/local.pdf");

        Assert.That(reading.Availability, Is.EqualTo(CloudAvailability.Local));
    }

    [Test]
    public void ReadCloudAvailabilityReportsUnknownWhenStatusCannotBeRead()
    {
        RequireMacOs();
        var reader = new MacFileMetadataReader(new FakeMacFileMetadataNative());

        var reading = reader.ReadCloudAvailability("/fixture/missing.pdf");

        Assert.That(reading, Is.EqualTo(CloudAvailabilityReading.Unknown));
    }

    [Test]
    public void ReadCarriesCloudAvailabilityFromExtendedMetadataWithoutAnotherStatusCall()
    {
        RequireMacOs();
        var native = new FakeMacFileMetadataNative
        {
            Capability = CloneCapability.Supported,
            ExtendedMetadata = CompleteExtendedMetadata() with
            {
                HasFileFlags = true,
                FileFlags = 0x40000000,
                HasTotalSize = true,
                TotalSizeBytes = 9_000_000
            }
        };
        var reader = new MacFileMetadataReader(native);

        var metadata = reader.Read("/fixture/placeholder.bin");

        Assert.Multiple(() =>
        {
            Assert.That(
                metadata.CloudAvailability,
                Is.EqualTo(new CloudAvailabilityReading(CloudAvailability.CloudOnly, 9_000_000)));
            Assert.That(native.StatusReadCount, Is.Zero);
            Assert.That(native.FallbackReadCount, Is.Zero);
        });
    }

    [Test]
    public void ReadLeavesCloudAvailabilityUnsetWhenExtendedMetadataLacksFlags()
    {
        RequireMacOs();
        var native = new FakeMacFileMetadataNative
        {
            Capability = CloneCapability.Supported,
            ExtendedMetadata = CompleteExtendedMetadata()
        };
        var reader = new MacFileMetadataReader(native);

        var metadata = reader.Read("/fixture/file.bin");

        Assert.That(metadata.CloudAvailability, Is.Null);
    }

    [Test]
    public void ReadCarriesCloudAvailabilityFromTheFallbackStatus()
    {
        RequireMacOs();
        var native = new FakeMacFileMetadataNative
        {
            Capability = CloneCapability.Unsupported,
            FallbackFlags = 0x40000000,
            FallbackLogicalSizeBytes = 2048
        };
        var reader = new MacFileMetadataReader(native);

        var metadata = reader.Read("/fixture/placeholder.bin");

        Assert.That(
            metadata.CloudAvailability,
            Is.EqualTo(new CloudAvailabilityReading(CloudAvailability.CloudOnly, 2048)));
    }

    [Test]
    public void TryParseExtendedBufferReadsFileFlagsAndTotalSizeInAttributeOrder()
    {
        var buffer = new byte[88];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), 88);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4, 4), 0x82040002);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(16, 4), 0x407);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(20, 4), 0x1300);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(24, 4), 7);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(28, 4), 0x40000000);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(32, 8), 17);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(40, 4), 1);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(44, 8), 123_456);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(52, 8), 0);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(60, 8), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(68, 8), 19);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(76, 8), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(84, 4), 1);

        var parsed = MacFileMetadataNative.TryParseExtendedBuffer(
            buffer,
            out var metadata);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(metadata.HasRequiredMetadata, Is.True);
            Assert.That(metadata.HasFileFlags, Is.True);
            Assert.That(metadata.FileFlags, Is.EqualTo(0x40000000));
            Assert.That(metadata.FileId, Is.EqualTo(17));
            Assert.That(metadata.HasTotalSize, Is.True);
            Assert.That(metadata.TotalSizeBytes, Is.EqualTo(123_456));
            Assert.That(metadata.AllocatedSizeBytes, Is.Zero);
            Assert.That(metadata.CloneId, Is.EqualTo(19));
        });
    }

    private static MacExtendedFileMetadata CompleteExtendedMetadata() =>
        new(
            HasRequiredMetadata: true,
            HasDataAllocatedSize: true,
            HasCloneId: true,
            HasExtendedFlags: true,
            HasCloneReferenceCount: true,
            AllocatedSizeBytes: 5120,
            DataAllocatedSizeBytes: 4096,
            DeviceId: 7,
            FileId: 17,
            LinkCount: 2,
            CloneId: 19,
            ExtendedFlags: 0x40,
            CloneReferenceCount: 2);

    private static byte[] CompleteExtendedBuffer()
    {
        var buffer = new byte[76];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), 76);
        BinaryPrimitives.WriteUInt32LittleEndian(
            buffer.AsSpan(4, 4),
            0x82000002);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(16, 4), 0x405);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(20, 4), 0x1300);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(24, 4), 7);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(28, 8), 17);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(36, 4), 2);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(40, 8), 5120);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(48, 8), 4096);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(56, 8), 19);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(64, 8), 0x40);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(72, 4), 2);
        return buffer;
    }

    private static void RequireMacOs()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Ignore("macOS-specific metadata reader behavior.");
        }
    }

    private sealed class FakeMacFileMetadataNative : IMacFileMetadataNative
    {
        public CloneCapability Capability { get; init; }

        public MacExtendedFileMetadata? ExtendedMetadata { get; init; }

        public IOException? FallbackException { get; init; }

        public MacFileStatus? Status { get; init; }

        public uint FallbackFlags { get; init; }

        public long FallbackLogicalSizeBytes { get; init; }

        public int StatusReadCount { get; private set; }

        public int CapabilityProbeCount { get; private set; }

        public int FallbackReadCount { get; private set; }

        public MacVolumeInfo? TryGetVolume(string path) =>
            new(new MacVolumeIdentity(101, 202), "/fixture");

        public CloneCapability ProbeCloneCapability(string mountPoint)
        {
            CapabilityProbeCount++;
            return Capability;
        }

        public MacExtendedFileMetadata? TryReadExtended(string path) =>
            ExtendedMetadata;

        public MacFallbackFileMetadata ReadFallback(string path)
        {
            FallbackReadCount++;
            if (FallbackException is not null)
            {
                throw FallbackException;
            }

            return new MacFallbackFileMetadata(
                AllocatedSizeBytes: 8192,
                DeviceId: 7,
                FileId: 23,
                LinkCount: 1,
                FallbackFlags,
                FallbackLogicalSizeBytes);
        }

        public MacFileStatus? TryReadStatus(string path)
        {
            StatusReadCount++;
            return Status;
        }
    }
}
