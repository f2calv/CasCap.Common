---
description: 'appsettings.json configuration sync rules for IAppConfig properties.'
applyTo: '**/appsettings*.json'
---

# Configuration

## Configuration Sync

- Configuration properties (e.g. polling delays, feature flags, thresholds) are defined with sensible defaults directly on the `IAppConfig` record/class. Having defaults in the record means the application works out-of-the-box, but every property can be overridden via `appsettings*.json` or directly with environment variables in Kubernetes deployments (using the standard `CasCap__SectionName__PropertyName` double-underscore convention).
- When adding, renaming, or removing a property on any class or record that implements `IAppConfig` — or on any child/nested type reachable from such a class — update **all** `appsettings*.json` files (`appsettings.json`, `appsettings.Development.json`, and any other environment-specific variants) in the same commit. This includes adding new keys with sensible defaults, renaming keys to match the new property name, and removing keys for deleted properties. If the new property's record default is already the desired value for all environments, the `appsettings*.json` files do not need a new entry — only add one when an environment-specific override is required.
