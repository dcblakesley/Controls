namespace Controls.Helpers;

public static class AttributesHelper
{
    public static MemberInfo GetExpressionMember<T>(Expression<Func<T>> accessor)
    {
        var accessorBody = accessor.Body;

        // Unwrap casts to object
        if (accessorBody is UnaryExpression unaryExpression
            && unaryExpression.NodeType == ExpressionType.Convert
            && unaryExpression.Type == typeof(object))
        {
            accessorBody = unaryExpression.Operand;
        }

        if (!(accessorBody is MemberExpression memberExpression))
        {
            throw new ArgumentException(
                $"The provided expression contains a {accessorBody.GetType().Name} which is not supported. {nameof(FieldIdentifier)} only supports simple member accessors (fields, properties) of an object.");
        }

        return memberExpression.Member;
    }

    public static List<Attribute> GetExpressionCustomAttributes<T>(Expression<Func<T>> accessor) =>
        GetExpressionMember(accessor).GetCustomAttributes().ToList();

    // Basic Attributes
    public static string? Description(this List<Attribute>? attrs) =>
        attrs?.OfType<DescriptionAttribute>().FirstOrDefault()?.Description;

    public static string? Tooltip(this List<Attribute>? attrs) =>
        attrs?.OfType<ToolTipAttribute>().FirstOrDefault()?.Value;

    /// <summary>
    /// The model-declared placeholder/hint text for a field: <see cref="PlaceholderAttribute"/> first,
    /// then DataAnnotations' own <c>[Display(Prompt = "…")]</c> — the framework's existing "watermark"
    /// slot, honored here for the same reason <see cref="GetLabelText"/> honors <c>[Display(Name)]</c>
    /// (a model already annotated for MVC/Razor Pages needs no second attribute). <c>GetPrompt()</c>
    /// rather than <c>.Prompt</c> so a localized <c>[Display(Prompt=…, ResourceType=…)]</c> resolves
    /// through its resource manager instead of surfacing the raw resource key. Null when neither is
    /// present, so every caller can fall through to its own default.
    /// </summary>
    public static string? Placeholder(this List<Attribute>? attrs) =>
        attrs?.OfType<PlaceholderAttribute>().FirstOrDefault()?.Value
        ?? attrs?.OfType<DisplayAttribute>().FirstOrDefault()?.GetPrompt();

    // Min/Max value bounds
    /// <summary>
    /// The model-declared minimum bound for a numeric field: <see cref="MinValueAttribute"/> first, then
    /// DataAnnotations' own <see cref="RangeAttribute"/> minimum -- the same "model already annotated for
    /// MVC/Razor Pages needs no second attribute" rationale as <see cref="Placeholder"/>. Returns
    /// <c>decimal</c> (not the attribute's own storage type) because that's what every numeric Edit
    /// control already renders bounds through; null means "no bound", so the caller omits the rendered
    /// <c>min</c> attribute instead of rendering a misleading 0.
    ///
    /// KEY RULE: a double bound that cannot be represented as <c>decimal</c> -- out of range, NaN,
    /// Infinity -- is treated as UNBOUNDED (null) rather than throwing <see cref="OverflowException"/> or
    /// rendering an unusable <c>max="1.79E+308"</c>. This is what makes the ubiquitous one-sided idiom
    /// <c>[Range(0, double.MaxValue)]</c> render <c>min="0"</c> with no <c>max</c> attribute at all,
    /// instead of blowing up or emitting scientific notation the browser can't use as a bound. The
    /// integer-typed spellings of that same idiom (<c>[Range(int.MinValue, 100)]</c>, and long/decimal's
    /// extremes via the string ctor) are equally unbounded on the [Range] fallback path -- see
    /// <see cref="IsRangeSentinel"/>, which keeps rendering consistent with ValidationHelper's
    /// one-sided message rewrite of those very same sentinels.
    /// </summary>
    public static decimal? MinNumber(this List<Attribute>? attrs) => NumberBound(attrs, isMin: true);

    /// <summary>See <see cref="MinNumber"/> -- identical rules, the other bound.</summary>
    public static decimal? MaxNumber(this List<Attribute>? attrs) => NumberBound(attrs, isMin: false);

