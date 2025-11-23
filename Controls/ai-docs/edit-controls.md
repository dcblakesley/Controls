# WssBlazorControls Usage Guide

A comprehensive guide for utilizing the WssBlazorControls library in your Blazor applications.

## Installation

Install the NuGet package:

```bash
dotnet add package WssBlazorControls
```

## Quick Start

### 1. Add Required Dependencies

Ensure your project has the required dependencies. The version of `Microsoft.AspNetCore.Components.Web` should match your target framework:

**For .NET 8.0:**
```xml
<PackageReference Include="Microsoft.AspNetCore.Components.DataAnnotations.Validation" Version="3.2.0-rc1.20223.4" />
<PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="8.0.0" />
```

**For .NET 9.0:**
```xml
<PackageReference Include="Microsoft.AspNetCore.Components.DataAnnotations.Validation" Version="3.2.0-rc1.20223.4" />
<PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="9.0.0" />
```

**For .NET 10.0:**
```xml
<PackageReference Include="Microsoft.AspNetCore.Components.DataAnnotations.Validation" Version="3.2.0-rc1.20223.4" />
<PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="10.0.0" />
```

> **Note:** The WssBlazorControls library targets .NET 8.0, 9.0, and 10.0. Choose the appropriate version based on your project's target framework.

### 2. Include CSS Styles

Add the CSS reference to your `App.razor` or `_Host.cshtml`:

```html
<link href="_content/WssBlazorControls/edit-controls.css" rel="stylesheet" />
```

### 3. Add Using Statement

Include the namespace in your Razor files or `_Imports.razor`:

```razor
@using Controls
```

### 4. Basic Usage

```razor
@using Microsoft.AspNetCore.Components.Forms

<EditForm Model="@myModel">
    <DataAnnotationsValidator />
    
    <EditString @bind-Value="myModel.Name" Field="@(() => myModel.Name)" />
    <EditNumber @bind-Value="myModel.Age" Field="@(() => myModel.Age)" />
    <EditBool @bind-Value="myModel.IsActive" Field="@(() => myModel.IsActive)" />
    
    <button type="submit">Submit</button>
</EditForm>

@code {
    public class MyModel
    {
        [Required]
        [DisplayName("Full Name")]
        public string Name { get; set; } = "";
        
        [Range(18, 100)]
        public int Age { get; set; }
        
        public bool IsActive { get; set; }
    }
    
    MyModel myModel = new();
}
```

## Available Components

### Text Input Controls

#### EditString
Basic text input with optional masking and URL support.

```razor
<EditString @bind-Value="model.Email" 
           Field="@(() => model.Email)"
           Placeholder="Enter email address" />

<!-- With URL in read-only mode -->
<EditString @bind-Value="model.Website" 
           Field="@(() => model.Website)"
           Url="@model.Website"
           UrlTarget="_blank" 
           IsEditMode="false" />

<!-- With masking -->
<EditString @bind-Value="model.Password" 
           Field="@(() => model.Password)"
           MaskText="****" />
```

#### EditTextArea
Multi-line text input.

```razor
<EditTextArea @bind-Value="model.Description" 
              Field="@(() => model.Description)"
              Rows="4" />
```

### Numeric Controls

#### EditNumber
Numeric input with validation and formatting.

```razor
<EditNumber @bind-Value="model.Price" 
            Field="@(() => model.Price)"
            Format="C" />

<EditNumber @bind-Value="model.Quantity" 
            Field="@(() => model.Quantity)" />
```

### Date Controls

#### EditDate
Date picker with customizable format.

```razor
<EditDate @bind-Value="model.BirthDate" 
          Field="@(() => model.BirthDate)"
          DateFormat="MM/dd/yyyy" />
```

### Boolean Controls

#### EditBool
Simple checkbox for boolean values.

```razor
<EditBool @bind-Value="model.IsActive" 
          Field="@(() => model.IsActive)" />
```

#### EditBoolNullRadio
Three-state radio buttons for nullable boolean values.

```razor
<EditBoolNullRadio @bind-Value="model.IsApproved" 
                   Field="@(() => model.IsApproved)"
                   TrueText="Approved"
                   FalseText="Rejected"
                   NullText="Pending"
                   IsHorizontal="true" />
```

### Selection Controls

#### EditSelect
Generic dropdown for object selection.

```razor
<EditSelect @bind-Value="selectedPlant" 
            Field="@(() => selectedPlant)">
    <option value="">Select a plant...</option>
    @foreach (var plant in plants)
    {
        <option value="@plant">@plant.Name</option>
    }
</EditSelect>
```

