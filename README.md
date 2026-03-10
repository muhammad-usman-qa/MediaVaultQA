# MediaVault QA Automation Framework

A production-grade QA automation framework built in .NET 10, demonstrating enterprise testing practices across unit, integration, and end-to-end layers.

## Framework Overview

| Layer | Tool | Tests |
|---|---|---|
| Unit Tests | xUnit + Moq + FluentAssertions | 53 passing |
| Integration Tests | WireMock.NET + WebApplicationFactory | 39 passing |
| E2E Tests | Playwright .NET | Full CRUD lifecycle |
| Coverage Gate | Coverlet + GitHub Actions | 80% enforced |
| CI/CD Pipeline | GitHub Actions | 6 stage pipeline |

## What This Framework Covers

### Unit Tests — 53 Tests
- Full Moq mock chains with `.Verify()` assertions
- Theory/InlineData for all validation paths
- FluentAssertions semantic assertions throughout
- Fake data generators for AudioFile, EmailRecord, ChatTranscript

### Integration Tests — 39 Tests
- WireMock.NET simulating external blob storage vendor
- WebApplicationFactory boots real API in memory
- Full CRUD + WireMock call verification for all 3 entity types
- In-memory database isolation per test run

### E2E Tests — Playwright
- Black-box lifecycle tests — Create → Read → Update → Delete
- Validation and 404 scenario coverage
- Configurable base URL — runs against local or deployed environment

### Fake Data Generators
- `AudioFileFaker.cs` — realistic audio file metadata
- `EmailRecordFaker.cs` — realistic email records
- `ChatTranscriptFaker.cs` — realistic chat transcripts with messages
- Bogus-powered with specialised builder methods for edge cases

### CI/CD Pipeline — GitHub Actions
6 stage pipeline on every PR:
1. Build
2. Unit Tests
3. Integration Tests
4. E2E Tests
5. Combined 80% Coverage Gate — blocks merge if coverage drops
6. Quality Summary — posted to PR as markdown table

## How To Run

### Prerequisites
- .NET 10 SDK
- Docker (for Testcontainers)

### Run All Tests
```bash
dotnet test
```

### Run Unit Tests Only
```bash
dotnet test MediaVault.UnitTests
```

### Run Integration Tests Only
```bash
dotnet test MediaVault.IntegrationTests
```

### Run With Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## QA Program Metrics

This framework demonstrates the same approach used to transform QA at The Tech Excellence:

| Metric | Before | After |
|---|---|---|
| Unit Test Coverage | 18% | 87% |
| Regression Time | 3 days manual | 4 hours automated |
| Vendor API Failures | High | 65% reduction |
| Production Incidents | Frequent | Rare |
| External APIs Mocked | 0 | 6 vendors |

## Architecture Decisions

**Why xUnit?**
Industry standard for .NET. Clean Theory/InlineData support for data-driven tests. Integrates with every CI pipeline natively.

**Why WireMock.NET?**
Simulates real HTTP vendor APIs at the network level — not just C# interfaces. Developers can run full test suite locally without VPN or live vendor access.

**Why WebApplicationFactory?**
Boots the entire API in memory — all middleware, routing, validation, and services running for real. Tests the full HTTP pipeline without deployment.

**Why Playwright?**
Faster and more reliable than Selenium. Built-in parallel execution, auto-waits, and network interception. Page Object Model keeps tests maintainable.

**Why 80% Coverage Gate?**
Coverage gates are hard build failures — not warnings. A warning without consequence gets ignored within two weeks. When the pipeline blocks a merge, quality becomes every developer's responsibility.

## Author
Muhammad Usman — Senior QA Automation Engineer
[GitHub](https://github.com/muhammad-usman-qa)