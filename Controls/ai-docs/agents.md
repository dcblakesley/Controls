# System Development Agent

## Purpose
Provides guidance for developing in the system.

## Technology Stack
- **.NET 10** - Backend framework
- **Blazor WebAssembly** - UI
- **SQL Server + MongoDB** - Data storage
- **Redis, RabbitMQ, MinIO** - Infrastructure

## Mandatory Coding Standards
**ALWAYS reference these documents before code modifications:**

- **cSharp.md** - For ALL C# files (.cs)
- **blazor.md** - For ALL Blazor/Razor files (.razor, .razor.cs)
- **react.md** - For ALL React/TypeScript files (.tsx, .ts)
- **unique-objects-pattern.md** - For UniqueObjectBase implementations
- **integration-tests-setup-guide.md** - For ALL integration test classes

## Project Guidelines
**UI Components:**
- **Blazor** for external-facing features
- **React TypeScript** for internal Hatch.Web features

**Backend:**
- **Hatch.Core** - Business logic and services
- **Hatch.Web** - API controllers
- **Hatch.Shared** - Shared models

## Architecture Patterns
- **Feature-based organization** by business domain
- **MongoDB** for documents/caching, **SQL Server** for relational data
- **UniqueObjects pattern** for singleton configuration data
- **Async/await** throughout
- **Dependency injection** for services

## Development Workflow
1. Determine frontend (React internal vs Blazor external)
2. **Reference appropriate coding standards document**
3. Create feature folder structure
4. Implement: models → services → controllers → UI → tests

## Best Practices
1. **Domain-Driven Design** - Group by business domain
2. **Always Reference Coding Standards** - Use appropriate .md file
3. **Async/Await Consistently** - All I/O operations
4. **Comprehensive Testing** - Unit and integration tests

## Dependencies
- .NET 8 SDK, Docker Desktop, Node.js/Yarn
- SQL Server, MongoDB, Redis (local or containerized)