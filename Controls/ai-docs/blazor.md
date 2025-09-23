# Blazor Development Agent

## Purpose
Blazor WebAssembly component development guidance for Hatch.External.Client using WssBlazorControls and AntBlazor.

## Component Priority
1. **WssBlazorControls** - First choice for forms
2. **AntBlazor** - Only for: Multiselect, Popconfirm, Modal, Notification
3. **Native HTML** - When EditControls insufficient
4. **Custom Components** - Last resort

## Form Controls

```razor
<!-- Primary Controls -->
<EditString @bind-Value="model.Name" Field="@(() => model.Name)" />
<EditTextArea @bind-Value="model.Description" Field="@(() => model.Description)" />
<EditNumber @bind-Value="model.Amount" Field="@(() => model.Amount)" Format="C" />
<EditBool @bind-Value="model.IsActive" Field="@(() => model.IsActive)" />
<EditDate @bind-Value="model.Date" Field="@(() => model.Date)" />
<EditSelectEnum @bind-Value="model.Status" Field="@(() => model.Status)" Type="typeof(Status)" />

<!-- Specialized Controls -->
<EditBoolNullRadio @bind-Value="model.IsApproved" Field="@(() => model.IsApproved)" />
<EditRadioEnum @bind-Value="model.Priority" Field="@(() => model.Priority)" Type="typeof(Priority)" />
<EditCheckedStringList @bind-Value="model.Tags" Field="@(() => model.Tags)" Options="tagOptions" />
```

## Layout Patterns

```razor
<!-- Column Layout -->
<div class="flex-column-16">
    <EditString @bind-Value="model.Name" Field="@(() => model.Name)" />
    <EditTextArea @bind-Value="model.Description" Field="@(() => model.Description)" />
</div>

<!-- Accessible Form Groups -->
<fieldset class="card flex-column-16">
    <legend>Contact Information</legend>
    <EditString @bind-Value="model.Email" Field="@(() => model.Email)" />
    <EditString @bind-Value="model.Phone" Field="@(() => model.Phone)" />
</fieldset>
```

## Code Organization

```csharp
// Component.razor.cs
public partial class Component : ComponentBase
{
    [Parameter] public Model Data { get; set; } = new();
    [Parameter] public bool IsReadOnly { get; set; }
    
    private async Task HandleSubmit()
    {
        // Business logic here
    }
}
```

## Custom Components

```csharp
// Two-way binding pattern
public partial class CustomInput : ComponentBase
{
    [Parameter] public string Value { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    private async Task OnValueChanged(string newValue)
    {
        Value = newValue;
        await ValueChanged.InvokeAsync(Value);
    }
}
```

## Best Practices

1. **EditControls First** - Use WssBlazorControls before alternatives
2. **Code-Behind** - Keep logic in .razor.cs files
3. **Use existing CSS classes** - No custom CSS
4. **Accessibility** - Use fieldset/legend for form groups
5. **Two-Way Binding** - Implement @bind-Value for custom components
