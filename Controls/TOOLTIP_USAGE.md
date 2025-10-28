# Tooltip Usage Guide

## Overview
All EditControls now support tooltips that can be set either through model attributes or directly in HTML markup. Tooltips appear as an info icon next to the label and display helpful text when hovered.

## Setting Tooltips via Attribute

Add the `[ToolTip]` attribute to your model property:

```csharp
public class MyModel
{
    [ToolTip("Enter your full name as it appears on official documents")]
    [DisplayName("Full Name")]
  public string FullName { get; set; }
}
```

## Setting Tooltips in HTML

Pass the `Tooltip` parameter directly to any EditControl:

```razor
<EditString @bind-Value="model.Email"
     Field="@(() => model.Email)"
     Tooltip="We'll never share your email with anyone else" />
```

## Overriding Attribute Tooltips

HTML tooltips take precedence over attribute tooltips:

```csharp
// In your model:
[ToolTip("This will be overridden")]
public string MyField { get; set; }
```

```razor
<!-- In your razor component: -->
<EditString @bind-Value="model.MyField"
            Field="@(() => model.MyField)"
         Tooltip="This tooltip will be shown instead" />
```

## Supported Controls

The `Tooltip` parameter is available on all EditControls:

- `EditString`
- `EditTextArea`
- `EditNumber<T>`
- `EditDate<T>`
- `EditBool`
- `EditBoolNullRadio`
- `EditSelect<TValue>`
- `EditSelectString<TValue>`
- `EditSelectEnum<TEnum>`
- `EditRadio<TValue>`
- `EditRadioString`
- `EditRadioEnum<TEnum>`
- `EditCheckedStringList`
- `EditCheckedEnumList<TEnum>`
- `EditDisplay` (for consistency)

## Example

```csharp
public class RegistrationForm
{
    [DisplayName("Username")]
    [ToolTip("Must be 5-20 characters, letters and numbers only")]
    public string Username { get; set; }
    
    [DisplayName("Password")]
    public string Password { get; set; }
}
```

```razor
<EditForm Model="model">
    <!-- Uses tooltip from attribute -->
    <EditString @bind-Value="model.Username"
    Field="@(() => model.Username)" />
    
 <!-- Sets tooltip in HTML -->
    <EditString @bind-Value="model.Password"
      Field="@(() => model.Password)"
      Tooltip="Must contain at least 8 characters with uppercase, lowercase, and numbers" />
</EditForm>
```

## Styling

Tooltips use the `.edit-tooltip-container` and `.edit-tooltip-content` CSS classes. You can customize their appearance in your stylesheet.
