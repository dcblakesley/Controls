namespace Controls;

/// <summary>
/// Obsolete compile-time guard: <c>EditDatePicker&lt;T&gt;</c> was renamed to <see cref="EditDate{T}"/>
/// — the AntD-style calendar dropdown is now the default date control — and the previous native-input
/// <c>EditDate&lt;T&gt;</c> is now <see cref="EditDateNative{T}"/>. This inert stub exists only so a
/// leftover <c>&lt;EditDatePicker&gt;</c> reference fails the build with a clear, actionable message
/// instead of silently binding to whatever type happens to be named <c>EditDate</c> now. Inheriting
/// <see cref="EditDate{T}"/> (rather than standing alone) keeps every one of its parameters resolving
/// through Razor's type inference, so the <see cref="ObsoleteAttribute"/> diagnostic is the only error
/// a consumer sees — not a cascade of "parameter does not exist" errors for everything the stale
/// markup still sets. Update your markup to <c>&lt;EditDate&gt;</c> (the calendar dropdown, what this
/// control used to be) or <c>&lt;EditDateNative&gt;</c> (if you specifically want the native
/// <c>&lt;input&gt;</c> that used to be called <c>EditDate</c>), then remove this stub reference.
/// </summary>
[Obsolete("EditDatePicker<T> has been renamed to EditDate<T> (the calendar dropdown is now the default date control). The previous native-input EditDate<T> is now EditDateNative<T>.", error: true)]
public class EditDatePicker<T> : EditDate<T>;