#### EditSelectEnum
Dropdown for enum values with automatic options.

```razor
<EditSelectEnum @bind-Value="model.AnimalType" 
                Field="@(() => model.AnimalType)" />

@code {
    public enum AnimalType
    {
        Cat,
        [EnumDisplayName("Puppy Dog")]
        Dog,
        Bird
    }
}
```

#### EditSelectString
Dropdown for string values.

```razor
<EditSelectString @bind-Value="model.Color" 
                  Field="@(() => model.Color)"
                  Options="@colorOptions" />

@code {
    List<string> colorOptions = ["Red", "Green", "Blue", "Yellow"];
}
```

### Radio Button Controls

#### EditRadio
Radio buttons for object selection.

```razor
<EditRadio @bind-Value="selectedItem" 
           Field="@(() => selectedItem)"
           Options="@itemList"
           IsHorizontal="true" />
```

#### EditRadioEnum
Radio buttons for enum values.

```razor
<EditRadioEnum @bind-Value="model.Priority" 
               Field="@(() => model.Priority)"
               IsHorizontal="false" />
```

#### EditRadioString
Radio buttons for string values.

```razor
<EditRadioString @bind-Value="model.Size" 
                 Field="@(() => model.Size)"
                 Options="@sizeOptions"
                 HasOther="true" />

@code {
    List<string> sizeOptions = ["Small", "Medium", "Large"];
}
```

### Multi-Selection Controls

#### EditCheckedStringList
Checkbox list for multiple string selection.

```razor
<EditCheckedStringList @bind-Value="model.Interests" 
                       Field="@(() => model.Interests)"
                       Options="@interestOptions"
                       IsHorizontal="true" />

@code {
    List<string> interestOptions = ["Sports", "Music", "Reading", "Travel"];
    public class Model 
    {
        [MinLength(2, ErrorMessage = "Select at least 2 interests")]
        public List<string> Interests { get; set; } = [];
    }
}
```

#### EditCheckedEnumList
Checkbox list for multiple enum selection.

```razor
<EditCheckedEnumList @bind-Value="model.Skills" 
                     Field="@(() => model.Skills)" />

@code {
    public enum Skill
    {
        Programming,
        Design,
        Marketing,
        Management
    }
    
    public class Model 
    {
        public List<Skill> Skills { get; set; } = [];
    }
}
```

## Core Interface Properties

All controls implement the `IEditControl` interface, providing these common properties:

### Identity Properties
```razor
<!-- Custom ID and prefix for unique identification -->
<EditString @bind-Value="model.Name" 
           Field="@(() => model.Name)"
           Id="custom-name-input"
           IdPrefix="form1" />
```

### Display Control
```razor
<!-- Toggle between edit and read-only modes -->
<EditString @bind-Value="model.Name" 
           Field="@(() => model.Name)"
           IsEditMode="false"
           IsDisabled="true" />
```

### Custom Labeling
```razor
<!-- Override automatic label generation -->
<EditString @bind-Value="model.Name" 
           Field="@(() => model.Name)"
           Label="<strong>Custom</strong> Label"
           Description="Additional help text" />
```

### Label Visibility Control
```razor
<!-- Hide labels for individual controls -->
<EditString @bind-Value="model.Name" 
           Field="@(() => model.Name)"
           IsLabelHidden="true" />

<!-- Hide labels for all controls using FormOptions -->
<CascadingValue Value="@(new FormOptions { IsLabelHidden = true })">
    <EditString @bind-Value="model.FirstName" Field="@(() => model.FirstName)" />
    <EditString @bind-Value="model.LastName" Field="@(() => model.LastName)" />
    <EditString @bind-Value="model.Email" Field="@(() => model.Email)" />
</CascadingValue>

<!-- Useful for custom form layouts -->
<div class="form-grid">
    <div class="form-labels">
        <label for="first-name">First Name:</label>
        <label for="last-name">Last Name:</label>
        <label for="email">Email:</label>
    </div>
    <div class="form-inputs">
        <EditString @bind-Value="model.FirstName" 
                   Field="@(() => model.FirstName)" 
                   IsLabelHidden="true" 
                   Id="first-name" />
        <EditString @bind-Value="model.LastName" 
                   Field="@(() => model.LastName)" 
                   IsLabelHidden="true" 
                   Id="last-name" />
        <EditString @bind-Value="model.Email" 
                   Field="@(() => model.Email)" 
                   IsLabelHidden="true" 
                   Id="email" />
    </div>
</div>
```

