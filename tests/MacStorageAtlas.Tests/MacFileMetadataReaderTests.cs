using System.Buffers.Binary;
using MacStorageAtlas.Core;
using MacStorageAtlas.Platform.Mac;

namespace MacStorageAtlas.Tests;

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
                LinkCount: 1);
        }
    }
}
