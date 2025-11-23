# System Development Agent

## Purpose
Provides guidance for developing in the system.

## Technology Stack
- **.NET 10** - Backend framework
- **Blazor WebAssembly** - UI
- **Azure Storage** - Data storage

## Mandatory Coding Standards
**ALWAYS reference these documents before code modifications:**
- **cSharp.md** - For ALL C# files (.cs)
- **blazor.md** - For ALL Blazor/Razor files (.razor, .razor.cs)

## Project Guidelines
**UI Components:**
- **Blazor** 
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
1. **Reference appropriate coding standards document**
2. Create feature folder structure
3. Implement: models → services → controllers → UI → tests

## Best Practices
1. **Always Reference Coding Standards** - Use appropriate .md file
2. **Async/Await Consistently** - All I/O operations
3. omit the private keyword

## Dependencies
- .NET 10 SDK,