using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Round-trip regression coverage for EditNumber's unified <c>FormatNumber</c> helper (finding 72 of
/// the 2026-07-30 audit). <c>FormatValueAsString</c> (edit-mode, always InvariantCulture) and
/// <c>GetFormattedNumber</c> (read-only, CurrentCulture plus an optional custom Format) used to be two
/// hand-synced 11-case switches over the same eleven numeric types; a type added to one and not the
/// other silently degraded to the naked <c>value.ToString()</c> fallback (losing InvariantCulture in
/// edit mode, or a custom Format in read-only mode). Every supported type is exercised through both
/// paths here, under a non-invariant CurrentCulture, against the exact expression the pre-unification
/// code used for that type (<see cref="BindConverter.FormatValue(int, CultureInfo)"/> and its five
/// sibling overloads for the six BindConverter-backed types; the type's own
/// <c>ToString(IFormatProvider)</c> for the other five) -- an independent oracle from the framework
/// itself, not the new implementation, so a regression can't hide by testing itself.
/// </summary>
public class EditNumberFormatNumberRoundTripTests : BunitContext
{
    class NumbersModel
    {
        public int? IntValue { get; set; }
        public long? LongValue { get; set; }
        public short? ShortValue { get; set; }
        public byte? ByteValue { get; set; }
        public sbyte? SByteValue { get; set; }
        public uint? UIntValue { get; set; }
        public ulong? ULongValue { get; set; }
        public ushort? UShortValue { get; set; }
        public float? FloatValue { get; set; }
        public double? DoubleValue { get; set; }
        public decimal? DecimalValue { get; set; }
    }

    string? EditModeValue<T>(NumbersModel model, Expression<Func<T?>> field, T? value) where T : struct
    {
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<T?>>(0);
            b.AddAttribute(1, "Value", value);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));
        return cut.Find("input.edit-number-input").GetAttribute("value");
    }

    string ReadOnlyText<T>(NumbersModel model, Expression<Func<T?>> field, T? value, string? format) where T : struct
    {
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<T?>>(0);
            b.AddAttribute(1, "Value", value);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsEditMode", false);
            if (format is not null) b.AddAttribute(4, "Format", format);
            b.CloseComponent();
        }));
        return cut.Find(".edit-readonly-value").TextContent;
    }

    [Fact]
    public void EditMode_formats_every_supported_type_exactly_like_the_pre_unification_code_did()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // ',' decimal separator -- proves invariant formatting doesn't leak CurrentCulture into
            // the value the native <input type=number> must be able to parse.
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var model = new NumbersModel();

            Assert.Equal(BindConverter.FormatValue(-12, CultureInfo.InvariantCulture),
                EditModeValue<int>(model, () => model.IntValue, -12));
            Assert.Equal(BindConverter.FormatValue(9_000_000_000L, CultureInfo.InvariantCulture),
                EditModeValue<long>(model, () => model.LongValue, 9_000_000_000L));
            Assert.Equal(BindConverter.FormatValue((short)-32000, CultureInfo.InvariantCulture),
                EditModeValue<short>(model, () => model.ShortValue, -32000));
            Assert.Equal(BindConverter.FormatValue(1.5f, CultureInfo.InvariantCulture),
                EditModeValue<float>(model, () => model.FloatValue, 1.5f));
            Assert.Equal(BindConverter.FormatValue(12345.6789, CultureInfo.InvariantCulture),
                EditModeValue<double>(model, () => model.DoubleValue, 12345.6789));
            Assert.Equal(BindConverter.FormatValue(1234.5m, CultureInfo.InvariantCulture),
                EditModeValue<decimal>(model, () => model.DecimalValue, 1234.5m));

            Assert.Equal(((byte)200).ToString(CultureInfo.InvariantCulture),
                EditModeValue<byte>(model, () => model.ByteValue, 200));
            Assert.Equal(((sbyte)-100).ToString(CultureInfo.InvariantCulture),
                EditModeValue<sbyte>(model, () => model.SByteValue, -100));
            Assert.Equal(((ushort)60000).ToString(CultureInfo.InvariantCulture),
                EditModeValue<ushort>(model, () => model.UShortValue, 60000));
            Assert.Equal(4_000_000_000U.ToString(CultureInfo.InvariantCulture),
                EditModeValue<uint>(model, () => model.UIntValue, 4_000_000_000U));
            Assert.Equal(18_000_000_000_000_000_000UL.ToString(CultureInfo.InvariantCulture),
                EditModeValue<ulong>(model, () => model.ULongValue, 18_000_000_000_000_000_000UL));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ReadOnly_formats_every_supported_type_with_CurrentCulture_and_an_optional_Format()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var culture = CultureInfo.CurrentCulture;
            var model = new NumbersModel();

            Assert.Contains((-12).ToString((string?)null, culture), ReadOnlyText<int>(model, () => model.IntValue, -12, null));
            Assert.Contains(9_000_000_000L.ToString((string?)null, culture), ReadOnlyText<long>(model, () => model.LongValue, 9_000_000_000L, null));
            Assert.Contains(((short)-32000).ToString((string?)null, culture), ReadOnlyText<short>(model, () => model.ShortValue, -32000, null));
            Assert.Contains(((byte)200).ToString((string?)null, culture), ReadOnlyText<byte>(model, () => model.ByteValue, 200, null));
            Assert.Contains(((sbyte)-100).ToString((string?)null, culture), ReadOnlyText<sbyte>(model, () => model.SByteValue, -100, null));
            Assert.Contains(4_000_000_000U.ToString((string?)null, culture), ReadOnlyText<uint>(model, () => model.UIntValue, 4_000_000_000U, null));
            Assert.Contains(18_000_000_000_000_000_000UL.ToString((string?)null, culture), ReadOnlyText<ulong>(model, () => model.ULongValue, 18_000_000_000_000_000_000UL, null));
            Assert.Contains(((ushort)60000).ToString((string?)null, culture), ReadOnlyText<ushort>(model, () => model.UShortValue, 60000, null));

            // Format string honored per-type with CurrentCulture separators (comma decimal under de-DE).
            Assert.Contains(1.5f.ToString("N2", culture), ReadOnlyText<float>(model, () => model.FloatValue, 1.5f, "N2"));
            Assert.Contains(12345.6789.ToString("N2", culture), ReadOnlyText<double>(model, () => model.DoubleValue, 12345.6789, "N2"));
            Assert.Contains(1234.5m.ToString("N2", culture), ReadOnlyText<decimal>(model, () => model.DecimalValue, 1234.5m, "N2"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void EditMode_formats_sbyte_invariantly_under_a_culture_with_a_non_ascii_minus_sign()
    {
        // sv-SE uses U+2212 (minus sign) rather than ASCII '-' for CurrentCulture formatting -- the
        // motivating case in FormatValueAsString's own doc comment for why the unsigned/byte types
        // must format invariantly too, or the native number input can't parse the rendered value back.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("sv-SE");
            var model = new NumbersModel();
            Assert.Equal("-5", EditModeValue<sbyte>(model, () => model.SByteValue, -5));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
