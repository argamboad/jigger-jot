using JiggerJot.Api.Tests.Infrastructure;
using Perezosoft.Testing.Suites;

namespace JiggerJot.Api.Tests;

/// <summary>The platform's tenancy suite on this app's context: the filter, the stamping, the membership invariants.</summary>
[Collection(PostgresCollection.Name)]
public class TenancyTests(PostgresFixture fixture) : PlatformTenancySuite<TestAppDbContext>(fixture);
