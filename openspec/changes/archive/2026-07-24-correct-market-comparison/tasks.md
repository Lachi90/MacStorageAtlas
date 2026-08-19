## 1. Verify current claims

- [x] 1.1 Recheck every retained competitor capability against a current official primary source and record the verification date.
- [x] 1.2 Compare README wording with `DiskScanner` and `NativeFileSize` so allocated-block behavior is not described as hardlink/clone-aware unique storage.

## 2. Update public documentation

- [x] 2.1 Rewrite the README comparison with qualified storage-measurement wording, official source links, and a visible last-verified date.
- [x] 2.2 Review the completed comparison to ensure only shipped MacStorageAtlas behavior is marked as implemented and unverified claims are omitted.

## 3. Validate and close the change

- [x] 3.1 Manually open every comparison source link and verify that it directly supports the adjacent claim.
- [x] 3.2 Run `dotnet build` and `dotnet test` to confirm the documentation change accompanies a healthy repository.
- [x] 3.3 Run `openspec validate correct-market-comparison --strict --no-interactive`.
- [x] 3.4 Mark WP-00 complete in `docs/IMPLEMENTATION_ROADMAP.md` after the README change is accepted, then archive the OpenSpec change.
