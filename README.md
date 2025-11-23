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

## Support

- **Documentation**: Check the demo applications in the repository
- **Issues**: Report bugs via GitHub Issues
- **Feature Requests**: Submit enhancement requests via GitHub Issues

## Changelog

### 10.0.1
- Upgrade to .net 10
- Add the ability to hide the required star within FormOptions

### 1.13.8
- Exposed xmldoc comments

### 1.13.7
- refactoring

### 1.13.6
- Move the star for non-legends to the left.

### 1.13.5
- Enable tooltips through markup
- Move the required star to the left of the label

### 1.13.4
- EditDate and other controls. Add a null value string to display when the value is null, such as a dash instead of blank space.
- IsRequired parameter on all controls. When set forces the “edit-label-required-star” to show up without being required in the DataAnnotations.
- Accessibility updates for EditCheckedStringList

### 1.13.3
- Current stable release
- Full feature set with comprehensive validation support

### 1.0.13.2
- EditCheckedListEnum
- 
### 1.0.13.1
- Rename icons to have edit- in front of the current names
  - .icon-eye => .edit-icon-eye
 - Icon-invalid, icon-eye-invisible
- EditSelectEnum no longer requires specifying the type.
- Tooltips exist on the controls
 - Only from attributes right now [Tooltip(“My cool tooltip”)
 - 
### 1.0.12.11
- Import js into application in App.razor or index.html
   -     <script src="_content/WssBlazorControls/editControls.js"></script>
  - This is to add the functionality of “When submit is clicked, but invalid, scroll to the first input that is invalid.
  - Use JsInteropEc to access js methods. Use JsInteropEc.FocusFirstInvalidField() when there are validation errors while submitting.
- EditCheckedStringList
  - Error message shows up on each checkbox

### 1.0.12.10
- IsRequired parameter on all controls. When set forces the “edit-label-required-star” to show up without being required in the DataAnnotations.
- Accessibility updates for EditCheckedStringList
- 
### 1.0.12.x
- moved away from utilizing bootstrap css classes such as form-group to using classes that start with edit- to avoid conflicts with other libraries
- New Features
 - IsHidden to hide controls withougt wrapping them in an if statement
 - Hiding allows hiding controls based on their own property for [Never, WhenReadonlyAndNull, WhenReadonly, etc.]
   - This also exists within FormOptions, so the hiding can be controlled over a large group of controls.
- Control Changes
 - EditRadio and EditCheckedList
   - Change parameter from HasHorizontalButtons -> IsHorizontal
   - Removed the need for "Type" parameter, now uses the type of the value passed in.
 - EditSelectEnum
   - Removed the need for "Type" parameter, now uses the type of the value passed in.
- New Controls
  - EditBoolNullRadio

