# Changelog

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
