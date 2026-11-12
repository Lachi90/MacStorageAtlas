# Design

## Context

See `proposal.md` for motivation and `specs/scan-traversal/spec.md` for the
required behavior.

`DiskItem` calls `ArgumentException.ThrowIfNullOrWhiteSpace` on its name.
`DiskScanner` builds child names with `Path.GetFileName(entryPath)` and wraps
only specific calls in `IsRecoverable` handlers that accept
`UnauthorizedAccessException`, `IOException`, `DirectoryNotFoundException`,
and `FileNotFoundException`. The `DiskItem` constructor runs outside those
handlers, so its `ArgumentException` escapes the async iterator.
`MainWindowViewModel.RunScanAsync` catches only `OperationCanceledException`,
and the generated `ScanFolderCommand` has no exception options, so the failure
reaches the UI synchronization context. The tree, export, and other views bind
`DiskItem.Name` directly.

## Goals / Non-Goals

**Goals:**

- Make whitespace-only and invisible names valid item names.
- Confine any unexpected per-entry failure to a scan error.
- Turn a whole-scan failure into a visible, non-fatal app state.

**Non-Goals:**

- Change the recoverable-exception classification used for access guidance.
- Add a global unhandled-exception handler for unrelated commands.

## Decisions

### Relax `DiskItem` name validation to non-empty

`DiskItem` keeps rejecting `null` and empty names but accepts whitespace-only
names. The scanner derives the name with `Path.GetFileName` and falls back to
the full path when that result is empty.

Alternatives considered:

- Substitute a placeholder name in Core. Rejected: search, export, history,
  and cleanup would then work with a name that does not exist on disk.
- Skip such entries. Rejected: it silently drops storage from totals.

### Present blank names through a display-name converter

An App `DisplayNameConverter` returns `⟨blank name: U+0020⟩`, listing every
code point, when the real name is whitespace-only or invisible,
`⟨empty name⟩` when it is empty, and the real name otherwise. Name text
bindings in the tree, item details, largest files, and duplicate lists, and
the Trash confirmation and failure messages, use it. Commands, search,
export, history, and Finder reveal keep `DiskItem.Name` and `DiskItem.Path`.
Invisible characters are detected with whitespace and the Unicode categories
`Zs`, `Cf`, and `Cc`, so the rule is deterministic and testable.

Alternatives considered:

- A display-name property on each row view model. Rejected: the largest-file
  and duplicate lists bind Core types directly, so a converter covers every
  list without new wrapper view models or presentation logic in Core.

### Contain unexpected per-entry exceptions in the scanner

The per-entry body of `ScanDirectoryAsync` that builds items and reads metadata
is wrapped so that any exception other than `OperationCanceledException` is
recorded through `ScanState.AddError` with the entry path, and traversal
continues. Because C# does not allow `yield return` inside a `try` with a
`catch`, the body is moved into a synchronous helper that returns an outcome,
and the iterator yields based on that outcome. Existing `IsRecoverable`
handling stays in place so access guidance still classifies permission errors
the same way.

Alternatives considered:

- Add `ArgumentException` to `IsRecoverable`. Rejected: it fixes one symptom
  and changes access classification semantics.

### Report whole-scan failures in the ViewModel

`RunScanAsync` catches exceptions other than `OperationCanceledException`
after the scan loop, leaves `ScanCompletedAt` unset, clears the in-progress
result as a cancelled scan does, and sets a new `ScanFailureMessage` shown in
the existing status area with the exception message only, never file
contents. `IsScanning` is reset by the existing `finally` block.

### Keep responsibilities within existing project boundaries

- Core: `DiskItem` validation, scanner name derivation, and per-entry
  containment, with unit tests.
- App: display-name presentation, scan failure state, AXAML bindings, and
  ViewModel tests.
- Platform.Mac and Rendering: unchanged.

## Risks / Trade-offs

- [Broad per-entry catch hides programming errors] → Each one appears in the
  scan error list with its exception type, and tests assert that.
- [Placeholder collides with a real name] → The placeholder uses characters
  that are uncommon in file names and is applied only to blank names.
- [Interaction with `attribute-firmlinked-storage`] → Implement after that
  change lands to avoid conflicting edits in `DiskScanner`.

## Migration Plan

No settings or stored data change. Older history snapshots contain no items
with blank names because such scans previously failed. Rollback reverts the
validation relaxation and handlers together.
