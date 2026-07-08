using System.Linq.Expressions;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

public class EditControlInitTests
{
    readonly PersonModel _model = new();

    [Fact]
    public void Init_returns_id_attributes_and_field_identifier()
    {
        var (id, attributes, fid) = EditControlInit.Init(
            () => _model.Name, id: null, formGroupOptions: null, idPrefix: null);

        Assert.Equal("Name", id);
        Assert.NotNull(attributes);
        Assert.Equal(nameof(PersonModel.Name), fid.FieldName);
        Assert.Same(_model, fid.Model);
    }

    [Fact]
    public void Init_uses_explicit_id_when_provided()
    {
        var (id, _, _) = EditControlInit.Init(
            () => _model.Name, id: "my-id", formGroupOptions: null, idPrefix: null);
        Assert.Equal("my-id", id);
    }

    // --- Required-ness resolution (IsRequired param → [Required] attribute → RequiredResolver) ---

    (List<Attribute> Attributes, FieldIdentifier Fid) InitFor<T>(Expression<Func<T>> field)
    {
        var (_, attributes, fid) = EditControlInit.Init(field, null, null, null);
        return (attributes, fid);
    }

    [Fact]
    public void IsRequired_true_when_Required_attribute_present()
    {
        var (attrs, fid) = InitFor(() => _model.Name); // [Required] is on Name
        Assert.True(EditControlInit.IsRequired(attrs, null, null, fid));
        Assert.Equal("true", EditControlInit.AriaRequired(attrs, null, null, fid));
    }

    [Fact]
    public void AriaRequired_is_null_when_no_Required_attribute()
    {
        // Null (not "false") so the binding omits aria-required for optional fields.
        var (attrs, fid) = InitFor(() => _model.IsActive);
        Assert.Null(EditControlInit.AriaRequired(attrs, null, null, fid));
    }

    [Fact]
    public void IsRequired_param_true_forces_required_without_the_attribute()
    {
        var (attrs, fid) = InitFor(() => _model.IsActive); // no [Required]
        Assert.True(EditControlInit.IsRequired(attrs, true, null, fid));
    }

    [Fact]
    public void IsRequired_param_false_forces_optional_even_with_the_attribute()
    {
        // The force-off half of the three-state escape hatch: a RequiredAttribute-derived
        // conditional (RequiredIf) whose condition is off would otherwise show a permanent star.
        var (attrs, fid) = InitFor(() => _model.Name); // [Required] present
        Assert.False(EditControlInit.IsRequired(attrs, false, null, fid));
        Assert.Null(EditControlInit.AriaRequired(attrs, false, null, fid));
    }

    [Fact]
    public void RequiredResolver_marks_a_field_required_without_the_attribute()
    {
        // The FluentValidation bridge point: no [Required] on the model, the form-level
        // resolver supplies required-ness instead.
        var (attrs, fid) = InitFor(() => _model.IsActive);
        var form = new FormOptions { RequiredResolver = f => f.FieldName == nameof(PersonModel.IsActive) };
        Assert.True(EditControlInit.IsRequired(attrs, null, form, fid));
        Assert.Equal("true", EditControlInit.AriaRequired(attrs, null, form, fid));
    }

    [Fact]
    public void IsRequired_param_false_overrides_the_resolver()
    {
        var (attrs, fid) = InitFor(() => _model.IsActive);
        var form = new FormOptions { RequiredResolver = _ => true };
        Assert.False(EditControlInit.IsRequired(attrs, false, form, fid));
    }

    [Fact]
    public void RequiredResolver_is_not_called_for_a_default_FieldIdentifier()
    {
        // FormLabel can render standalone (EditDisplay) with no field — the consumer's resolver
        // lambda must never see a FieldIdentifier with a null Model.
        var form = new FormOptions { RequiredResolver = f => f.Model.GetType() == typeof(PersonModel) };
        Assert.False(EditControlInit.IsRequired(null, null, form, default));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ShowEditor_requires_both_local_and_form_to_agree(bool isEditMode, bool formEditMode, bool expected)
    {
        var formOptions = new FormOptions { IsEditMode = formEditMode };
        Assert.Equal(expected, EditControlInit.ShowEditor(isEditMode, formOptions));
    }

    [Fact]
    public void ShowEditor_treats_null_FormOptions_as_edit_mode()
    {
        Assert.True(EditControlInit.ShowEditor(true, null));
        Assert.False(EditControlInit.ShowEditor(false, null));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void ShouldHideLabel_is_OR_of_local_and_form_settings(bool localHidden, bool formHidden, bool expected)
    {
        var formOptions = new FormOptions { IsLabelHidden = formHidden };
        Assert.Equal(expected, EditControlInit.ShouldHideLabel(localHidden, formOptions));
    }

    [Theory]
    // isHidden short-circuits to false regardless of everything else
    [InlineData(true, HidingMode.None, true, false, false, false)]
    [InlineData(true, HidingMode.WhenNull, true, true, true, false)]
    // None always shows
    [InlineData(false, HidingMode.None, false, true, true, true)]
    // WhenReadOnlyAndNull: hide only when read-only (!showEditor) AND null
    [InlineData(false, HidingMode.WhenReadOnlyAndNull, false, true, false, false)]
    [InlineData(false, HidingMode.WhenReadOnlyAndNull, true, true, false, true)]
    [InlineData(false, HidingMode.WhenReadOnlyAndNull, false, false, false, true)]
    // WhenReadOnlyAndNullOrDefault: hide only when read-only AND default
    [InlineData(false, HidingMode.WhenReadOnlyAndNullOrDefault, false, false, true, false)]
    [InlineData(false, HidingMode.WhenReadOnlyAndNullOrDefault, true, false, true, true)]
    // WhenNull: show iff not null
    [InlineData(false, HidingMode.WhenNull, true, true, false, false)]
    [InlineData(false, HidingMode.WhenNull, true, false, false, true)]
    // WhenNullOrDefault: show iff not default
    [InlineData(false, HidingMode.WhenNullOrDefault, true, false, true, false)]
    [InlineData(false, HidingMode.WhenNullOrDefault, true, false, false, true)]
    public void ShouldShow_truth_table(bool isHidden, HidingMode hiding, bool showEditor, bool isNull, bool isDefault, bool expected)
    {
        Assert.Equal(expected, EditControlInit.ShouldShow(isHidden, hiding, formOptions: null, showEditor, isNull, isDefault));
    }

    [Fact]
    public void ShouldShow_per_control_hiding_overrides_form_wide()
    {
        var form = new FormOptions { Hiding = HidingMode.None };
        // Per-control WhenNull wins over the form-wide None: a null value hides.
        Assert.False(EditControlInit.ShouldShow(false, HidingMode.WhenNull, form, showEditor: true, isNull: true, isDefault: true));
    }

    [Fact]
    public void ShouldShow_falls_back_to_form_wide_hiding_when_per_control_null()
    {
        var form = new FormOptions { Hiding = HidingMode.WhenNull };
        Assert.False(EditControlInit.ShouldShow(false, null, form, showEditor: true, isNull: true, isDefault: true));
        Assert.True(EditControlInit.ShouldShow(false, null, form, showEditor: true, isNull: false, isDefault: false));
    }
}
