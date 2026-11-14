# VersionFlow.PackageSmoke.Library

Non-published fixture used by the PackageSmoke consumer. It references packed VersionFlow.Core and declares two versions of IExternalOperation. The consumer discovers its generated catalog through compile-time assembly links without explicit catalog registration.

Build it through the PackageSmoke steps after packing, as described in that project's README. The root MIT license applies.
