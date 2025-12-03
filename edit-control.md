# Edit Controls Documentation

## Overview

The Edit Controls library provides a comprehensive set of Blazor components for building forms with consistent styling, validation, and behavior. All controls implement the `IEditControl` interface, ensuring a unified API across different input types.

**Key Philosophy**: Use Data Annotations attributes on your model properties to configure controls. Only override settings in markup when necessary for specific UI requirements.

## Table of Contents

- [Quick Start](#quick-start)
- [Available Controls](#available-controls)
- [Common Properties (IEditControl)](#common-properties-ieditcontrol)
- [Configuration Philosophy](#configuration-philosophy)
- [Form Options](#form-options)
- [Control-Specific Features](#control-specific-features)
- [Validation](#validation)
- [Hiding Modes](#hiding-modes)
- [Best Practices](#best-practices)

---

## Quick Start

### Basic Usage

```razor
@using Controls
@using Microsoft.AspNetCore.Components.Forms

<EditForm Model="@_model" OnValidSubmit="HandleValidSubmit">
    <ObjectGraphDataAnnotationsValidator />
    
    <EditString @bind-Value="_model.Name" 
                Field="@(() => _model.Name)" />
    
    <EditBool @bind-Value="_model.IsActive" 
              Field="@(() => _model.IsActive)" />
    
    <EditNumber @bind-Value="_model.Age" 
                Field="@(() => _model.Age)" />
    
    <button type="submit">Submit</button>
</EditForm>

@code {
    private MyModel _model = new();
    
    private void HandleValidSubmit()
    {
        // Handle form submission
    }
}
```

### Model Definition with Data Annotations

```csharp
public class MyModel
{
    [Required]
    [DisplayName("Full Name")]
    [Description("Enter your full legal name")]
    [ToolTip("This will be used for all official correspondence")]
    public string? Name { get; set; }
    
    [DisplayName("Active Status")]
    [Description("Indicates whether this record is currently active")]
    public bool IsActive { get; set; }
    
    [Required]
    [Range(18, 120)]
    [DisplayName("Age")]
    [Description("Must be between 18 and 120 years")]
    public int Age { get; set; }
}
```

**Notice**: All configuration comes from attributes. The controls automatically read and apply these settings.

---

## Available Controls

### Text Input Controls

- **EditString** - Single-line text input with optional masking and URL support
- **EditTextArea** - Multi-line text input with configurable rows

### Boolean Controls

- **EditBool** - Checkbox for boolean values
- **EditBoolNullRadio** - Radio buttons for nullable boolean (Yes/No/Not Set)

### Numeric Controls

- **EditNumber<T>** - Number input with formatting and step values

### Date Controls

- **EditDate<T>** - Date/datetime input with customizable format

### Selection Controls

- **EditSelect<TValue>** - Dropdown with custom options in markup
- **EditSelectEnum<TEnum>** - Dropdown populated from enum values
- **EditSelectString<TValue>** - Dropdown populated from string list

### Radio Button Controls

- **EditRadio<TValue>** - Radio buttons with custom options in markup
- **EditRadioEnum<TEnum>** - Radio buttons populated from enum values
- **EditRadioString** - Radio buttons populated from string list

### Checkbox List Controls

- **EditCheckedStringList** - Multiple checkboxes for selecting strings
- **EditCheckedEnumList<TEnum>** - Multiple checkboxes for selecting enum values

### Display Controls

- **EditDisplay** - Read-only text display with consistent styling

---

## Configuration Philosophy

### ✅ Preferred: Data Annotations

**Always configure controls using Data Annotations on your model properties.** This approach:
- Keeps configuration with your business logic
- Ensures consistency across your application
- Makes controls reusable without duplication
- Enables automatic label, description, and validation generation

```csharp
public class Customer
{
    [Required]
    [DisplayName("Customer Name")]
    [Description("Enter the customer's full legal name")]
    [ToolTip("This name will appear on all invoices and contracts")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string? Name { get; set; }
    
    [Required]
    [DisplayName("Email Address")]
    [Description("Primary contact email for this customer")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    public string? Email { get; set; }
}
```

```razor
<!-- Clean, minimal markup - configuration comes from model -->
<EditString @bind-Value="_customer.Name" Field="@(() => _customer.Name)" />
<EditString @bind-Value="_customer.Email" Field="@(() => _customer.Email)" />
```

### ⚠️ Use Sparingly: Markup Properties

**Only override properties in markup when you have specific UI requirements** that differ from the model's general configuration:

```razor
<!-- Override only when needed for this specific UI context -->
<EditString @bind-Value="_customer.Name" 
            Field="@(() => _customer.Name)"
            Placeholder="e.g., John Smith" 
            ContainerClass="highlight-field" />
```

### ❌ Avoid: Duplicating Data Annotations in Markup

Don't repeat information that's already in your model:

```razor
<!-- ❌ BAD: Duplicating information from model attributes -->
<EditString @bind-Value="_customer.Name" 
            Field="@(() => _customer.Name)"
            Label="Customer Name"
            Description="Enter the customer's full legal name"
            IsRequired="true" />

<!-- ✅ GOOD: Let attributes handle it -->
<EditString @bind-Value="_customer.Name" 
            Field="@(() => _customer.Name)" />
```

### When to Override in Markup

Override in markup only for:

1. **UI-Specific Behavior**: Placeholders, CSS classes specific to this view
2. **Dynamic Requirements**: Conditional hiding based on user permissions
3. **Layout Adjustments**: Hiding labels for space-constrained layouts
4. **Page-Specific Features**: URL links in one view but not another

```razor
<!-- Good use case: Page-specific behavior -->
<EditString @bind-Value="_customer.Email" 
            Field="@(() => _customer.Email)"
            Url="@($"mailto:{_customer.Email}")"
            UrlTarget="_blank"
            IsHidden="@(!_currentUser.CanViewEmail)" />
```

---

## Common Properties (IEditControl)

All edit controls implement these properties from the `IEditControl` interface. **Most of these should be configured via Data Annotations, not in markup.**

### Identification & Labeling

| Property | Type | Configure Via | Description |
|----------|------|---------------|-------------|
| `Id` | `string?` | Markup (rarely) | Custom ID for the control. Auto-generated from property name if not provided. Override only for specific ID requirements. |
| `IdPrefix` | `string?` | FormGroupOptions | Prefix for IDs to avoid duplicates. Use FormGroupOptions instead of setting per-control. |
| `Label` | `string?` | **[DisplayName] attribute** | Override only for page-specific label variations. |
| `Description` | `string?` | **[Description] attribute** | Override only for page-specific descriptions. |
| `Tooltip` | `string?` | **[ToolTip] attribute** | Override only for page-specific tooltips. |

**Example - Preferred approach:**

```csharp
// ✅ Configure in model
[DisplayName("Customer Email")]
[Description("Primary email address for customer communications")]
[ToolTip("We'll send invoices and updates to this address")]
public string? Email { get; set; }
```

```razor
<!-- ✅ Clean markup -->
<EditString @bind-Value="_customer.Email" Field="@(() => _customer.Email)" />
```

### Display & Visibility

| Property | Type | Configure Via | Description |
|----------|------|---------------|-------------|
| `IsEditMode` | `bool` | FormOptions or Markup | Use FormOptions for entire forms, markup for specific controls. |
| `IsHidden` | `bool` | Markup | For conditional display based on UI state or permissions. |
| `IsLabelHidden` | `bool` | FormOptions or Markup | Use FormOptions for entire forms, markup for specific layouts. |
| `Hiding` | `HidingMode?` | FormOptions or Markup | Use FormOptions for consistent behavior across forms. |

### Styling & State

| Property | Type | Configure Via | Description |
|----------|------|---------------|-------------|
| `ContainerClass` | `string?` | Markup | For page-specific styling needs. |
| `IsDisabled` | `bool` | Markup | For dynamic enable/disable based on UI state. |
| `IsRequired` | `bool` | **[Required] attribute** | Auto-detected from validation attributes. Override rarely. |

### Required Property

| Property | Type | Description |
|----------|------|-------------|
| `Field` | `Expression<Func<T>>` | **Always Required**. Expression binding to the model property. Enables validation and auto-configuration. |

---

## Form Options

Use `FormOptions` as a cascading parameter to apply settings to multiple controls at once:

```razor
<CascadingValue Value="@_formOptions">
    <EditForm Model="@_model" OnValidSubmit="HandleValidSubmit">
        <ObjectGraphDataAnnotationsValidator />
        
        <EditString @bind-Value="_model.Name" Field="@(() => _model.Name)" />
        <EditNumber @bind-Value="_model.Age" Field="@(() => _model.Age)" />
        <EditBool @bind-Value="_model.IsActive" Field="@(() => _model.IsActive)" />
        
        <button type="submit">Submit</button>
    </EditForm>
</CascadingValue>

@code {
    private MyModel _model = new();
    
    private FormOptions _formOptions = new()
    {
        IsEditMode = true,
        Hiding = HidingMode.WhenNull,
        IsLabelHidden = false,
        IsRequiredStarHidden = false,
        ShowBoundValues = false // Debug only
    };
}
```

### FormOptions Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsEditMode` | `bool` | Set edit/read-only mode for all controls. Default: `true` |
| `Hiding` | `HidingMode?` | Set hiding mode for all controls. |
| `IsLabelHidden` | `bool` | Hide labels for all controls. |
| `IsRequiredStarHidden` | `bool` | Hide required star indicators. |
| `ShowBoundValues` | `bool` | Debug mode - show bound values. |

### FormGroupOptions

Use `FormGroupOptions` to namespace controls when using multiple instances of the same model:

```razor
<CascadingValue Value="@(new FormGroupOptions { Name = "person1" })">
    <EditString @bind-Value="_person1.Name" Field="@(() => _person1.Name)" />
</CascadingValue>

<CascadingValue Value="@(new FormGroupOptions { Name = "person2" })">
    <EditString @bind-Value="_person2.Name" Field="@(() => _person2.Name)" />
</CascadingValue>
```

---

## Control-Specific Features

**Note**: Control-specific properties often need to be set in markup as they're context-specific rather than model-specific.

### EditString

```razor
<EditString @bind-Value="_model.Email" 
            Field="@(() => _model.Email)"
            Placeholder="user@example.com"
            MaskText="****"
            Url="mailto:user@example.com"
            UrlTarget="_blank" />
```

| Property | Type | Description |
|----------|------|-------------|
| `Placeholder` | `string?` | Placeholder text for empty input. Context-specific, set in markup. |
| `MaskText` | `string?` | Mask displayed value in read-only mode. Context-specific. |
| `Url` | `string?` | Convert read-only value to clickable link. Context-specific. |
| `UrlTarget` | `string?` | Link target attribute (e.g., `_blank`). Context-specific. |

### EditTextArea

```razor
<EditTextArea @bind-Value="_model.Comments" 
              Field="@(() => _model.Comments)"
              Placeholder="Enter comments..."
              Rows="5" />
```

| Property | Type | Description |
|----------|------|-------------|
| `Placeholder` | `string?` | Placeholder text. Context-specific. |
| `Rows` | `int` | Number of visible rows. Context-specific. Default: `2` |

### EditNumber<T>

```razor
<EditNumber @bind-Value="_model.Price" 
            Field="@(() => _model.Price)"
            Step="0.01m"
            Format="C2" />
```

| Property | Type | Description |
|----------|------|-------------|
| `Step` | `decimal` | Increment/decrement step. Context-specific. Default: `1.0` |
| `Format` | `string?` | Format string for read-only display. Context-specific (e.g., "N2", "C2"). |

### EditDate<T>

```razor
<EditDate @bind-Value="_model.BirthDate" 
          Field="@(() => _model.BirthDate)"
          DateFormat="MM-dd-yyyy" />
```

| Property | Type | Description |
|----------|------|-------------|
| `DateFormat` | `string` | Display format for read-only mode. Context-specific. Default: `"MM-dd-yyyy"` |

### EditBool

```razor
<EditBool @bind-Value="_model.AcceptTerms" 
          Field="@(() => _model.AcceptTerms)"
          AllowFocusWhenDisabled="true" />
```

| Property | Type | Description |
|----------|------|-------------|
| `AllowFocusWhenDisabled` | `bool` | Allow focus when disabled. Default: `true` |

### EditBoolNullRadio

```razor
<EditBoolNullRadio @bind-Value="_model.IsApproved" 
                   Field="@(() => _model.IsApproved)"
                   IsHorizontal="true"
                   ShowNullOption="true"
                   TrueText="Approved"
                   FalseText="Rejected"
                   NullText="Pending" />
```

| Property | Type | Description |
|----------|------|-------------|
| `IsHorizontal` | `bool` | Display horizontally. Context-specific. Default: `true` |
| `ShowNullOption` | `bool` | Show null/not set option. Default: `true` |
| `TrueText` | `string` | Text for true option. Context-specific. Default: `"Yes"` |
| `FalseText` | `string` | Text for false option. Context-specific. Default: `"No"` |
| `NullText` | `string` | Text for null option. Context-specific. Default: `"Not Set"` |

### EditSelectEnum<TEnum>

```razor
<EditSelectEnum @bind-Value="_model.Status" 
                Field="@(() => _model.Status)"
                Sort="true" />
```

| Property | Type | Description |
|----------|------|-------------|
| `Sort` | `bool` | Sort alphabetically by display name vs. numeric order. Context-specific. |

### EditSelectString<TValue>

```razor
<EditSelectString @bind-Value="_model.Color" 
                  Field="@(() => _model.Color)"
                  Options="@_colorOptions" />
```

| Property | Type | Description |
|----------|------|-------------|
| `Options` | `List<string>` | **Required**. List of string options. Always set in markup. |

### EditSelect<TValue>

```razor
<EditSelect @bind-Value="_model.PlantId" 
            Field="@(() => _model.PlantId)">
    @foreach (var plant in _plants)
    {
        <option value="@plant.Id">@plant.Name</option>
    }
</EditSelect>
```

Define options in markup using standard `<option>` elements.

### EditRadioEnum<TEnum>

```razor
<EditRadioEnum @bind-Value="_model.Priority" 
               Field="@(() => _model.Priority)"
               IsHorizontal="false"
               Sort="true"
               LabelClass="radio-label"
               HasOtherOption="false"
               OtherPlaceholder="Specify other..."
               @bind-OtherValue="_otherText" />
```

| Property | Type | Description |
|----------|------|-------------|
| `IsHorizontal` | `bool` | Display horizontally. Context-specific. |
| `Sort` | `bool` | Sort alphabetically by display name. Context-specific. |
| `LabelClass` | `string?` | CSS class for radio button labels. Context-specific. |
| `HasOtherOption` | `bool` | Include "Other" with text input. Context-specific. |
| `OtherPlaceholder` | `string?` | Placeholder for "Other" text input. Context-specific. |
| `OtherValue` | `string?` | Value of "Other" text input. |

### EditRadioString

```razor
<EditRadioString @bind-Value="_model.Color" 
                 Field="@(() => _model.Color)"
                 Options="@_colorOptions"
                 IsHorizontal="true"
                 HasOther="true"
                 LabelClass="radio-label" />
```

| Property | Type | Description |
|----------|------|-------------|
| `Options` | `List<string>` | **Required**. List of string options. Always set in markup. |
| `IsHorizontal` | `bool` | Display horizontally. Context-specific. |
| `HasOther` | `bool` | Include "Other" with text input. Context-specific. |
| `LabelClass` | `string?` | CSS class for radio button labels. Context-specific. |

### EditRadio<TValue>

```razor
<EditRadio @bind-Value="_model.ProductId" 
           Field="@(() => _model.ProductId)"
           IsHorizontal="false">
    @foreach (var product in _products)
    {
        <label class="edit-radio-label">
            <InputRadio Value="@product.Id" />
            @product.Name
        </label>
    }
</EditRadio>
```

| Property | Type | Description |
|----------|------|-------------|
| `IsHorizontal` | `bool` | Display horizontally. Context-specific. |

Define options in markup using `InputRadio` components.

### EditCheckedStringList

```razor
<EditCheckedStringList @bind-Value="_model.SelectedItems" 
                       Field="@(() => _model.SelectedItems)"
                       Options="@_itemOptions"
                       IsHorizontal="false"
                       LabelClass="checkbox-label"
                       Css="custom-class" />
```

| Property | Type | Description |
|----------|------|-------------|
| `Value` | `List<string>` | **Required**. Current selection. |
| `Options` | `List<string>` | List of available options. Always set in markup. |
| `IsHorizontal` | `bool` | Display horizontally. Context-specific. |
| `LabelClass` | `string?` | CSS class for checkbox labels. Context-specific. |
| `Css` | `string` | Additional CSS classes. Context-specific. |

### EditCheckedEnumList<TEnum>

```razor
<EditCheckedEnumList @bind-Value="_model.SelectedFeatures" 
                     Field="@(() => _model.SelectedFeatures)"
                     Sort="true"
                     IsHorizontal="false"
                     LabelClass="checkbox-label" />
```

| Property | Type | Description |
|----------|------|-------------|
| `Value` | `List<TEnum>` | **Required**. Current selection. |
| `Sort` | `bool` | Sort alphabetically by display name. Context-specific. |
| `IsHorizontal` | `bool` | Display horizontally. Context-specific. |
| `LabelClass` | `string?` | CSS class for checkbox labels. Context-specific. |

### EditDisplay

```razor
<EditDisplay Label="Calculated Total" 
             Text="@_model.CalculatedTotal.ToString("C2")"
             Id="total-display"
             Description="This is a read-only calculated value"
             Tooltip="Sum of all items"
             ContainerClass="highlight"
             Class="text-bold"
             IsRequired="false"
             IsHidden="false" />
```

| Property | Type | Description |
|----------|------|-------------|
| `Text` | `string` | Text to display. Default: `""` |
| `Class` | `string?` | CSS class for the text element. Context-specific. |

---

## Validation

Edit controls automatically integrate with Blazor's validation system when used within an `EditForm`.

### Choosing the Right Validator

**Always use `<ObjectGraphDataAnnotationsValidator />`** instead of `<DataAnnotationsValidator />`:

```razor
<!-- ✅ RECOMMENDED: Use ObjectGraphDataAnnotationsValidator -->
<EditForm Model="@_model" OnValidSubmit="HandleValidSubmit">
    <ObjectGraphDataAnnotationsValidator />
    <!-- Controls here -->
</EditForm>

<!-- ❌ AVOID: Basic DataAnnotationsValidator has limitations -->
<EditForm Model="@_model" OnValidSubmit="HandleValidSubmit">
    <DataAnnotationsValidator />
    <!-- Controls here -->
</EditForm>
```

**Why ObjectGraphDataAnnotationsValidator?**

- ✅ Validates nested objects and complex object graphs
- ✅ Supports collections and lists
- ✅ Validates all levels of your model hierarchy
- ✅ Required for models with nested classes or child objects
- ✅ Works with all scenarios where `DataAnnotationsValidator` works, plus more

**Example of nested validation:**

```csharp
public class CustomerForm
{
    [Required]
    public string? CompanyName { get; set; }
    
    // ObjectGraphDataAnnotationsValidator will validate this nested object
    [Required]
    [ValidateComplexType]
    public ContactInfo Contact { get; set; } = new();
    
    public class ContactInfo
    {
        [Required]
        [EmailAddress]
        public string? Email { get; set; }
        
        [Phone]
        public string? Phone { get; set; }
    }
}
```

### Data Annotations for Validation

**Always use Data Annotations for validation rules:**

```csharp
public class MyModel
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(50, ErrorMessage = "Name must be less than 50 characters")]
    [DisplayName("Full Name")]
    [Description("Enter your legal name")]
    public string? Name { get; set; }
    
    [Required]
    [Range(1, 100, ErrorMessage = "Age must be between 1 and 100")]
    [DisplayName("Age")]
    public int Age { get; set; }
    
    [Required]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [DisplayName("Email Address")]
    [Description("We'll use this for account notifications")]
    public string? Email { get; set; }
}
```

```razor
<!-- Clean markup - validation comes from model -->
<EditForm Model="@_model" OnValidSubmit="HandleValidSubmit">
    <ObjectGraphDataAnnotationsValidator />
    <ValidationSummary />
    
    <EditString @bind-Value="_model.Name" Field="@(() => _model.Name)" />
    <ValidationMessage For="@(() => _model.Name)" />
    
    <EditNumber @bind-Value="_model.Age" Field="@(() => _model.Age)" />
    <ValidationMessage For="@(() => _model.Age)" />
    
    <EditString @bind-Value="_model.Email" Field="@(() => _model.Email)" />
    <ValidationMessage For="@(() => _model.Email)" />
    
    <button type="submit">Submit</button>
</EditForm>
```

### Validating Nested Objects

When working with nested objects, use `[ValidateComplexType]` attribute:

```csharp
public class Order
{
    [Required]
    [DisplayName("Order Number")]
    public string? OrderNumber { get; set; }
    
    // Validates the nested Customer object
    [Required]
    [ValidateComplexType]
    public Customer Customer { get; set; } = new();
    
    // Validates each item in the collection
    [ValidateComplexType]
    public List<OrderItem> Items { get; set; } = new();
}

public class Customer
{
    [Required]
    [DisplayName("Customer Name")]
    public string? Name { get; set; }
    
    [Required]
    [EmailAddress]
    public string? Email { get; set; }
}

public class OrderItem
{
    [Required]
    [DisplayName("Product Name")]
    public string? ProductName { get; set; }
    
    [Required]
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}
```

```razor
<EditForm Model="@_order" OnValidSubmit="HandleValidSubmit">
    <ObjectGraphDataAnnotationsValidator />
    <ValidationSummary />
    
    <EditString @bind-Value="_order.OrderNumber" Field="@(() => _order.OrderNumber)" />
    
    <h3>Customer Information</h3>
    <EditString @bind-Value="_order.Customer.Name" Field="@(() => _order.Customer.Name)" />
    <EditString @bind-Value="_order.Customer.Email" Field="@(() => _order.Customer.Email)" />
    
    <button type="submit">Submit</button>
</EditForm>
```

### Required Field Indicator

The required star (*) is automatically shown when:
- The property has a `[Required]` attribute - **Preferred approach**
- `IsRequired` is set to `true` on the control - Only for special cases
- `FormOptions.IsRequiredStarHidden` is `false`

**✅ Good: Use data annotation**
```csharp
[Required]
[DisplayName("Customer Name")]
public string? Name { get; set; }
```

```razor
<!-- ✅ Clean, maintainable markup -->
<EditString @bind-Value="_model.Name" Field="@(() => _model.Name)" />
```

```razor
<!-- ❌ AVOID: Setting in markup -->
<EditString @bind-Value="_model.Name" 
            Field="@(() => _model.Name)"
            IsRequired="true" />
```

---

## Hiding Modes

The `HidingMode` enum provides flexible control visibility options:

### HidingMode.None (Default)
Always show the control.

```razor
<EditString @bind-Value="_model.Name" 
            Field="@(() => _model.Name)"
            Hiding="HidingMode.None" />
```

### HidingMode.WhenNull
Hide when value is null (both edit and read-only modes).

```razor
<EditString @bind-Value="_model.MiddleName" 
            Field="@(() => _model.MiddleName)"
            Hiding="HidingMode.WhenNull" />
```

### HidingMode.WhenNullOrDefault
Hide when value is null or default (e.g., empty string, 0, false).

```razor
<EditNumber @bind-Value="_model.OptionalCount" 
            Field="@(() => _model.OptionalCount)"
            Hiding="HidingMode.WhenNullOrDefault" />
```

### HidingMode.WhenReadOnlyAndNull
Hide in read-only mode when value is null. Always show in edit mode.

```razor
<EditString @bind-Value="_model.Notes" 
            Field="@(() => _model.Notes)"
            Hiding="HidingMode.WhenReadOnlyAndNull" />
```

### HidingMode.WhenReadOnlyAndNullOrDefault
Hide in read-only mode when value is null or default. Always show in edit mode.

```razor
<EditString @bind-Value="_model.Comments" 
            Field="@(() => _model.Comments)"
            Hiding="HidingMode.WhenReadOnlyAndNullOrDefault" />
```

### Apply to Multiple Controls with FormOptions

**Preferred approach for consistent hiding behavior:**

```razor
@code {
    private FormOptions _formOptions = new()
    {
        Hiding = HidingMode.WhenReadOnlyAndNull
    };
}

<CascadingValue Value="@_formOptions">
    <EditString @bind-Value="_model.Name" Field="@(() => _model.Name)" />
    <EditString @bind-Value="_model.Email" Field="@(() => _model.Email)" />
    <!-- Both controls will use HidingMode.WhenReadOnlyAndNull -->
</CascadingValue>
```

---

## Best Practices

### 1. Always Use Data Annotations First

**The most important rule**: Configure controls using Data Annotations, not markup properties.

```csharp
// ✅ BEST: Configure everything in the model
public class Product
{
    [Required]
    [DisplayName("Product Name")]
    [Description("Enter a unique, descriptive name for this product")]
    [ToolTip("This name will appear on invoices and reports")]
    [StringLength(100)]
    public string? Name { get; set; }
    
    [Required]
    [DisplayName("Unit Price")]
    [Description("Price per unit in USD")]
    [Range(0.01, 999999.99)]
    [DataType(DataType.Currency)]
    public decimal Price { get; set; }
}
```

```razor
<!-- ✅ Clean, maintainable markup -->
<EditString @bind-Value="_product.Name" Field="@(() => _product.Name)" />
<EditNumber @bind-Value="_product.Price" Field="@(() => _product.Price)" Format="C2" />
```

```razor
<!-- ❌ AVOID: Duplicating model configuration in markup -->
<EditString @bind-Value="_product.Name" 
            Field="@(() => _product.Name)"
            Label="Product Name"
            Description="Enter a unique, descriptive name for this product"
            IsRequired="true" />
```

### 2. Override in Markup Only for UI-Specific Needs

Override properties in markup only when the requirement is specific to this particular UI context:

```razor
<!-- ✅ Good: Context-specific overrides -->
<EditString @bind-Value="_product.Name" 
            Field="@(() => _product.Name)"
            Placeholder="e.g., Premium Laptop Stand"
            ContainerClass="featured-field"
            IsHidden="@(!_user.CanEditProducts)" />
```

### 3. Use FormOptions for Consistent Behavior

Apply settings to multiple controls at once using FormOptions instead of repeating them:

```razor
<!-- ✅ Good: Consistent behavior via FormOptions -->
<CascadingValue Value="@_formOptions">
    <EditForm Model="@_model">
        <EditString @bind-Value="_model.Name" Field="@(() => _model.Name)" />
        <EditString @bind-Value="_model.Email" Field="@(() => _model.Email)" />
    </EditForm>
</CascadingValue>

<!-- ❌ Bad: Repeating settings -->
<EditString @bind-Value="_model.Name" 
            Field="@(() => _model.Name)"
            IsLabelHidden="true" />
<EditString @bind-Value="_model.Email" 
            Field="@(() => _model.Email)"
            IsLabelHidden="true" />
```

### 4. Always Include the Field Parameter

The `Field` parameter enables automatic configuration from Data Annotations:

```razor
<!-- ✅ Good: Field parameter enables auto-configuration -->
<EditString @bind-Value="_model.Name" Field="@(() => _model.Name)" />

<!-- ❌ Bad: Missing Field parameter loses all auto-configuration -->
<EditString @bind-Value="_model.Name" />
```

### 5. Leverage Enum Display Attributes

Use `[Display]` attributes for user-friendly enum labels instead of hardcoding text:

```csharp
// ✅ Good: Configure in enum
public enum Status
{
    [Display(Name = "Pending Review")]
    PendingReview,
    
    [Display(Name = "In Progress")]
    InProgress,
    
    [Display(Name = "Completed")]
    Completed
}
```

```razor
<!-- ✅ Clean markup -->
<EditSelectEnum @bind-Value="_model.Status" Field="@(() => _model.Status)" />
```

### 6. Organize Complex Models with Nested Classes

Keep your Data Annotations organized by using nested classes for complex models. **Remember to use `[ValidateComplexType]` and `<ObjectGraphDataAnnotationsValidator />`:**

```csharp
public class CustomerForm
{
    [Required]
    [ValidateComplexType]
    public PersonalInfo Personal { get; set; } = new();
    
    [Required]
    [ValidateComplexType]
    public ContactInfo Contact { get; set; } = new();
    
    public class PersonalInfo
    {
        [Required]
        [DisplayName("First Name")]
        [Description("Customer's legal first name")]
        public string? FirstName { get; set; }
        
        [Required]
        [DisplayName("Last Name")]
        public string? LastName { get; set; }
    }
    
    public class ContactInfo
    {
        [Required]
        [DisplayName("Email Address")]
        [Description("Primary contact email")]
        [EmailAddress]
        public string? Email { get; set; }
        
        [Phone]
        [DisplayName("Phone Number")]
        public string? Phone { get; set; }
    }
}
```

```razor
<EditForm Model="@_customerForm" OnValidSubmit="HandleValidSubmit">
    <ObjectGraphDataAnnotationsValidator />
    <ValidationSummary />
    
    <section>
        <h3>Personal Information</h3>
        <EditString @bind-Value="_customerForm.Personal.FirstName" 
                    Field="@(() => _customerForm.Personal.FirstName)" />
        <EditString @bind-Value="_customerForm.Personal.LastName" 
                    Field="@(() => _customerForm.Personal.LastName)" />
    </section>
    
    <section>
        <h3>Contact Information</h3>
        <EditString @bind-Value="_customerForm.Contact.Email" 
                    Field="@(() => _customerForm.Contact.Email)" />
        <EditString @bind-Value="_customerForm.Contact.Phone" 
                    Field="@(() => _customerForm.Contact.Phone)" />
    </section>
    
    <button type="submit">Submit</button>
</EditForm>
```

### 7. Use Custom Validation Attributes

Create custom validation attributes for business rules instead of validating in markup:

```csharp
// ✅ Good: Reusable validation attribute
public class FutureDateAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value is DateTime date && date <= DateTime.Now)
        {
            return new ValidationResult("Date must be in the future");
        }
        return ValidationResult.Success;
    }
}

public class Event
{
    [Required]
    [FutureDate]
    [DisplayName("Event Date")]
    public DateTime EventDate { get; set; }
}
```

### 8. Document Your Model with Attributes

Use Data Annotations as documentation for your model:

```csharp
/// <summary>
/// Represents a customer order in the system.
/// </summary>
public class Order
{
    [Required]
    [DisplayName("Order Number")]
    [Description("Unique identifier for this order")]
    [ToolTip("Auto-generated when order is saved")]
    public string? OrderNumber { get; set; }
    
    [Required]
    [DisplayName("Order Date")]
    [Description("Date this order was placed")]
    [DataType(DataType.Date)]
    public DateTime OrderDate { get; set; }
}
```

### 9. Handle Validation Properly

Always include validators and handle invalid submissions. **Use `<ObjectGraphDataAnnotationsValidator />`:**

```razor
<EditForm Model="@_model" 
          OnValidSubmit="HandleValidSubmit" 
          OnInvalidSubmit="HandleInvalidSubmit">
    <ObjectGraphDataAnnotationsValidator />
    <ValidationSummary />
    
    <!-- Controls here -->
    
    <button type="submit">Submit</button>
</EditForm>

@code {
    private async Task HandleValidSubmit()
    {
        // Process valid form
    }
    
    private async Task HandleInvalidSubmit()
    {
        // Focus first invalid field or show error message
    }
}
```

### 10. Use Appropriate Control Types

Choose the right control for your data type:

| Data Type | Recommended Control |
|-----------|-------------------|
| `string` (short) | `EditString` |
| `string` (long) | `EditTextArea` |
| `bool` | `EditBool` |
| `bool?` | `EditBoolNullRadio` |
| `int`, `decimal`, `double` | `EditNumber<T>` |
| `DateTime`, `DateTimeOffset` | `EditDate<T>` |
| Enum (single select) | `EditSelectEnum<T>` or `EditRadioEnum<T>` |
| Enum (multi-select) | `EditCheckedEnumList<T>` |
| String list (single select) | `EditSelectString` or `EditRadioString` |
| String list (multi-select) | `EditCheckedStringList` |

---

## Common Patterns

### Master-Detail Forms

```razor
<EditSelectEnum @bind-Value="_selectedCategory" 
                Field="@(() => _selectedCategory)" />

@if (_selectedCategory != Category.None)
{
    <EditSelectString @bind-Value="_selectedItem" 
                      Field="@(() => _selectedItem)"
                      Options="@GetItemsForCategory(_selectedCategory)" />
}
```

### Conditional Fields

```razor
<EditBool @bind-Value="_model.HasMiddleName" 
          Field="@(() => _model.HasMiddleName)" />

@if (_model.HasMiddleName)
{
    <EditString @bind-Value="_model.MiddleName" 
                Field="@(() => _model.MiddleName)" />
}
```

### Search and Filter

```razor
<EditString @bind-Value="_searchTerm" 
            Field="@(() => _searchTerm)"
            Placeholder="Search..."
            @bind-Value:after="PerformSearch" />

<EditCheckedEnumList @bind-Value="_selectedCategories" 
                     Field="@(() => _selectedCategories)"
                     @bind-Value:after="FilterResults" />
```

---

## Troubleshooting

### Issue: Labels not showing or incorrect

**Solution**: Ensure the `Field` parameter is provided and the model property has appropriate Data Annotations.

```csharp
// ✅ Correct
[DisplayName("Customer Name")]
public string? Name { get; set; }
```

```razor
<!-- ✅ Correct -->
<EditString @bind-Value="_model.Name" Field="@(() => _model.Name)" />
```

### Issue: Validation not working

**Solution**: Verify `<ObjectGraphDataAnnotationsValidator />` is included and validation attributes are on the model.

```csharp
// ✅ Add validation attributes
[Required]
[EmailAddress]
public string? Email { get; set; }
```

```razor
<EditForm Model="@_model" OnValidSubmit="HandleValidSubmit">
    <ObjectGraphDataAnnotationsValidator /> <!-- Required -->
    <EditString @bind-Value="_model.Email" Field="@(() => _model.Email)" />
</EditForm>
```

### Issue: Nested object validation not working

**Solution**: Use `<ObjectGraphDataAnnotationsValidator />` instead of `<DataAnnotationsValidator />` and add `[ValidateComplexType]` to nested properties.

```csharp
// ✅ Add ValidateComplexType attribute
public class Order
{
    [Required]
    public string? OrderNumber { get; set; }
    
    [Required]
    [ValidateComplexType]  // This is required for nested validation
    public Customer Customer { get; set; } = new();
}

public class Customer
{
    [Required]
    public string? Name { get; set; }
}
```

```razor
<!-- ✅ Use ObjectGraphDataAnnotationsValidator -->
<EditForm Model="@_order" OnValidSubmit="HandleValidSubmit">
    <ObjectGraphDataAnnotationsValidator />
    <EditString @bind-Value="_order.Customer.Name" 
                Field="@(() => _order.Customer.Name)" />
</EditForm>
```

### Issue: Configuration not applying

**Solution**: Check that you're using Data Annotations and have the `Field` parameter set.

```csharp
// ✅ Configure in model
[DisplayName("Full Name")]
[Description("Enter your legal name")]
public string? Name { get; set; }
```

```razor
<!-- ✅ Field parameter is required -->
<EditString @bind-Value="_model.Name" Field="@(() => _model.Name)" />
```

### Issue: Duplicate IDs on the page

**Solution**: Use FormGroupOptions instead of setting IdPrefix on individual controls.

```razor
<CascadingValue Value="@(new FormGroupOptions { Name = "form1" })">
    <EditString @bind-Value="_model1.Name" Field="@(() => _model1.Name)" />
</CascadingValue>

<CascadingValue Value="@(new FormGroupOptions { Name = "form2" })">
    <EditString @bind-Value="_model2.Name" Field="@(() => _model2.Name)" />
</CascadingValue>
```

### Issue: Required star not showing

**Solution**: Verify the `[Required]` attribute is on the property, not set via markup.

```csharp
// ✅ Correct
[Required]
public string? Name { get; set; }
```

---

## Advanced Topics

### Custom Styling

All controls generate consistent HTML structure with predictable CSS classes:

```css
.edit-control-wrapper { /* Container for each control */ }
.edit-control-label { /* Label element */ }
.edit-control-description { /* Description text */ }
.edit-control-input { /* Input element */ }
.edit-control-readonly { /* Read-only value display */ }
```

### Dynamic Form Generation

Controls work well with dynamic model generation:

```razor
@foreach (var field in _model.GetType().GetProperties())
{
    <DynamicControl Property="field" Model="_model" />
}
```

### Integration with JavaScript

Controls support standard Blazor JavaScript interop:

```razor
<EditString @bind-Value="_model.Name" 
            Field="@(() => _model.Name)"
            Id="name-input" />

@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JS.InvokeVoidAsync("focusElement", "name-input");
        }
    }
}
```

---

## Summary

The Edit Controls library provides:

- ✅ **Consistent API** - All controls implement `IEditControl`
- ✅ **Automatic Configuration** - Reads attributes from model properties
- ✅ **Declarative Configuration** - Use Data Annotations over markup properties
- ✅ **Flexible Styling** - Standard CSS classes with customization options
- ✅ **Validation Integration** - Works seamlessly with Blazor validation (use `<ObjectGraphDataAnnotationsValidator />`)
- ✅ **Edit/Display Modes** - Easy switching between input and read-only views
- ✅ **Smart Hiding** - Multiple hiding modes based on value or state
- ✅ **Type Safety** - Generic controls for type-specific behavior
- ✅ **Extensible** - Easy to customize and extend
- ✅ **Nested Object Support** - Full validation support for complex object graphs

**Remember**: 
- Configure with Data Annotations in your model
- Use `<ObjectGraphDataAnnotationsValidator />` for all forms
- Add `[ValidateComplexType]` to nested objects
- Override in markup only when necessary for specific UI requirements

Use these controls to build robust, maintainable forms with minimal code and maximum consistency.
