# Storage measurement semantics

MacStorageAtlas reports the size of the entries it successfully visits. Those
scan totals are not interchangeable with macOS volume-capacity numbers, and
allocated totals are not yet unique physical-storage totals.

## File-size terms

- **Logical size** is the file length visible to an application. Sparse regions,
  compressed storage, and cloud content that is not present locally can make
  this much larger than the storage currently allocated on the Mac.
- **Allocated file size** is the local filesystem allocation attributed to one
  visited file path. On macOS, MacStorageAtlas reads `st_blocks × 512` from file
  metadata.
- **Unique allocated size** counts storage once across file identities and
  shared physical extents in a stated scan scope. MacStorageAtlas does not yet
  report this value: hardlinks and APFS clones can cause allocated bytes to be
  counted for more than one path.

Logical and allocated measurement read metadata only. They do not read file
contents, contact a cloud provider, or request that an undownloaded placeholder
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
  deleting user-designated files. It can change as macOS manages caches and
  snapshots.

MacStorageAtlas does not currently display volume capacity, used, available, or
purgeable values. A folder or volume scan total must not be labeled as volume
used space.

## What contributes to a scan total

One measurement basis is captured when a scan starts and is retained with every
progress update and the completed result:

- In logical mode, each included file contributes its logical length.
- In allocated mode, each included path contributes its locally allocated
  bytes. The App uses this mode by default.
- A directory contributes the sum of its successfully measured descendants
  using the same basis.
- Hidden entries and symbolic links contribute only when their corresponding
  scan options include them.
- A collapsed `.app` package still includes the measured total of its
  descendants; only its presentation is collapsed.
- A metadata or access failure is listed as a scan error and contributes no
  invented value. Allocated mode does not silently substitute logical length
  when macOS allocation metadata is unavailable.
- A cancelled scan is incomplete. Any retained progress total contains only
  entries measured before cancellation.

The allocated reader in portable Core falls back to logical length on
unsupported development platforms. Released MacStorageAtlas targets use the
macOS allocation reader.

## Reproducible macOS fixtures

The following commands create one ordinary 1 MiB file and one sparse file with
a 1 GiB logical length:

```shell
fixture_dir=$(mktemp -d /tmp/MacStorageAtlas-measurement.XXXXXX)
mkfile 1m "$fixture_dir/normal.bin"
mkfile -n 1g "$fixture_dir/sparse.bin"

stat -f 'logical=%z bytes, allocated-blocks=%b' "$fixture_dir/normal.bin"
stat -f 'logical=%z bytes, allocated-blocks=%b' "$fixture_dir/sparse.bin"
du -k "$fixture_dir/normal.bin" "$fixture_dir/sparse.bin"
```

`stat` reports allocated blocks in 512-byte units, so multiply `%b` by 512 to
compare it with MacStorageAtlas allocated bytes. `du -k` normally reports the
same allocation rounded to KiB, but flags and platform variants can change its
scope or units.

Verified on 2026-07-24 using arm64 macOS 26.5.2 on APFS:

| Fixture | Logical bytes | Allocated bytes | `du -k` |
| --- | ---: | ---: | ---: |
| `normal.bin` | 1,048,576 | 1,048,576 | 1,024 |
| `sparse.bin` | 1,073,741,824 | 16,384 | 16 |

The exact sparse allocation can vary by filesystem and macOS version. The
architecture-independent observation is that logical mode reports 1 GiB while
allocated mode reports only the blocks locally committed to the sparse file.

Remove the fixture directory after inspection:

```shell
case "$fixture_dir" in
  /tmp/MacStorageAtlas-measurement.*) rm -R -- "$fixture_dir" ;;
  *) echo "Refusing to remove unexpected fixture path: $fixture_dir" ;;
esac
```

## Comparing other tools

Finder, `du`, and `stat` are useful comparison points only when the path, scope,
units, symlink behavior, and measurement basis match. Finder can round values or
show logical and on-disk values separately. Aggregate tools can also deduplicate
hardlinks or shared storage differently.

Equal content does not prove shared physical storage. Until the dedicated
hardlink and APFS-clone changes are implemented, MacStorageAtlas makes no claim
that its allocated total is the number of unique bytes that deleting a path
would reclaim.
