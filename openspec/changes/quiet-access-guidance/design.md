## Context

`AccessGuidanceClassifier` returned an `Indeterminate` guidance state whenever
the scan had no permission errors and the probe was inconclusive. The probe
listed `~/Library/Mail`, `~/Library/Messages`, and `~/Library/Safari` and
needed two successful listings to conclude that access was granted. Folders
that do not exist are skipped, so the probe was often inconclusive.

## Decisions

- Evidence first: guidance requires permission-related scan errors. Without
  them the scan read everything it walked, so a warning adds no information.
- The probe lists `/Library/Application Support/com.apple.TCC` first. It exists
  on every Mac and listing it fails with "Operation not permitted" unless the
  app has Full Disk Access, so the probe is conclusive even when the user's
  Mail, Messages, or Safari folders are missing. Only directory entries are
  enumerated, never file contents.
- The `Indeterminate` guidance state is removed instead of kept unused.
  `ScanCompleteness.Undetermined` stays in Core because stored snapshots can
  contain it.

## Alternatives considered

- Keep the banner but soften the wording. Rejected, because any banner without
  a blocked path is noise.
- Query the TCC database. Rejected, because reading it needs Full Disk Access
  itself and its schema is private.

## Risks / Trade-offs

- [Silent hiding by macOS] → Not known to occur. TCC refusals surface as scan
  errors and still produce guidance.

## Migration Plan

No data migration. Rolling back restores the previous banner.
