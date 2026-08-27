## Why

The product was renamed to Atlas Storage Analyzer to resolve the App Review
rejection of 1.0.2, and the change that did it updated every user-visible
surface plus the two capabilities it touched. The remaining main specs still
call the product MacStorageAtlas, so `openspec/specs/` now describes an
application under a name that no longer exists. The project context in
`openspec/config.yaml` carries the same stale name into every artifact the
OpenSpec CLI generates from here on, which makes the drift self-propagating.

## What Changes

- Establish that specification documents and the OpenSpec project context refer
  to the product by its current user-visible name.
- Replace the product name in the eighteen main specs that still use the old
  one, as an editorial substitution that leaves every requirement, scenario,
  and normative keyword otherwise untouched.
- Update the product line of `openspec/config.yaml` so newly generated
  artifacts inherit the current name.
- Preserve assembly, namespace, project, and solution identifiers such as
  `MacStorageAtlas.Core`, `MacStorageAtlas.App`, and `MacStorageAtlas.slnx`,
  which name code that is deliberately not renamed.
- Leave archived changes untouched. They record what was agreed at the time and
  must stay readable as history.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `product-identity`: add a requirement that the specification documents and the
  OpenSpec project context name the product as users see it, while code-level
  identifiers keep their existing names.

## Impact

- `openspec/specs/*/spec.md` for eighteen capabilities: cleanup-basket,
  core-project-structure, duplicate-detection, file-metadata, item-relocation,
  main-toolbar-layout, product-comparison, recent-locations, release-packaging,
  result-export, result-filtering, result-tree-browsing,
  sandboxed-platform-integration, scan-access-guidance, scan-history,
  scan-performance, storage-measurement, and test-project-structure.
- `openspec/config.yaml`, product line only.
- No application code, tests, packaging, or user-facing documentation change.
  The behavior each spec describes is unchanged.
