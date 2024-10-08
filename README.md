# CasCap.Common

[cascap.common.caching-badge]: https://img.shields.io/nuget/v/CasCap.Common.Caching?color=blue
[cascap.common.caching-url]: https://nuget.org/packages/CasCap.Common.Caching
[cascap.common.extensions-badge]: https://img.shields.io/nuget/v/CasCap.Common.Extensions?color=blue
[cascap.common.extensions-url]: https://nuget.org/packages/CasCap.Common.Extensions
[cascap.common.extensions.diagnostics.healthchecks-badge]: https://img.shields.io/nuget/v/CasCap.Common.Extensions.Diagnostics.HealthChecks?color=blue
[cascap.common.extensions.diagnostics.healthchecks-url]: https://nuget.org/packages/CasCap.Common.Extensions.Diagnostics.HealthChecks
[cascap.common.logging-badge]: https://img.shields.io/nuget/v/CasCap.Common.Logging?color=blue
[cascap.common.logging-url]: https://nuget.org/packages/CasCap.Common.Logging
[cascap.common.net-badge]: https://img.shields.io/nuget/v/CasCap.Common.Net?color=blue
[cascap.common.net-url]: https://nuget.org/packages/CasCap.Common.Net
[cascap.common.Serialization.json-badge]: https://img.shields.io/nuget/v/CasCap.Common.Serialization.Json?color=blue
[cascap.common.Serialization.json-url]: https://nuget.org/packages/CasCap.Common.Serialization.Json
[cascap.common.Serialization.messagepack-badge]: https://img.shields.io/nuget/v/CasCap.Common.Serialization.MessagePack?color=blue
[cascap.common.Serialization.messagepack-url]: https://nuget.org/packages/CasCap.Common.Serialization.MessagePack
[cascap.common.testing-badge]: https://img.shields.io/nuget/v/CasCap.Common.Testing?color=blue
[cascap.common.testing-url]: https://nuget.org/packages/CasCap.Common.Testing

![CI](https://github.com/f2calv/CasCap.Common/actions/workflows/ci.yml/badge.svg) [![Coverage Status](https://coveralls.io/repos/github/f2calv/CasCap.Common/badge.svg?branch=main)](https://coveralls.io/github/f2calv/CasCap.Common?branch=main) [![SonarCloud Coverage](https://sonarcloud.io/api/project_badges/measure?project=f2calv_CasCap.Common&metric=code_smells)](https://sonarcloud.io/component_measures/metric/code_smells/list?id=f2calv_CasCap.Common)

<!-- other types of SonarQube badges; bugs, code_smells, coverage, duplicated_lines_density, ncloc, sqale_rating, alert_status, reliability_rating, security_rating, sqale_index, vulnerabilities -->

A range of .NET class libraries packed with helper functions, extensions, utilities and 'kickstarter' abstract classes.

| Library                                           | Package                                                                                                |
| ------------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| CasCap.Common.Caching                             | [![Nuget][cascap.common.caching-badge]][cascap.common.caching-url]                                     |
| CasCap.Common.Extensions                          | [![Nuget][cascap.common.extensions-badge]][cascap.common.extensions-url]                               |
| CasCap.Common.Extensions.Diagnostics.HealthChecks | [![Nuget][cascap.common.extensions.diagnostics.healthchecks-badge]][cascap.common.extensions.diagnostics.healthchecks-url] |
| CasCap.Common.Logging                             | [![Nuget][cascap.common.logging-badge]][cascap.common.logging-url]                                     |
| CasCap.Common.Net                                 | [![Nuget][cascap.common.net-badge]][cascap.common.net-url]                                             |
| CasCap.Common.Serialization.Json                  | [![Nuget][cascap.common.Serialization.json-badge]][cascap.common.Serialization.json-url]               |
| CasCap.Common.Serialization.MessagePack           | [![Nuget][cascap.common.Serialization.messagepack-badge]][cascap.common.Serialization.messagepack-url] |
| CasCap.Common.Testing                             | [![Nuget][cascap.common.testing-badge]][cascap.common.testing-url]                                     |

## CasCap.Common.Caching

Special mention to this package which provides a customisable flexible Distributed Caching solution supporting the cache-aside pattern in an async manner. Local caching via either Memory or Disk and remote caching using Redis. Serialization supported includes both JSON and MessagePack.

Cache items are stored both in local and remote cache locations and a background service provides an expiration and syncronisation capability. This way CPU and/or IO intensive methods which generate objects for storage in the cache can only be fired once and then retrieved either from the original server via the local cache or by other consumers via the remote cache.
