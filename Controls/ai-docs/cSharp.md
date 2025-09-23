# C# Development Agent

## Purpose
Modern C# development guidance for Hatch system using .NET 8 features and clean architecture.

## Projects
- **Hatch.Core** - Business logic and services
- **Hatch.Web** - API controllers
- **Hatch.Shared** - Shared models

## Modern C# Syntax

```csharp
// File-scoped namespaces, primary constructors
using Microsoft.AspNetCore.Mvc;

[ApiController, Route("api/[controller]/[action]")]
public class ProjectsController(IProjectsService projectsService) : ControllerBase
{
    public async Task<Project> GetProject(int id) => await projectsService.GetProject(id);
}

// Collection expressions
var projects = [project1, project2, project3];
var emptyList = new List<Project>();
```

## Naming Conventions
- **PascalCase**: Classes, methods, properties
- **camelCase**: Variables, parameters
- **_camelCase**: Private fields
- **UPPER_CASE**: Configuration constants only

## Controller Pattern
```csharp
[ApiController, Route("api/[controller]/[action]")]
public class VendorsController(IVendorsService vendorsService, IUsersService usersService) : ControllerBase
{
    [HttpGet]
    public async Task<GetVendorsResponse> Get([FromQuery] GetVendorsRequest request)
        => await vendorsService.GetVendors(request);

    [HttpPost]
    public async Task<Vendor> Upsert([FromBody] UpsertVendorRequest request)
        => await vendorsService.UpsertVendor(request);
}
```

## Service Pattern
```csharp
public class VendorsService(IHatchDbConnectionFactory connectionFactory) : IVendorsService
{
    private readonly List<Vendor> _cachedData = [];
    private bool _isInitialized;

    public async Task<List<Vendor>> GetVendors(GetVendorsRequest request)
    {
        EnsureInitialized();
        return await repository.GetVendors(request);
    }

    private void EnsureInitialized()
    {
        if (_isInitialized) return;
        // Initialize _cachedData
        _isInitialized = true;
    }
}
```

## Best Practices
1. **Use C# 12 features** - File-scoped namespaces, primary constructors, collection expressions
2. **Async everywhere** - All I/O operations, no "Async" suffix
3. **LINQ method syntax** - Multi-line for readability
4. **List<T> over arrays** - Initialize with `= []`
5. **XML docs for public APIs** - When purpose isn't obvious
