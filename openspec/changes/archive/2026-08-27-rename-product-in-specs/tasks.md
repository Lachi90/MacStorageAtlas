## 1. Baseline

- [x] 1.1 Record the list of `### Requirement:` and `#### Scenario:` headings across `openspec/specs/*/spec.md`, with the product name normalized, as the comparison baseline.
- [x] 1.2 Record the count of old-name occurrences per spec file, split into product-name and identifier forms.

## 2. Substitution

- [x] 2.1 Apply `MacStorageAtlas(?!\.(?:[A-Z]|slnx))` → `Atlas Storage Analyzer` across `openspec/specs/*/spec.md` in one scripted pass.
- [x] 2.2 Apply the same guarded pattern to the product line of `openspec/config.yaml`.
- [x] 2.3 Confirm the assembly lines of `openspec/config.yaml` still name `MacStorageAtlas.Core`, `MacStorageAtlas.Rendering`, `MacStorageAtlas.Platform.Mac`, and `MacStorageAtlas.App`.

## 3. Verification

- [x] 3.1 Recompute the normalized heading list and confirm it is identical to the baseline from 1.1.
- [x] 3.2 Enumerate every surviving occurrence of the old token under `openspec/specs/` and `openspec/config.yaml`, and confirm each one is an assembly, namespace, project, or solution identifier.
- [x] 3.3 Confirm `openspec/changes/archive/` is unmodified.
- [x] 3.4 Confirm no file outside `openspec/` changed.

## 4. Delivery

- [x] 4.1 Run `openspec validate --all --strict --no-interactive`.
- [x] 4.2 Run `git diff --check`.
- [x] 4.3 Review `README.md`, the documentation under `docs/`, and `docs/index.html`, and report that no update was necessary because no user-visible behavior changed.