### Styling
```razor
<!-- Add custom CSS classes -->
<EditString @bind-Value="model.Name" 
           Field="@(() => model.Name)"
           ContainerClass="my-custom-wrapper"
           class="my-input-style" />
```

### Conditional Display
```razor
<!-- Hide based on conditions -->
<EditString @bind-Value="model.OptionalField" 
           Field="@(() => model.OptionalField)"
           IsHidden="@(!showOptionalFields)"
           Hiding="HidingMode.WhenNullOrDefault" />
```

### Required Indicator
```razor
<!-- Dynamic required state -->
<EditString @bind-Value="model.ConditionalField" 
           Field="@(() => model.ConditionalField)"
           IsRequired="@(someCondition)" />
```

## Data Annotations Integration

The controls fully support Data Annotations for validation and display:

```csharp
public class UserModel
{
    [Required(ErrorMessage = "Name is required")]
    [MinLength(2, ErrorMessage = "Name must be at least 2 characters")]
    [DisplayName("Full Name")]
    [Description("Enter your complete legal name")]
    public string Name { get; set; } = "";

    [Range(18, 120, ErrorMessage = "Age must be between 18 and 120")]
    [Description("Your age in years")]
    public int Age { get; set; }

    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    [Required]
    public string Email { get; set; } = "";

    [Url(ErrorMessage = "Please enter a valid URL")]
    public string? Website { get; set; }
}
```

## Form Options and Cascading Parameters

Use `FormOptions` to control multiple controls at once:

```razor
<EditForm Model="@model">
    <CascadingValue Value="@formOptions">
        <DataAnnotationsValidator />
        
        <!-- Toggle for all controls -->
        <button type="button" @onclick="ToggleEditMode">
            @(formOptions.IsEditMode ? "View Mode" : "Edit Mode")
        </button>
        
        <!-- Toggle for hiding all labels -->
        <button type="button" @onclick="ToggleLabelVisibility">
            @(formOptions.IsLabelHidden ? "Show Labels" : "Hide Labels")
        </button>
        
        <EditString @bind-Value="model.Name" Field="@(() => model.Name)" />
        <EditString @bind-Value="model.Email" Field="@(() => model.Email)" />
        <EditNumber @bind-Value="model.Age" Field="@(() => model.Age)" />
        
        <!-- Debug mode shows bound values -->
        @if (formOptions.ShowBoundValues)
        {
            <div class="bound-values-debug">
                <h3>Bound Values (Debug)</h3>
                <p>Name: @model.Name</p>
                <p>Email: @model.Email</p>
                <p>Age: @model.Age</p>
            </div>
        }
    </CascadingValue>
</EditForm>

@code {
    FormOptions formOptions = new() 
    { 
        IsEditMode = true,
        IsLabelHidden = false,
        ShowBoundValues = false,
        Hiding = HidingMode.WhenReadOnlyAndNull
    };
    
    void ToggleEditMode() => formOptions.IsEditMode = !formOptions.IsEditMode;
    void ToggleLabelVisibility() => formOptions.IsLabelHidden = !formOptions.IsLabelHidden;
}
```

### Form Group Options

Use `FormGroupOptions` for unique IDs when using multiple instances:

```razor
<CascadingValue Value="@(new FormGroupOptions { Name = "billing" })">
    <EditString @bind-Value="billing.Address" Field="@(() => billing.Address)" />
</CascadingValue>

<CascadingValue Value="@(new FormGroupOptions { Name = "shipping" })">
    <EditString @bind-Value="shipping.Address" Field="@(() => shipping.Address)" />
</CascadingValue>
```

## Hiding Modes

Control when components are displayed based on their values:

```csharp
public enum HidingMode
{
    None = 1,                        // Always show
    WhenReadOnlyAndNull = 2,         // Hide in read-only mode if null
    WhenReadOnlyAndNullOrDefault = 3, // Hide in read-only mode if null or default
    WhenNull = 4,                    // Hide if null (both modes)
    WhenNullOrDefault = 5            // Hide if null or default (both modes)
}
```

```razor
<!-- Hide empty optional fields in read-only mode -->
<EditString @bind-Value="model.OptionalNotes" 
           Field="@(() => model.OptionalNotes)"
           Hiding="HidingMode.WhenReadOnlyAndNullOrDefault" />
```

## Label Hiding Use Cases

The `IsLabelHidden` property is useful in several scenarios:

