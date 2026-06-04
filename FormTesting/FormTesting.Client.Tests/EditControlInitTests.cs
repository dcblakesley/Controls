namespace FormTesting.Client.Tests;

public class EditControlInitTests
{
    readonly PersonModel _model = new();

    [Fact]
    public void Init_returns_id_isRequired_attributes_and_field_identifier()
    {
        var (id, isRequired, attributes, fid) = EditControlInit.Init(
            () => _model.Name, id: null, formGroupOptions: null, idPrefix: null);

        Assert.Equal("Name", id);
        Assert.Equal("true", isRequired); // [Required] is on Name
        Assert.NotNull(attributes);
        Assert.Equal(nameof(PersonModel.Name), fid.FieldName);
        Assert.Same(_model, fid.Model);
    }

    [Fact]
    public void Init_isRequired_is_null_when_no_Required_attribute()
    {
        // Null (not "false") so the binding omits aria-required for optional fields.
        var (_, isRequired, _, _) = EditControlInit.Init(
            () => _model.IsActive, null, null, null);
        Assert.Null(isRequired);
    }

    [Fact]
    public void Init_uses_explicit_id_when_provided()
    {
        var (id, _, _, _) = EditControlInit.Init(
            () => _model.Name, id: "my-id", formGroupOptions: null, idPrefix: null);
        Assert.Equal("my-id", id);
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