    private static decimal? NumberBound(List<Attribute>? attrs, bool isMin)
    {
        if (attrs is null)
            return null;

        var boundValue = isMin
            ? attrs.OfType<MinValueAttribute>().FirstOrDefault()?.Value
            : attrs.OfType<MaxValueAttribute>().FirstOrDefault()?.Value;

        if (boundValue is not null)
        {
            // [MinValue]/[MaxValue] wins outright -- lenient (null) if it can't convert (the
            // OverflowException rule above), but does NOT fall back to [Range] just because it failed.
            return MinMaxValueComparer.TryConvertBoundToDecimal(boundValue, out var decimalBound) ? decimalBound : null;
        }

        var range = attrs.OfType<RangeAttribute>().FirstOrDefault();
        if (range is null)
            return null;

        // [Range]'s own validation parses a string Minimum/Maximum with this same flag -- honoring it
        // here means the rendered bound and RangeAttribute's enforced bound can never disagree.
        var culture = range.ParseLimitsInInvariantCulture ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;
        var operand = isMin ? range.Minimum : range.Maximum;
        return MinMaxValueComparer.TryConvertBoundToDecimal(operand, culture, out var rangeBound)
            && !IsRangeSentinel(rangeBound)
            ? rangeBound
            : null;
    }

    // RangeAttribute requires BOTH bounds, so "no minimum" is conventionally spelled int.MinValue
    // (or long/decimal's) -- the same one-sided idiom ValidationHelper.IsTypeMin/MaxSentinel already
    // rewrites into one-sided messages ("Cannot exceed 100"). Rendering must agree with that rewrite:
    // a bound the message layer presents as absent can't show up in the DOM as min="-2147483648".
    // double/float extremes never reach here (unrepresentable as decimal -> already null), and the
    // unsigned/byte/ushort minimums (0) are deliberately NOT sentinels even though ValidationHelper
    // treats "0" as one -- min="0" is a real browser-side floor that [Range(0, 100)] must keep.
    // Applies only to the [Range] fallback: [MinValue]/[MaxValue] are one-sided by design, so a bound
    // written there is always intentional and renders verbatim.
    static bool IsRangeSentinel(decimal bound) =>
        bound == int.MinValue || bound == int.MaxValue
        || bound == long.MinValue || bound == long.MaxValue
        || bound == decimal.MinValue || bound == decimal.MaxValue;

    /// <summary>
    /// The model-declared minimum bound for a date/time field: <see cref="MinValueAttribute"/>'s string
    /// ctor first (parsed as invariant-culture date/time text -- its int/double ctors are meaningless for
    /// a date and resolve to null), then a DataAnnotations <c>[Range(typeof(DateTime), "…", "…")]</c> (or
    /// <c>typeof(DateOnly)</c>) bound, honoring <see cref="RangeAttribute.ParseLimitsInInvariantCulture"/>
    /// the same way <see cref="RangeAttribute"/>'s own validation does. Null when neither attribute is
    /// present or the bound text doesn't parse.
    /// </summary>
    public static DateTime? MinDate(this List<Attribute>? attrs) => DateBound(attrs, isMin: true);

    /// <summary>See <see cref="MinDate"/> -- identical rules, the other bound.</summary>
    public static DateTime? MaxDate(this List<Attribute>? attrs) => DateBound(attrs, isMin: false);

    private static DateTime? DateBound(List<Attribute>? attrs, bool isMin)
    {
        if (attrs is null)
            return null;

        var boundValue = isMin
            ? attrs.OfType<MinValueAttribute>().FirstOrDefault()?.Value
            : attrs.OfType<MaxValueAttribute>().FirstOrDefault()?.Value;

        if (boundValue is not null)
        {
            // int/double ctor values are meaningless for a date bound -- only the string ctor applies.
            return boundValue is string text
                && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : null;
        }

        var range = attrs.OfType<RangeAttribute>()
            .FirstOrDefault(r => r.OperandType == typeof(DateTime) || r.OperandType == typeof(DateOnly));
        if (range is null)
            return null;

        var culture = range.ParseLimitsInInvariantCulture ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;
        var operand = isMin ? range.Minimum : range.Maximum;
        return operand is string rangeText && DateTime.TryParse(rangeText, culture, DateTimeStyles.None, out var rangeParsed)
            ? rangeParsed
            : null;
    }

    public static string GetId(string? id, FormGroupOptions? formGroupOptions, string? idPrefix,
        FieldIdentifier fieldIdentifier)
    {
        // Explicit id always wins.
        if (!string.IsNullOrEmpty(id))
            return id;

        var fieldName = fieldIdentifier.FieldName;
        if (!string.IsNullOrEmpty(formGroupOptions?.Name))
            fieldName = formGroupOptions.Name + "-" + fieldName;

        if (idPrefix != null)
            fieldName = idPrefix + "-" + fieldName;

        return fieldName.Replace(" ", "");
    }

