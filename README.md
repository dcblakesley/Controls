# WssBlazorControls

[![NuGet Version](https://img.shields.io/nuget/v/WssBlazorControls.svg)](https://www.nuget.org/packages/WssBlazorControls/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/WssBlazorControls.svg)](https://www.nuget.org/packages/WssBlazorControls/)

A comprehensive library of form controls for Blazor applications providing consistent, feature-rich input components with built-in validation, accessibility support, and flexible styling options.

## Features

- **?? Rich Form Controls**: String, Number, Date, Boolean, Select, Radio, Checkbox lists, and TextArea components
- **? Data Annotations Integration**: Full support for validation attributes (Required, Range, MinLength, etc.)
- **? Accessibility First**: ARIA attributes, screen reader support, and keyboard navigation
- **?? Flexible Display Modes**: Edit mode and read-only views for all controls
- **?? Consistent Styling**: CSS classes and customizable appearance
- **? TypeScript/JavaScript Interop**: Enhanced client-side functionality
- **?? Cross-Platform**: Works with both Blazor Server and Blazor WebAssembly

## Installation

Install the package via NuGet Package Manager:

```bash
dotnet add package WssBlazorControls
```

Or via Package Manager Console:

```powershell
Install-Package WssBlazorControls
```

## Quick Start

1. **Add the using statement** to your `_Imports.razor`:

```razor
@using WssBlazorControls
```

2. **Include the CSS** in your `App.razor` or `index.html`:

```html
<link href="_content/WssBlazorControls/editControls.css" rel="stylesheet" />
```

3. **Use the controls** in your Blazor components:

```razor
@using System.ComponentModel.DataAnnotations

<EditForm Model="@model" OnValidSubmit="@HandleSubmit">
    <DataAnnotationsValidator />
    
    <EditString @bind-Value="model.Name" 
                Label="Full Name" 
                IsRequired="true" />
    
    <EditNumber @bind-Value="model.Age" 
                Label="Age" 
                IsRequired="true" />
    
    <EditDate @bind-Value="model.BirthDate" 
              Label="Birth Date" />
    
    <EditBool @bind-Value="model.IsActive" 
              Label="Active Status" />
    
    <button type="submit">Submit</button>
    
    <ValidationSummary />
</EditForm>

@code {
    private PersonModel model = new();
    
    private void HandleSubmit()
    {
        // Handle form submission
    }
    
    public class PersonModel
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = "";
        
        [Required]
        [Range(1, 120)]
        public int? Age { get; set; }
        
        public DateTime? BirthDate { get; set; }
        
        public bool IsActive { get; set; } = true;
    }
}
```

## Available Controls

### Input Controls
- **`EditString`** - Text input with masking and URL support
- **`EditTextArea`** - Multi-line text input
- **`EditNumber`** - Numeric input with validation
- **`EditDate`** - Date picker component
- **`EditBool`** - Checkbox for boolean values
- **`EditBoolNullRadio`** - Three-state radio for nullable booleans

### Selection Controls
- **`EditSelect`** - Dropdown selection for objects
- **`EditSelectEnum`** - Dropdown for enum values
- **`EditSelectString`** - Dropdown for string values
- **`EditRadio`** - Radio buttons for objects
- **`EditRadioEnum`** - Radio buttons for enums
- **`EditRadioString`** - Radio buttons for strings

### Multi-Selection Controls
- **`EditCheckedStringList`** - Checkbox list for strings
- **`EditCheckedEnumList`** - Checkbox list for enums

### Support Components
- **`FormLabel`** - Consistent labeling with tooltips and descriptions
- **`FieldValidationDisplay`** - Validation message display
- **`ReadOnlyValue`** - Read-only value presentation

## Component Features

All form controls implement the `IEditControl` interface and provide:

- **Identity Management**: `Id`, `IdPrefix` for unique identification
- **Display Control**: `IsEditMode`, `IsDisabled`, `IsHidden`
- **Labeling**: `Label`, `Description` with markup support
- **Styling**: `ContainerClass` for custom CSS
- **Validation**: `IsRequired` integration with DataAnnotations
- **Conditional Display**: `Hiding` modes and `HidingMode` enum

## Examples

### Dropdown with Enum

```razor
<EditSelectEnum @bind-Value="model.Priority" 
                Label="Priority Level" 
                IsRequired="true" />

@code {
    public enum Priority
    {
        Low,
        Medium,
        High,
        Critical
    }
    
    public class TaskModel
    {
        [Required]
        public Priority? Priority { get; set; }
    }
}
```

### Radio Button Group

```razor
<EditRadioString @bind-Value="model.Department" 
                 Label="Department"
                 Options="@departments" />

@code {
    private List<string> departments = new() 
    { 
        "Engineering", 
        "Marketing", 
        "Sales", 
        "Support" 
    };
}
```

### Checkbox List

```razor
<EditCheckedStringList @bind-Value="model.Skills" 
                       Label="Technical Skills"
                       Options="@skills" />

@code {
    private List<string> skills = new() 
    { 
        "C#", 
        "JavaScript", 
        "Blazor", 
        "ASP.NET Core" 
    };
}
```

## Styling and Customization

The library provides default styling through the included CSS file. You can customize the appearance by:

1. **Overriding CSS classes** in your own stylesheets
2. **Using ContainerClass** parameter for component-specific styling
3. **Applying custom CSS** to the `.edit-control-wrapper` class

```razor
<EditString @bind-Value="model.Name" 
            Label="Name" 
            ContainerClass="my-custom-style" />
```

## Accessibility

WssBlazorControls is built with accessibility as a priority:

- ? **ARIA attributes** for screen readers
- ?? **Keyboard navigation** support
- ?? **Focus management** and indicators
- ?? **Semantic HTML** structure
- ?? **High contrast** color support

## Browser Support

- Modern browsers with WebAssembly support
- Designed for both Blazor Server and Blazor WebAssembly scenarios
- Compatible with .NET 8.0+

## Contributing

Contributions are welcome! Please feel free to submit issues, feature requests, or pull requests.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Changelog

### Version 1.0.13.3
- Current stable release
- Full feature set with comprehensive validation support

For detailed changelog, see the [releases](../../releases) page.

## Support

- ?? **Documentation**: Check the demo applications in the repository
- ?? **Issues**: Report bugs via GitHub Issues
- ?? **Feature Requests**: Submit enhancement requests via GitHub Issues

---

Built with ?? for the Blazor community