# Changelog

## Unreleased

- The runtime NuGet package is now `VersionFlow` (previously `VersionFlow.Core`). Replace the old package reference with `VersionFlow`; the public namespace remains `VersionFlow`. The source project and assembly remain `VersionFlow.Core`, with an explicit default namespace of `VersionFlow` for new source files.

- Railway results now support `OnCompleted` / `OnCompletedAsync` observers for either outcome, including fluent task extensions. New `TraverseResults` / `TraverseResultsAsync` APIs retain every input and result in a read-only batch, with successful values, failed entries, flattened errors, and awaited batch observers. Existing traversal behavior is unchanged.

- Railway validation now supports per-check `onPassed(value, wasRecovered)` and `onFailed(error)` observers. Async validation also accepts awaited `onPassedAsync` and `onFailedAsync` observers with cancellation. Observers run only for the current rule's final outcome and are skipped after earlier failures. Supplying both callback forms for one outcome is rejected; observer exceptions propagate. Existing validation signatures remain available for compiled consumers.

- **CQRS breaking change:** `ICqrsHandler<TRequest, TResponse>` and `ICqrsHandler<TRequest>` now expose synchronous `Handle` methods. Existing async handlers must use `ICqrsAsyncHandler<TRequest, TResponse>` or `ICqrsAsyncHandler<TRequest>`; their `HandleAsync` methods are unchanged. Update registrations, consumers, and wrapper interfaces, then rebuild. Both calling conventions support cancellation and generated non-generic wrappers.

## 1.0.1

- AddVersionFlow and AddVersionFlowOptions now discover generated catalogs and register interface dispatchers automatically.
- Generated catalog metadata links referenced projects, with per-container deduplication and an explicit assembly selector for shared startup helpers.
- Added parameterless AddVersionFlow for default Latest selection and autoRegister:false for manual/fluent-only integrations.
- Preserved original setup method signatures and explicit catalog/dispatcher APIs.
- Updated sample, package-consumer coverage, and all usage READMEs around the simplified startup and non-generic CQRS wrapper pattern.

### Upgrade

Update Core in your startup and implementation projects and rebuild them. For normal setup, keep concrete DI registration plus AddVersionFlow and remove explicit catalog/dispatcher calls. Existing direct registrations of versioned interfaces conflict with automatic dispatchers; remove them or use autoRegister:false intentionally. Explicit catalogs registered before setup retain manual catalog scope. No implementation versions need to change.

## 1.0.0

Initial class/method version routing, bundled generator, fluent matching, options/reload support, independent CQRS contracts, and native attribute DI registration.