    // Complex
    public static (int? MinLength, int? MaxLength) GetMinAndMaxLengths(List<Attribute> attributes)
    {
        // null means "no length constraint" rather than a misleading 0; when both a StringLength and
        // a separate Min/MaxLength apply, take the tighter bound.
        int? min = null;
        int? max = null;
        var stringLengthAttribute = attributes.OfType<StringLengthAttribute>().FirstOrDefault();
        if (stringLengthAttribute != null)
        {
            min = stringLengthAttribute.MinimumLength;
            max = stringLengthAttribute.MaximumLength;
        }

        var minLengthAttribute = attributes.OfType<MinLengthAttribute>().FirstOrDefault();
        if (minLengthAttribute != null)
        {
            min = Math.Max(min ?? 0, minLengthAttribute.Length);
        }

        var maxLengthAttribute = attributes.OfType<MaxLengthAttribute>().FirstOrDefault();
        if (maxLengthAttribute != null)
        {
            // Upper bound: the tighter constraint is the SMALLER of the two (both validators run,
            // so the effective max is whichever rejects first). Math.Max here would report the
            // looser bound and break the MaxLength message rewrite.
            max = max is null ? maxLengthAttribute.Length : Math.Min(max.Value, maxLengthAttribute.Length);
        }

        return (min, max);
    }

    public static string GetLabelText(this List<Attribute>? attrs, FieldIdentifier fieldIdentifier)
    {
        // Order: DisplayNameAttribute, EnumDisplayNameAttribute, DisplayAttribute, PropertyName
        var displayNameAttribute = attrs?.OfType<DisplayNameAttribute>().FirstOrDefault();
        var labelText = displayNameAttribute?.DisplayName;

        if (displayNameAttribute == null)
        {
            var enumDisplayName = attrs?.OfType<EnumDisplayNameAttribute>().FirstOrDefault();
            if (enumDisplayName != null)
            {
                labelText = enumDisplayName.Value;
            }
        }

        if (string.IsNullOrEmpty(labelText))
        {
            // [Display(Name = …)] — DataAnnotations' own naming attribute. Honoring it keeps the
            // label consistent with the validation messages DataAnnotations generates (which use
            // [Display]), and with EnumHelpers.GetName, which already honors it for enum members.
            // GetName() (not .Name) so a localized [Display(Name=…, ResourceType=…)] resolves through
            // its resource manager instead of surfacing the raw resource key.
            var displayAttribute = attrs?.OfType<DisplayAttribute>().FirstOrDefault();
            var displayName = displayAttribute?.GetName();
            if (!string.IsNullOrEmpty(displayName))
            {
                labelText = displayName;
            }
        }

        if (string.IsNullOrEmpty(labelText))
        {
            labelText = fieldIdentifier.FieldName;
            // split by camel case
            labelText = string.Concat(labelText.Select(x => char.IsUpper(x) ? " " + x : x.ToString())).TrimStart(' ');
        }

        return labelText;
    }
}

// Custom Attributes
public class ToolTipAttribute(string value) : Attribute
{
    public string Value { get; protected set; } = value;
}

public class EnumDisplayNameAttribute(string value) : Attribute
{
    public string Value { get; protected set; } = value;
}

/// <summary>
/// Declares a control's placeholder/hint text on the model property, so the hint lives next to the
/// field it describes instead of being repeated at every markup site (the same rationale as
/// <see cref="ToolTipAttribute"/> and <c>[Description]</c>). Every control that renders a placeholder
/// resolves it as: its own <c>Placeholder</c> parameter → this attribute → <c>[Display(Prompt)]</c> →
/// the control's built-in default. Controls without a placeholder concept (native date inputs, radios,
/// checkbox lists, file upload) ignore it.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class PlaceholderAttribute(string value) : Attribute
{
    public string Value { get; protected set; } = value;
}

