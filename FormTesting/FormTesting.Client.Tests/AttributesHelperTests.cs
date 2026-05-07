using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

public class AttributesHelperTests
{
    readonly PersonModel _model = new();

    FieldIdentifier FieldOf<T>(System.Linq.Expressions.Expression<Func<T>> field)
        => FieldIdentifier.Create(field);

    [Fact]
    public void GetId_uses_explicit_id_when_provided()
    {
        var fid = FieldOf(() => _model.Name);
        var id = AttributesHelper.GetId("custom-id", null, null, fid);
        Assert.Equal("custom-id", id);
    }

    [Fact]
    public void GetId_falls_back_to_field_name_with_spaces_stripped()
    {
        var fid = FieldOf(() => _model.Name);
        var id = AttributesHelper.GetId(null, null, null, fid);
        Assert.Equal("Name", id);
    }

    [Fact]
    public void GetId_prefixes_with_FormGroupOptions_name()
    {
        var fid = FieldOf(() => _model.Name);
        var id = AttributesHelper.GetId(null, new FormGroupOptions { Name = "billing" }, null, fid);
        Assert.Equal("billing-Name", id);
    }

    [Fact]
    public void GetId_layers_idPrefix_on_top_of_group_name()
    {
        var fid = FieldOf(() => _model.Name);
        var id = AttributesHelper.GetId(null, new FormGroupOptions { Name = "billing" }, "form1", fid);
        Assert.Equal("form1-billing-Name", id);
    }

    [Fact]
    public void GetId_explicit_id_wins_over_prefixes()
    {
        var fid = FieldOf(() => _model.Name);
        var id = AttributesHelper.GetId("explicit", new FormGroupOptions { Name = "billing" }, "form1", fid);
        Assert.Equal("explicit", id);
    }

    [Fact]
    public void GetLabelText_uses_DisplayName_attribute_when_present()
    {
        var fid = FieldOf(() => _model.Name);
        var attrs = AttributesHelper.GetExpressionCustomAttributes(() => _model.Name);
        Assert.Equal("Full Name", attrs.GetLabelText(fid));
    }

    [Fact]
    public void GetLabelText_splits_camelCase_when_no_attribute()
    {
        var fid = FieldOf(() => _model.BirthDate);
        var attrs = AttributesHelper.GetExpressionCustomAttributes(() => _model.BirthDate);
        Assert.Equal("Birth Date", attrs.GetLabelText(fid));
    }

    [Fact]
    public void GetMinAndMaxLengths_reads_StringLength_attribute()
    {
        var attrs = AttributesHelper.GetExpressionCustomAttributes(() => _model.Name);
        var (min, max) = AttributesHelper.GetMinAndMaxLengths(attrs);
        Assert.Equal(2, min);
        Assert.Equal(100, max);
    }

    [Fact]
    public void GetMinAndMaxLengths_reads_separate_MinLength_MaxLength_attributes()
    {
        var attrs = AttributesHelper.GetExpressionCustomAttributes(() => _model.Username);
        var (min, max) = AttributesHelper.GetMinAndMaxLengths(attrs);
        Assert.Equal(2, min);
        Assert.Equal(10, max);
    }

    [Fact]
    public void Description_extension_pulls_DescriptionAttribute()
    {
        var attrs = AttributesHelper.GetExpressionCustomAttributes(() => _model.BirthDate);
        Assert.Equal("The person's birth date", attrs.Description());
    }

    [Fact]
    public void GetExpressionMember_throws_for_non_member_expression()
    {
        Assert.Throws<ArgumentException>(() =>
            AttributesHelper.GetExpressionMember<int>(() => 1 + 1));
    }
}
