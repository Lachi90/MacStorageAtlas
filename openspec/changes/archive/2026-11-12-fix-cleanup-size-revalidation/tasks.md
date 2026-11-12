# Tasks

## 1. Revalidation

- [x] 1.1 Let `CleanupFileSystemMetadataReader` report the current allocated size through an optional `IAllocatedFileMetadataReader`, falling back to the logical length; verified by reader tests with a temporary file and a failing allocated reader.
- [x] 1.2 Pass the result's measurement mode to `CleanupPreflightValidator` and compare measured sizes in allocated modes; verified by validator tests for unchanged and changed files in each mode and a hardlink with zero counted size.
- [x] 1.3 Wire the macOS allocated reader and the result measurement mode into the App's source validator; verified by an App test that moves a shared-aware basket file to the Trash.

## 2. Documentation and Validation

- [x] 2.1 Review `README.md`, `docs/FEATURES.md`, and `docs/index.html` for cleanup statements and record whether an update was needed.
- [x] 2.2 Run build, tests, the IDE0005 format check, `openspec validate --all --strict --no-interactive`, and `git diff --check`.
- [x] 2.3 Repeat the sandboxed cleanup-basket Trash step with a rebuilt App Store payload.