/// <summary>
/// Declares a model-level minimum bound for a field. Unlike <see cref="PlaceholderAttribute"/> this IS a
/// <see cref="ValidationAttribute"/>: resolved by <see cref="AttributesHelper.MinNumber"/>/
/// <see cref="AttributesHelper.MinDate"/> to drive the rendered bound (the same "model owns the metadata,
/// controls just resolve it" rationale as <see cref="PlaceholderAttribute"/>) and enforced at validation
/// time too, so the rendered bound and the enforced bound can never drift apart. The three constructors
/// cover both worlds with one attribute -- an int/double for a numeric field, or a string for either an
/// invariant-culture number or date/time text; which applies is decided by the CLR type of the value
/// actually being validated, not by which ctor was used to declare the attribute. See
/// <see cref="MinMaxValueComparer"/> for the comparison rules, which all favor silently doing nothing
/// (valid) over throwing when the bound doesn't make sense for the field's type -- a misconfigured
/// [MinValue] should degrade gracefully, not take down the form.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class MinValueAttribute : ValidationAttribute
{
    public object Value { get; }

    public MinValueAttribute(int value) : this((object)value) { }
    public MinValueAttribute(double value) : this((object)value) { }
    public MinValueAttribute(string value) : this((object)value) { }

    private MinValueAttribute(object value)
    {
        Value = value;
        ErrorMessage = "The {0} field must be at least {1}.";
    }

    public override bool IsValid(object? value) => MinMaxValueComparer.IsValid(Value, value, isMin: true);

    public override string FormatErrorMessage(string name) =>
        string.Format(CultureInfo.CurrentCulture, ErrorMessageString, name, Value);
}

/// <summary>See <see cref="MinValueAttribute"/> -- identical shape, the other bound.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class MaxValueAttribute : ValidationAttribute
{
    public object Value { get; }

    public MaxValueAttribute(int value) : this((object)value) { }
    public MaxValueAttribute(double value) : this((object)value) { }
    public MaxValueAttribute(string value) : this((object)value) { }

    private MaxValueAttribute(object value)
    {
        Value = value;
        ErrorMessage = "The {0} field must be no more than {1}.";
    }

    public override bool IsValid(object? value) => MinMaxValueComparer.IsValid(Value, value, isMin: false);

    public override string FormatErrorMessage(string name) =>
        string.Format(CultureInfo.CurrentCulture, ErrorMessageString, name, Value);
}

/// <summary>
/// Shared comparison plumbing for <see cref="MinValueAttribute"/>/<see cref="MaxValueAttribute"/>'s
/// <c>IsValid</c> and for <see cref="AttributesHelper"/>'s Min/Max resolution extensions -- both need the
/// same "can this bound be converted/parsed for this value's type, and if not, degrade gracefully" logic,
/// so it lives here once instead of twice.
/// </summary>
internal static class MinMaxValueComparer
{
    /// <summary>
    /// True when <paramref name="value"/> satisfies the Min (<paramref name="isMin"/> true) or Max
    /// (false) <paramref name="bound"/>. A null value is always valid -- that's <c>[Required]</c>'s job,
    /// not this one's, same convention as <see cref="RangeAttribute"/>. NaN never satisfies a Min or Max
    /// comparison. A bound this code cannot convert/parse for the value's CLR type -- or a value of a
    /// type this code doesn't understand at all -- is treated as "no constraint" (valid), not an error:
    /// a misconfigured attribute should silently do nothing, not throw mid-validation.
    /// </summary>
    public static bool IsValid(object bound, object? value, bool isMin)
    {
        if (value is null)
            return true;

        if (value is float or double)
            return IsValidFloatingPoint(bound, value, isMin);

        if (value is sbyte or byte or short or ushort or int or uint or long or ulong or decimal)
            return IsValidIntegral(bound, value, isMin);

        if (value is DateTime dateTime)
            return IsValidDate(bound, dateTime, isMin);

        if (value is DateTimeOffset dateTimeOffset)
            // Compare the wall-clock face value -- matches how EditDate bridges DateTimeOffset <-> DateTime.
            return IsValidDate(bound, dateTimeOffset.DateTime, isMin);

        if (value is DateOnly dateOnly)
        {
            if (!TryParseDateBound(bound, out var boundDate))
                return true;
            var boundDateOnly = DateOnly.FromDateTime(boundDate);
            return isMin ? dateOnly >= boundDateOnly : dateOnly <= boundDateOnly;
        }

        if (value is TimeOnly timeOnly)
        {
            if (bound is not string boundText || !TimeOnly.TryParse(boundText, CultureInfo.InvariantCulture, out var boundTime))
                return true;
            return isMin ? timeOnly >= boundTime : timeOnly <= boundTime;
        }

        // A value type this attribute doesn't understand (e.g. a custom struct) -- not our job to validate.
        return true;
    }

    private static bool IsValidDate(object bound, DateTime value, bool isMin)
    {
        if (!TryParseDateBound(bound, out var boundDate))
            return true;
        return isMin ? value >= boundDate : value <= boundDate;
    }

