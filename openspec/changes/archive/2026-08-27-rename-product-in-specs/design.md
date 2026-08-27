## Context

`resolve-app-store-rejection` renamed the product to Atlas Storage Analyzer and
carried the new name into the application, the packaging, the documentation, and
the two capabilities it touched: `product-identity` and `release-packaging`. The
other eighteen main specs were outside that change's scope and still name the
product MacStorageAtlas, roughly 360 times.

Two constraints shape the work. The code keeps its existing identifiers, so the
same token appears in the specs both as a product name that must change and as
an assembly, project, or solution name that must not. And the specs are the
archived agreement about behavior, so the substitution must be provably
editorial rather than an opportunity to reword requirements.

## Goals / Non-Goals

**Goals:**

- One product name across `openspec/specs/` and the OpenSpec project context.
- A substitution that provably changes nothing but the product name.
- A rule in `product-identity` that keeps future specs consistent without
  needing another sweep.

**Non-Goals:**

- Renaming assemblies, namespaces, projects, or the solution file.
- Rewriting archived changes.
- Changing the bundle identifier or any user data location.
- Improving, clarifying, or restructuring any requirement encountered on the
  way. A wording problem found here becomes its own change.

## Decisions

**The delta specifies the naming rule, not the renamed requirements.**
OpenSpec is delta-first, and a delta that carried the substitution itself would
have to restate nearly every requirement in the repository under `## MODIFIED
Requirements` — hundreds of blocks whose only difference is one noun. That
volume would hide the actual agreement and would make the archive step re-apply
text that the tasks already wrote. Instead the delta adds two requirements to
`product-identity`: specs name the product as users see it, and code-level
identifiers are exempt. The substitution is mechanical work in `tasks.md`.

The alternative, treating the sweep as a documentation chore with no OpenSpec
change at all, was rejected because nothing would then prevent the next sweep
from being necessary.

**The substitution is a guarded pattern, not a plain replacement.**
A plain `MacStorageAtlas` → `Atlas Storage Analyzer` replacement would corrupt
`MacStorageAtlas.Core`, `MacStorageAtlas.App`, `MacStorageAtlas.Platform.Mac`,
and `MacStorageAtlas.slnx`. Replace only occurrences not followed by a dot and
an uppercase letter or by `.slnx`, which as a Perl pattern is
`MacStorageAtlas(?!\.(?:[A-Z]|slnx))`.

The negative lookahead is needed rather than a simpler "not followed by a dot"
rule because `scan-performance/spec.md` ends a sentence with the product name,
where the following dot is a full stop and the occurrence must be replaced.

**Correctness is verified structurally, not by reading the diff.**
After the sweep, two checks establish that the substitution was editorial: the
list of `### Requirement:` and `#### Scenario:` headings is compared before and
after with only the product name normalized, and the remaining occurrences of
the old token are confirmed to be identifier forms only. Reading a 360-line diff
would not reliably catch a stray edit; the heading comparison would.

## Risks / Trade-offs

- **A guarded pattern still misses a form nobody anticipated**, such as the name
  inside a code fence or a URL → The verification step enumerates every surviving
  occurrence of the old token and requires each to be an identifier, so an
  unexpected form surfaces as a leftover rather than passing silently.

- **The heading comparison passes while prose is altered** → The tasks perform
  the substitution with one scripted pattern over whole files rather than
  file-by-file editing, so there is no step at which prose could be rewritten by
  hand.

- **The delta records a rule the sweep itself does not prove** → Accepted. The
  requirement is verifiable at any later point by the same grep the verification
  task runs, and it is what keeps the next capability from reintroducing the old
  name.

- **`openspec/config.yaml` lists both the product and the assemblies**, so the
  same guarded pattern applies there and only the product line should change →
  The task checks the file's assembly lines explicitly after the edit.
