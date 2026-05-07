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
    public void Init_isRequired_is_false_string_when_no_Required_attribute()
    {
        var (_, isRequired, _, _) = EditControlInit.Init(
            () => _model.IsActive, null, null, null);
        Assert.Equal("false", isRequired);
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
}