### Custom Form Layouts
```razor
<!-- Grid layout with external labels -->
<div class="form-grid">
    <label for="fname" class="form-label">First Name</label>
    <EditString @bind-Value="model.FirstName" 
               Field="@(() => model.FirstName)" 
               IsLabelHidden="true" 
               Id="fname" />
    
    <label for="lname" class="form-label">Last Name</label>
    <EditString @bind-Value="model.LastName" 
               Field="@(() => model.LastName)" 
               IsLabelHidden="true" 
               Id="lname" />
</div>

<style>
.form-grid {
    display: grid;
    grid-template-columns: auto 1fr;
    gap: 1rem;
    align-items: center;
}
</style>
```

### Card-Based Forms
```razor
<div class="form-card">
    <h3>Personal Information</h3>
    <p class="card-description">Please provide your basic details</p>
    
    <!-- Hide labels when card title provides context -->
    <CascadingValue Value="@(new FormOptions { IsLabelHidden = true })">
        <EditString @bind-Value="model.FirstName" 
                   Field="@(() => model.FirstName)" 
                   Placeholder="First Name" />
        <EditString @bind-Value="model.LastName" 
                   Field="@(() => model.LastName)" 
                   Placeholder="Last Name" />
        <EditString @bind-Value="model.Email" 
                   Field="@(() => model.Email)" 
                   Placeholder="Email Address" />
    </CascadingValue>
</div>
```

### Inline Forms
```razor
<!-- Search form with hidden labels -->
<div class="search-form">
    <EditString @bind-Value="searchCriteria.Query" 
               Field="@(() => searchCriteria.Query)" 
               IsLabelHidden="true" 
               Placeholder="Search..." />
    <EditSelectString @bind-Value="searchCriteria.Category" 
                      Field="@(() => searchCriteria.Category)" 
                      IsLabelHidden="true" 
                      Options="@categories" />
    <button type="submit">Search</button>
</div>
```

## Accessibility Features

The controls include comprehensive accessibility support:

- **ARIA Attributes**: Automatic `aria-required`, `aria-invalid`, `aria-describedby`
- **Screen Reader Support**: Proper labels and error message associations
- **Keyboard Navigation**: Full keyboard accessibility
- **Focus Management**: Appropriate focus indicators

When using `IsLabelHidden="true"`, ensure you provide alternative labeling methods:

```razor
<!-- Use aria-label when hiding visual labels -->
<EditString @bind-Value="model.Name" 
           Field="@(() => model.Name)"
           IsLabelHidden="true"
           aria-label="Full Name" />

<!-- Or reference external labels -->
<label id="external-label" for="name-input">Full Name</label>
<EditString @bind-Value="model.Name" 
           Field="@(() => model.Name)"
           IsLabelHidden="true"
           Id="name-input"
           aria-labelledby="external-label" />
```

## Custom Styling

### Using CSS Classes

```css
/* Target specific control types */
.edit-string-input {
    border: 2px solid #007bff;
    border-radius: 8px;
}

.edit-control-wrapper {
    margin-bottom: 1rem;
}

.edit-label {
    font-weight: 600;
    color: #333;
}

.edit-validation-message {
    color: #dc3545;
    font-size: 0.875rem;
}
```

### Component-Specific Styling

```razor
<!-- Add custom container class -->
<EditString @bind-Value="model.Name" 
           Field="@(() => model.Name)"
           ContainerClass="highlighted-field"
           class="custom-input" />

<style>
.highlighted-field {
    background-color: #f8f9fa;
    padding: 1rem;
    border-radius: 4px;
}

.custom-input {
    font-size: 1.1rem;
    padding: 0.75rem;
}
</style>
```

## Advanced Usage

### Custom Validation

```csharp
public class CustomValidationModel
{
    [Required]
    [CustomValidation(typeof(CustomValidationModel), nameof(ValidateEmail))]
    public string Email { get; set; } = "";

    public static ValidationResult? ValidateEmail(string email, ValidationContext context)
    {
        if (!email.EndsWith("@company.com"))
        {
            return new ValidationResult("Email must be from company domain");
        }
        return ValidationResult.Success;
    }
}
```

### Conditional Logic

```razor
<EditBool @bind-Value="model.HasAddress" 
          Field="@(() => model.HasAddress)"
          Label="Do you have an address?" />

@if (model.HasAddress)
{
    <EditString @bind-Value="model.Address" 
               Field="@(() => model.Address)"
               IsRequired="true" />
}

<!-- Or use IsHidden property -->
<EditString @bind-Value="model.Address" 
           Field="@(() => model.Address)"
           IsHidden="@(!model.HasAddress)"
           IsRequired="@model.HasAddress" />
```