    private static bool TryParseDateBound(object bound, out DateTime result)
    {
        // int/double ctor values are meaningless for a date/time bound -- only the string ctor applies.
        if (bound is string s)
            return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
        result = default;
        return false;
    }

    private static bool IsValidFloatingPoint(object bound, object value, bool isMin)
    {
        var doubleValue = value is float f ? f : (double)value;
        if (double.IsNaN(doubleValue))
            return false; // NaN never satisfies a Min or a Max comparison.

        if (!TryConvertBoundToDouble(bound, out var boundDouble) || double.IsNaN(boundDouble))
            return true; // unparseable/misconfigured bound -- lenient.

        return isMin ? doubleValue >= boundDouble : doubleValue <= boundDouble;
    }

    private static bool IsValidIntegral(object bound, object value, bool isMin)
    {
        // Prefer exact decimal comparison; fall back to double only when the configured bound can't be
        // represented as a decimal (a double bound of e.g. double.MaxValue/NaN/Infinity -- see
        // AttributesHelper.MinNumber/MaxNumber for why that specific case must degrade, not throw).
        if (TryConvertBoundToDecimal(bound, out var boundDecimal))
        {
            var decimalValue = ToDecimal(value);
            return isMin ? decimalValue >= boundDecimal : decimalValue <= boundDecimal;
        }

        if (TryConvertBoundToDouble(bound, out var boundDouble) && !double.IsNaN(boundDouble))
        {
            var doubleValue = ToDouble(value);
            return isMin ? doubleValue >= boundDouble : doubleValue <= boundDouble;
        }

        return true; // bound could not be converted at all -- lenient.
    }

    private static decimal ToDecimal(object value) => value switch
    {
        sbyte v => v,
        byte v => v,
        short v => v,
        ushort v => v,
        int v => v,
        uint v => v,
        long v => v,
        ulong v => v,
        decimal v => v,
        _ => throw new ArgumentOutOfRangeException(nameof(value)) // caller already narrowed to these types
    };

    private static double ToDouble(object value) => value switch
    {
        sbyte v => v,
        byte v => v,
        short v => v,
        ushort v => v,
        int v => v,
        uint v => v,
        long v => v,
        ulong v => v,
        decimal v => (double)v,
        _ => throw new ArgumentOutOfRangeException(nameof(value)) // caller already narrowed to these types
    };

    /// <summary>
    /// Converts a <see cref="MinValueAttribute"/>/<see cref="MaxValueAttribute"/>/<see cref="RangeAttribute"/>
    /// bound (an int, a double, or a string) to <c>decimal</c>, parsing a string bound with invariant
    /// culture. False -- not an exception -- when the bound is a double outside decimal's range (or
    /// NaN/Infinity) or a string that doesn't parse: see the callers for why that must be lenient rather
    /// than fatal.
    /// </summary>
    internal static bool TryConvertBoundToDecimal(object bound, out decimal result) =>
        TryConvertBoundToDecimal(bound, CultureInfo.InvariantCulture, out result);

    /// <summary>
    /// Culture-aware overload for the <see cref="RangeAttribute"/> fallback, which must honor
    /// <see cref="RangeAttribute.ParseLimitsInInvariantCulture"/> so the rendered bound and
    /// <see cref="RangeAttribute"/>'s own validation bound can never disagree.
    /// </summary>
    internal static bool TryConvertBoundToDecimal(object bound, CultureInfo culture, out decimal result)
    {
        switch (bound)
        {
            case int i:
                result = i;
                return true;
            case double d:
                if (double.IsNaN(d) || double.IsInfinity(d))
                {
                    result = default;
                    return false;
                }
                try
                {
                    result = (decimal)d;
                    return true;
                }
                catch (OverflowException)
                {
                    result = default;
                    return false;
                }
            case string s:
                return decimal.TryParse(s, culture, out result);
            default:
                result = default;
                return false;
        }
    }

    internal static bool TryConvertBoundToDouble(object bound, out double result)
    {
        switch (bound)
        {
            case int i:
                result = i;
                return true;
            case double d:
                result = d;
                return true;
            case string s:
                return double.TryParse(s, CultureInfo.InvariantCulture, out result);
            default:
                result = default;
                return false;
        }
    }
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class MustBeTrueAttribute : ValidationAttribute
{
    public MustBeTrueAttribute()
    {
        ErrorMessage = "Must be checked";
    }

    public override bool IsValid(object? value)
    {
        return value is true;
    }
}