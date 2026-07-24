# MacStorageAtlas repository instructions

These rules apply to the entire repository. Keep `AGENTS.md` and `CLAUDE.md`
synchronized.

## Project context

- MacStorageAtlas is a native macOS storage analyzer built with .NET 10, C#,
  Avalonia, CommunityToolkit.Mvvm, NUnit, and NSubstitute.
- `global.json` and the project files are the source of truth for SDK, framework,
  language, and package versions. Do not change them unless the task requires it.
- The app supports Apple Silicon and Intel Macs. Do not introduce behavior that
  assumes only one architecture.

## Repository structure

- `src/MacStorageAtlas.Core` owns scan-domain logic and must remain independent
  of Avalonia.
- `src/MacStorageAtlas.Rendering` owns treemap layout calculations.
- `src/MacStorageAtlas.Platform.Mac` owns Finder, Trash, Dock, and other
  macOS-specific integrations.
- `src/MacStorageAtlas.App` owns Avalonia views, application services,
  ViewModels, and the composition root.
- `tests/MacStorageAtlas.Tests` contains NUnit unit and integration tests.
- Keep dependencies flowing toward Core. Do not add UI dependencies to Core or
  platform dependencies to Rendering.

## Code style

- Follow the effective `.editorconfig` and the established style in nearby
  files.
- Use modern C# supported by the pinned .NET SDK, nullable reference types,
  file-scoped namespaces, descriptive names, and focused methods.
- Use PascalCase for types, methods, properties, and public members; use
  camelCase for parameters and local variables; prefix interfaces with `I`.
- Suffix asynchronous methods with `Async`. Return `Task` or `Task<T>` and avoid
  `async void` except where a framework event signature requires it.
- Prefer `is null` and `is not null`, `nameof`, guard clauses, and pattern
  matching when they make the code clearer.
- C# files must not contain unused `using` directives. Remove obsolete imports
  whenever code changes.
- Do not add code or configuration comments of any kind, including inline,
  block, XML documentation, XAML/XML/HTML/CSS/JavaScript, shell, TODO, FIXME, or
  commented-out code. Use clear names and structure, and put necessary
  explanations in Markdown documentation or OpenSpec artifacts. Shebangs and
  compiler or preprocessor directives are not comments.
- Prefer the .NET base class library and existing dependencies. Add or upgrade a
  package only when the task requires it.

## Avalonia and MVVM

- Keep views declarative in AXAML. Limit code-behind to view-only behavior,
  framework integration, and platform presentation concerns.
- Use compiled bindings and provide `x:DataType` where practical. Set binding
  modes explicitly when state must flow back to a ViewModel.
- Reuse theme resources and styles instead of hardcoding colors, spacing,
  typography, or repeated control styling.
- ViewModels inherit from the appropriate CommunityToolkit.Mvvm base class.
  Prefer `[ObservableProperty]`, `[RelayCommand]`,
  `[NotifyPropertyChangedFor]`, and `[NotifyCanExecuteChangedFor]` over manual
  notification or command boilerplate.
- Use generated property-change partial methods for side effects instead of
  subscribing a ViewModel to its own `PropertyChanged` event.
- Inject dependencies through constructors. Do not use service locators inside
  ViewModels or services.
- Keep blocking filesystem work off the UI thread and marshal UI state changes
  through the UI-dispatcher abstraction.
- Propagate `CancellationToken` through cancellable operations. Do not use
  `.Wait()`, `.Result`, or other sync-over-async patterns.

## Storage and safety behavior

- Preserve the distinction between logical length and locally allocated bytes.
  Every byte count and label produced by one scan must use the same measurement
  mode.
- Allocated-size mode is per visited path and does not currently deduplicate
  hardlinks or APFS clone storage. Update `docs/STORAGE_MEASUREMENT.md`, tests,
  and OpenSpec artifacts when these semantics change.
- Scanning must remain streaming, cancellable, responsive, and resilient to
  recoverable filesystem errors.
- Preserve hidden-file, package-expansion, symbolic-link, and cycle-detection
  behavior unless a reviewed change explicitly modifies it.
- Never replace Trash-based cleanup with permanent deletion. Destructive actions
  require explicit user confirmation and should remain recoverable.
- Do not log or persist file contents. Treat scanned paths and metadata as
  private user data.

## Testing

- Add or update tests for every behavior change and regression fix. Cover
  success, failure, boundary, cancellation, and platform-specific paths as
  applicable.
- Use NUnit assertions and NSubstitute consistently with nearby tests.
- Give tests descriptive behavior-based names. Keep each test independent and
  deterministic.
- Do not add Arrange, Act, or Assert comments. Separate test phases with blank
  lines when useful.
- Use isolated temporary directories for filesystem tests and clean them up.
  Never depend on or modify a user's real files.
- Gate macOS-only integration tests explicitly and ignore them with a clear
  reason on unsupported platforms.

## Documentation and OpenSpec

- Write Markdown compatible with CommonMark: use ATX headings, blank lines around
  blocks, language identifiers on fenced code blocks, valid links, and useful
  image alt text.
- Update user-facing documentation when commands, behavior, limitations, or
  screenshots change.
- Verify time-sensitive market comparisons against current primary sources and
  record the verification date.
- Follow `docs/OPENSPEC_WORKFLOW.md` for feature-level work. Keep each change
  focused on one intent, implement only approved scope, and update `tasks.md` as
  work completes.
- Validate OpenSpec artifacts strictly before implementation, after changes, and
  before archive.

## Shell and packaging

- Keep shell scripts fail-fast, quote variable expansions, validate arguments,
  avoid `eval`, and keep destructive paths explicit and narrowly scoped.
- Preserve both `osx-arm64` and `osx-x64` packaging unless the task explicitly
  changes supported architectures.
- Do not modify or commit generated output such as `bin/`, `obj/`, `*.app`,
  `*.dmg`, coverage results, IDE settings, or `.DS_Store`.

## Validation and delivery

- Run commands from the repository root.
- After code changes, run:

  ```shell
  dotnet build MacStorageAtlas.slnx --no-restore
  dotnet test MacStorageAtlas.slnx --no-build
  dotnet format MacStorageAtlas.slnx analyzers --diagnostics IDE0005 --verify-no-changes
  git diff --check
  ```

- Run `dotnet restore` before the first build in a fresh checkout and whenever
  dependencies or SDK inputs changed.
- Run `openspec validate --all --strict --no-interactive` when OpenSpec
  artifacts or specified behavior changed.
- Run `shellcheck build-dmg.sh` when the packaging script changes and ShellCheck
  is available.
- Preserve unrelated working-tree changes. Do not commit, archive, or push
  unless the user explicitly requests it.
