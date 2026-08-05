# Storage measurement semantics

MacStorageAtlas reports the size of entries it successfully visits. Scan totals
are not interchangeable with macOS volume-capacity numbers, unique physical
storage, or the bytes that deleting one path would reclaim.

## File-size terms

- **Logical size** is the file length visible to an application. Sparse regions,
  compressed storage, and cloud content that is not present locally can make
  this much larger than the storage currently allocated on the Mac.
- **Allocated file size** is the local filesystem allocation attributed to one
  visited file path.
- **Shared-aware allocated size** counts each filesystem file identity once in
  the scan scope. Where supported metadata verifies distinct identities as full
  clones of one data stream, it also counts that data allocation once.
  Allocation outside the verified data stream continues to count for each
  identity.
- **Unique allocated size** would count allocation once across every file
  identity and shared physical extent in a stated scope. MacStorageAtlas does
  not report this value.

Logical and allocated measurement use metadata only. Scanning does not compare
contents, hash files, open data forks for clone inspection, enumerate physical
extents, contact a cloud provider, or request that an undownloaded placeholder
be materialized.

## Volume-capacity terms

These terms describe a filesystem or APFS volume, not the sum of a scan tree:

- **Capacity** is the volume's total reported storage.
- **Used space** is capacity currently not reported as free. It includes
  filesystem metadata and storage outside or inaccessible to a scan.
- **Free space** is capacity currently reported as unallocated.
- **Available space** is capacity currently available for allocation. It can
  differ from free space because of reservations and reclaimable storage.
- **Purgeable space** is used capacity that macOS reports as reclaimable without
  deleting user-designated files.

MacStorageAtlas does not currently display volume capacity, used, available, or
purgeable values. A folder or volume scan total must not be labeled as volume
used space.

## What contributes to a scan total

One measurement basis is captured when a scan starts and retained with every
progress update and the completed result:

- In logical mode, each included path contributes its logical length.
- In allocated-per-path mode, each included path contributes its total local
  allocation.
- In shared-aware allocated mode, the first included path for a filesystem
  identity contributes its total local allocation. Repeated hardlinks and
  followed symbolic-link aliases contribute zero.
- The first included identity in a verified full-clone group contributes its
  full total. Each later full clone contributes its total allocation minus its
  verified shared data allocation.
- Non-data allocation, including resource-fork allocation, continues to
  contribute once for every distinct filesystem identity.
- Clone reference count classifies full-clone metadata; it never divides bytes.
- A full clone outside the selected scope does not suppress the first included
  contribution.
- Divergent clones that share only some physical extents contribute their full
  per-identity allocation.
- Missing, malformed, or inconsistent optional clone metadata fails closed:
  the affected identity contributes normally and coverage becomes partial.
- A directory contributes the additive measured, counted, and shared totals of
  its successfully measured descendants.
- Hidden entries and symbolic links contribute only when their scan options
  include them.
- A collapsed `.app` package still includes its descendants in accounting;
  only its presentation is collapsed.
- A required allocation or identity failure is listed as a scan error and
  contributes no invented or logical fallback value.
- A cancelled scan is incomplete and contains only entries measured before
  cancellation.

Every included path remains browsable. Item details distinguish its measured
allocation, counted contribution, and shared bytes. Treemaps, file-type totals,
largest-file ordering, progress, and directory totals use counted
contributions.

## Clone-accounting coverage

Shared-aware progress and completed results capture the coverage observed so
far:

- **Available** means every relevant observed allocated entry exposed the
  capability and complete clone metadata.
- **Unavailable** means none of the observed volumes exposed supported clone
  mapping. Hardlink accounting remains active.
- **Partial** means capable and incapable volumes were mixed, an optional
  metadata read degraded, or clone-group metadata was inconsistent.

Platform.Mac probes and caches `VOL_CAP_FMT_CLONE_MAPPING` by mounted-volume
identity. On a capable volume it uses one public `getattrlist(2)` read for
total allocation, data allocation, device, file identifier, link count, clone
identifier, clone reference count, returned attributes, and sharing flags. The
reader validates returned masks and buffer lengths before exposing an opaque
shared-data identity to Core.

macOS 11 through 13 and unsupported filesystems retain the `stat(2)` fallback
for total allocation and filesystem identity, so shared-aware mode still counts
hardlinks correctly while reporting clone accounting as unavailable. Optional
clone failures retain required allocation through the same fallback and report
partial coverage. Apple Silicon uses the native entry points; Intel preserves
the 64-bit-inode ABI entry points.

## Exported size fields

A CSV or JSON export reports three byte fields per item plus the mode that
produced them. One scan produces exactly one measurement basis, so an export
never reports a logical and an allocated size for the same item.

