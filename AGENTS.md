# AGENTS.md

This file provides metadata and instructions for AI coding assistants working with this repository.

## Repository Overview

This repository contains **WssBlazorControls**, a comprehensive library of form controls for Blazor applications. The library provides a consistent, feature-rich set of input components with built-in validation, accessibility support, and flexible styling options.

### Key Features

- **Rich Form Controls**: String, Number, Date, Boolean, Select, Radio, Checkbox lists, and TextArea components
- **Data Annotations Integration**: Full support for validation attributes (Required, Range, MinLength, etc.)
- **Accessibility First**: ARIA attributes, screen reader support, and keyboard navigation
- **Flexible Display Modes**: Edit mode and read-only views for all controls
- **Consistent Styling**: CSS classes and customizable appearance
- **TypeScript/JavaScript Interop**: For enhanced client-side functionality

## Project Structure

The repository is organized into three main projects:

### 1. Controls (`Controls/Controls.csproj`)
- **Primary Component Library** - The main Blazor Razor Class Library
- **Target Framework**: .NET 8.0
- **Package ID**: WssBlazorControls
- **Version**: 1.0.13.2

Key directories:
- `Controls/` - Main component implementations (EditString, EditNumber, etc.)
- `Controls/Demo/` - Demo components and example models
- `Controls/Helpers/` - Utility classes for validation, attributes, and enums
- `wwwroot/` - Static assets including CSS files

### 2. FormTesting (`FormTesting/FormTesting/FormTesting.csproj`)
- **Blazor Server Test Application** - For testing and demonstrating controls
- **Target Framework**: .NET 9.0

### 3. FormTesting.Client (`FormTesting/FormTesting.Client/FormTesting.Client.csproj`)
- **Blazor WebAssembly Client** - Client-side testing application
- **Target Framework**: .NET 9.0

## Component Architecture

### Core Interface: `IEditControl`
All form controls implement the `IEditControl` interface, which provides:

- **Identity**: `Id`, `IdPrefix` for unique identification
- **Display Control**: `IsEditMode`, `IsDisabled`, `IsHidden`
- **Labeling**: `Label`, `Description` with markup support
- **Styling**: `ContainerClass` for custom CSS
- **Validation**: `IsRequired` integration
- **Conditional Display**: `Hiding` modes and `HidingMode` enum

### Key Components

#### Input Controls
- `EditString` - Text input with masking and URL support
- `EditTextArea` - Multi-line text input
- `EditNumber` - Numeric input with validation
- `EditDate` - Date picker component
- `EditBool` - Checkbox for boolean values
- `EditBoolNullRadio` - Three-state radio for nullable booleans

#### Selection Controls
- `EditSelect` - Dropdown selection for objects
- `EditSelectEnum` - Dropdown for enum values
- `EditSelectString` - Dropdown for string values
- `EditRadio` - Radio buttons for objects
- `EditRadioEnum` - Radio buttons for enums
- `EditRadioString` - Radio buttons for strings

#### Multi-Selection Controls
- `EditCheckedStringList` - Checkbox list for strings
- `EditCheckedEnumList` - Checkbox list for enums

#### Support Components
- `FormLabel` - Consistent labeling with tooltips and descriptions
- `FieldValidationDisplay` - Validation message display
- `ReadOnlyValue` - Read-only value presentation

## Development Guidelines

### When Adding New Components

1. **Implement `IEditControl`** - Ensure all new form controls implement the core interface
2. **Follow Naming Convention** - Use `Edit[Type]` pattern for input components
3. **Include Demo Component** - Create corresponding `Demo[ComponentName]` for testing
4. **Add to Models.cs** - Include properties in demo models for comprehensive testing
5. **Accessibility First** - Include proper ARIA attributes and keyboard support
6. **Validation Integration** - Support DataAnnotations validation attributes

### Code Style & Patterns

- **Razor Components**: Use `.razor` files for components with HTML markup
- **Code-Behind**: Use `.razor.cs` files for complex component logic
- **CSS Isolation**: Component-specific styles in `.razor.css` files
- **Global Styles**: Shared styles in `wwwroot/editControls.css`

### Testing Approach

The `FormTesting` projects provide comprehensive testing environments:
- **Interactive Demos** - Live testing of all component features
- **Validation Testing** - Complete DataAnnotations validation scenarios
- **Accessibility Testing** - Screen reader and keyboard navigation verification
- **Visual Testing** - Styling and responsive behavior validation

## Common Tasks

### Adding a New Form Control

1. Create the main component in `Controls/` (e.g., `EditNewControl.razor`)
2. Implement `IEditControl` interface
3. Add corresponding demo in `Controls/Demo/` (e.g., `DemoEditNewControl.razor`)
4. Add test properties to `DemoModelForEditControls` in `Models.cs`
5. Update `EditControlsDemo.razor` to include the new demo
6. Add appropriate CSS styling
7. Update version number in `Controls.csproj`

### Fixing Validation Issues

Common validation problems mentioned in the Todo:
- Missing `invalid` CSS class application
- Incomplete ARIA attribute setup
- Missing pointer cursor styles for interactive elements

### Working with CSS and Styling

- **Component Wrappers**: Use `edit-control-wrapper` class for consistent container styling
- **Validation States**: Apply `invalid` class for validation errors
- **Interactive Elements**: Add `cursor: pointer` for clickable elements
- **Accessibility**: Ensure sufficient color contrast and focus indicators

## Dependencies

### NuGet Packages
- `Microsoft.AspNetCore.Components.DataAnnotations.Validation` (3.2.0-rc1.20223.4)
- `Microsoft.AspNetCore.Components.Web` (8.0.19)

### Browser Support
- Modern browsers with WebAssembly support
- Designed for both Blazor Server and Blazor WebAssembly scenarios

## AI Assistant Guidelines

When working with this codebase:

1. **Maintain Consistency** - Follow established patterns in existing components
2. **Preserve Accessibility** - Always include proper ARIA attributes and labels
3. **Test Integration** - Ensure new components work in both demo applications
4. **Validation Support** - Implement comprehensive DataAnnotations validation
5. **CSS Architecture** - Use existing CSS class patterns and maintain visual consistency
6. **Documentation** - Update demo components to showcase new features

### Common Fix Patterns

- **Validation Display**: Ensure components show validation errors with proper styling
- **Accessibility**: Add missing ARIA attributes and improve screen reader support
- **Styling Issues**: Apply consistent cursor styles and visual feedback
- **Integration**: Verify components work properly with EditContext and validation

This repository represents a mature, production-ready component library with a strong focus on accessibility, validation, and developer experience. Maintain these standards when making modifications or additions.