### Dynamic Options

```razor
<EditSelectString @bind-Value="selectedCountry" 
                  Field="@(() => selectedCountry)"
                  Options="@countries"
                  @onchange="OnCountryChanged" />

<EditSelectString @bind-Value="selectedState" 
                  Field="@(() => selectedState)"
                  Options="@states" />

@code {
    string selectedCountry = "";
    string selectedState = "";
    List<string> countries = ["USA", "Canada", "Mexico"];
    List<string> states = [];

    void OnCountryChanged()
    {
        states = selectedCountry switch
        {
            "USA" => ["California", "Texas", "New York"],
            "Canada" => ["Ontario", "Quebec", "British Columbia"],
            "Mexico" => ["Jalisco", "Nuevo León", "Yucatán"],
            _ => []
        };
        selectedState = "";
    }
}
```

## Best Practices

### 1. Model Design
```csharp
public class WellDesignedModel
{
    [Required]
    [DisplayName("Full Name")]
    [Description("Enter your complete legal name as it appears on official documents")]
    public string FullName { get; set; } = "";

    [Range(18, 120)]
    [Description("Your age in years")]
    public int Age { get; set; }

    [EmailAddress]
    [Required]
    [Description("We'll use this to send you important updates")]
    public string Email { get; set; } = "";
}
```

### 2. Consistent Layouts
```razor
<div class="form-grid">
    <EditString @bind-Value="model.FirstName" Field="@(() => model.FirstName)" />
    <EditString @bind-Value="model.LastName" Field="@(() => model.LastName)" />
    <EditString @bind-Value="model.Email" Field="@(() => model.Email)" />
    <EditDate @bind-Value="model.BirthDate" Field="@(() => model.BirthDate)" />
</div>

<style>
.form-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 1rem;
}

@media (max-width: 768px) {
    .form-grid {
        grid-template-columns: 1fr;
    }
}
</style>
```

### 3. Label Hiding Guidelines
```razor
<!-- Good: Hide labels when using placeholders that provide clear context -->
<EditString @bind-Value="model.Email" 
           Field="@(() => model.Email)" 
           IsLabelHidden="true" 
           Placeholder="Enter your email address" />

<!-- Good: Hide labels in forms with external labeling -->
<fieldset>
    <legend>Contact Information</legend>
    <EditString @bind-Value="model.Phone" 
               Field="@(() => model.Phone)" 
               IsLabelHidden="true" 
               Placeholder="Phone Number" />
</fieldset>

<!-- Avoid: Hiding labels without alternative labeling -->
<EditString @bind-Value="model.SecretField" 
           Field="@(() => model.SecretField)" 
           IsLabelHidden="true" />
```

### 4. Error Handling
```razor
<EditForm Model="@model" OnValidSubmit="HandleValidSubmit" OnInvalidSubmit="HandleInvalidSubmit">
    <DataAnnotationsValidator />
    <ValidationSummary />
    
    <!-- Your form controls here -->
    
    <button type="submit" disabled="@isSubmitting">
        @if (isSubmitting)
        {
            <span>Saving...</span>
        }
        else
        {
            <span>Save</span>
        }
    </button>
</EditForm>

@code {
    bool isSubmitting = false;
    
    async Task HandleValidSubmit()
    {
        isSubmitting = true;
        try
        {
            // Save logic here
            await SaveModel(model);
        }
        finally
        {
            isSubmitting = false;
        }
    }
    
    void HandleInvalidSubmit()
    {
        // Handle validation errors
        Console.WriteLine("Form has validation errors");
    }
}
```

## Troubleshooting

### Common Issues

1. **Missing CSS Styles**: Ensure you've included the CSS reference
2. **Validation Not Working**: Make sure you have `<DataAnnotationsValidator />` in your EditForm
3. **IDs Not Unique**: Use `IdPrefix` or `FormGroupOptions` for multiple instances
4. **Accessibility Issues**: The controls handle ARIA attributes automatically, but when using `IsLabelHidden`, provide alternative labeling
5. **Hidden Labels Not Working**: Ensure you're setting `IsLabelHidden="true"` on individual controls or `IsLabelHidden = true` in FormOptions

### Performance Tips

1. Use `@bind-Value:event="oninput"` for real-time validation
2. Consider using `ObjectGraphDataAnnotationsValidator` for complex objects
3. Use `IsHidden` instead of conditional rendering for better performance
4. When hiding labels globally, use FormOptions cascading parameter instead of setting `IsLabelHidden` on each control