| Field | Logical mode | Allocated mode | Shared-aware allocated mode |
| --- | --- | --- | --- |
| `MeasuredSizeBytes` | Logical length | Allocated size | Allocated size |
| `CountedSizeBytes` | Logical length | Allocated size | Allocated size minus the bytes attributed to another included path |
| `SharedSizeBytes` | 0 | 0 | Bytes attributed to another included path |
| `IsSharedStorage` | false | false | True when `SharedSizeBytes` is above zero |

`MeasurementMode` repeats on every row so a row stays interpretable after a
spreadsheet sort or after rows from two exports are combined. A directory row
reports the totals of its own subtree, so summing every row would count each
file once per ancestor; the metadata byte total sums the file rows only and
equals the counted size of the scan root.

The same caveats apply to an export as to the on-screen totals: a shared-aware
number is not a promise of unique physical or reclaimable bytes, and shared
accounting is limited to the scanned scope.

## Reproducible macOS fixtures

The integration suite creates isolated temporary fixtures for an ordinary file,
a full clone, a clone made divergent by a small write, a hardlink, a sparse
file, and a full clone with independent resource-fork allocation. It gates
clone assertions on macOS and the advertised mounted-volume capability, ignores
unsupported environments with a reason, and removes its temporary directory.

The same shapes can be inspected manually on a capable APFS volume:

```shell
fixture_dir=$(mktemp -d /tmp/MacStorageAtlas-measurement.XXXXXX)
mkfile 1m "$fixture_dir/original.bin"
mkfile 1m "$fixture_dir/ordinary.bin"
cp -c "$fixture_dir/original.bin" "$fixture_dir/full-clone.bin"
cp -c "$fixture_dir/original.bin" "$fixture_dir/divergent-clone.bin"
printf '\1' | dd of="$fixture_dir/divergent-clone.bin" bs=1 seek=4096 conv=notrunc
ln "$fixture_dir/original.bin" "$fixture_dir/original-link.bin"
mkfile -n 1g "$fixture_dir/sparse.bin"
mkfile 1m "$fixture_dir/fork-source.bin"
cp -c "$fixture_dir/fork-source.bin" "$fixture_dir/fork-clone.bin"
mkfile 8k "$fixture_dir/fork-clone.bin/..namedfork/rsrc"

stat -f '%N device=%d inode=%i links=%l logical=%z blocks=%b' \
  "$fixture_dir"/*.bin
du -k "$fixture_dir"/*.bin
du -k -l "$fixture_dir"/*.bin
```

`stat` reports allocated blocks in 512-byte units. Multiply `%b` by 512 to
compare it with allocated bytes. Ordinary `du` deduplicates repeated hardlink
identities while `du -l` counts each path; aggregate tools may handle clone
sharing differently.

The baseline hardlink and sparse fixture was verified on 2026-07-24 using
arm64 macOS 26.5.2 on APFS:

| Fixture | Logical bytes | Allocated bytes | Link count | Ordinary `du -k` |
| --- | ---: | ---: | ---: | ---: |
| `normal.bin` | 1,048,576 | 1,048,576 | 2 | 1,024 |
| `normal-link.bin` | 1,048,576 | 1,048,576 | 2 | Deduplicated |
| `sparse.bin` | 1,073,741,824 | 16,384 | 1 | 16 |

Exact sparse and non-data allocation can vary by filesystem and macOS version.
The stable observations are that allocated modes do not substitute logical
length, hardlinks share one filesystem identity, verified full-clone data is
counted once only within the included scope, and divergent clone extents are
not deduplicated.

Remove the manual fixture after inspection:

```shell
case "$fixture_dir" in
  /tmp/MacStorageAtlas-measurement.*) rm -R -- "$fixture_dir" ;;
  *) echo "Refusing to remove unexpected fixture path: $fixture_dir" ;;
esac
```

## Comparing other tools and cleanup

Finder, `du`, and `stat` are useful comparison points only when path, scope,
units, symlink behavior, and measurement basis match. Equal content does not
prove shared physical storage, and a shared-aware total is not a promise of
unique physical or reclaimable bytes.

A hardlink or clone outside the selected scope can keep storage alive after an
included path is moved to Trash. After a successful Trash operation on a
shared-aware result, the App rescans with the captured options so another
included path can become the counted representative. Failed or cancelled Trash
operations leave the existing result unchanged.

## Moving and copying reclaim different amounts

Moving cleanup basket items to another location reclaims local bytes on the
source volume under the same rules as a Trash operation, so the App reports the
expected reclaimed size using the completed scan result's measurement mode and
rescans a shared-aware result after a successful move.

Copying reclaims nothing on the source volume, so the review reports an expected
reclaimed size of zero and the displayed scan result is left unchanged.

The bytes a copy adds at the destination are not the copied item's logical size
in every case. The App copies with APFS cloning enabled, so a copy that stays on
one APFS volume can create a clone that initially consumes almost no additional
space and only diverges as either copy is modified. A copy to a different volume,
or to a filesystem that cannot clone, writes the full content. Destination
free-space preflight compares against logical size, which is the conservative
figure in both cases.
