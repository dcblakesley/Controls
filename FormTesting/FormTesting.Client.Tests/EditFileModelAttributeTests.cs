using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Coverage for EditFile's model-attribute fallback: <see cref="FileConstraintsAttribute"/> on the
/// bound property supplies AllowedExtensions/MaxFileSizeBytes/MaxFiles/MaxTotalBytes when the matching
/// parameter is left unset (EditFile.razor.cs's EffectiveAllowedExtensions/EffectiveMaxFileSizeBytes/
/// EffectiveMaxFiles/EffectiveMaxTotalBytes) -- the same "parameter wins, else the model attribute, else
/// the built-in default" pattern as EditString's Placeholder / EditNumber's Min/Max. The base
/// upload/validation behavior itself (extension matching, caps, buffering) is covered exhaustively in
/// <see cref="EditFileTests"/>; this file only proves the resolution chain that feeds those checks.
/// </summary>
public class EditFileModelAttributeTests : BunitContext
{
    // Deliberately small numeric caps (bytes, not MB) so the size/total-cap tests stay fast --
    // the resolution logic being tested doesn't care about the magnitude of the configured values.
    class ConstrainedFileModel
    {
        [FileConstraints(AllowedExtensions = new[] { ".pdf", ".png" }, MaxFileSizeBytes = 2048, MaxFiles = 2, MaxTotalBytes = 3072)]
        public List<IBrowserFile> Files { get; set; } = [];
    }

    class UnconstrainedFileModel
    {
        public List<IBrowserFile> Files { get; set; } = [];
    }

    // Regression coverage (audit finding 70 / cc691f4): a negative [FileConstraints] value is a
    // plausible consumer mistake -- "-1 means unlimited" is a widespread convention -- and must fall
    // back to the built-in default exactly like 0/unset does, not be taken literally as a real bound.
    class NegativeFileConstraintsModel
    {
        [FileConstraints(MaxFileSizeBytes = -1, MaxFiles = -1, MaxTotalBytes = -1)]
        public List<IBrowserFile> Files { get; set; } = [];
    }

    [Fact]
    public void AllowedExtensions_attribute_drives_the_rendered_accept_attribute_when_the_parameter_is_unset()
    {
        var model = new ConstrainedFileModel();
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal(".pdf,.png", cut.Find("input[type=file]").GetAttribute("accept"));
        Assert.Contains("Supported formats: .pdf, .png", cut.Find(".edit-file-formats").TextContent);
    }

    [Fact]
    public void AllowedExtensions_attribute_rejects_a_disallowed_extension()
    {
        var model = new ConstrainedFileModel();
        List<IBrowserFile>? changed = null;
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<List<IBrowserFile>>(this, v => changed = v));
            b.CloseComponent();
        }));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.txt"));

        Assert.Null(changed); // .txt isn't in the attribute's AllowedExtensions -- nothing accepted
        Assert.Contains("a.txt", cut.Find(".edit-validation-message").TextContent);
    }

    [Fact]
    public void MaxFiles_attribute_rejects_an_over_count_selection()
    {
        var model = new ConstrainedFileModel(); // [FileConstraints(MaxFiles = 2)]
        List<IBrowserFile>? changed = null;
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<List<IBrowserFile>>(this, v => changed = v));
            b.CloseComponent();
        }));

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "a.pdf"),
            InputFileContent.CreateFromText("2", "b.pdf"),
            InputFileContent.CreateFromText("3", "c.pdf"));

        Assert.NotNull(changed);
        Assert.Equal(2, changed.Count);
        Assert.Contains("Only 2 files allowed — 1 not added.", cut.Find(".edit-validation-message").TextContent);
    }

    [Fact]
    public void MaxFileSizeBytes_attribute_rejects_a_file_over_the_declared_cap()
    {
        var model = new ConstrainedFileModel(); // [FileConstraints(MaxFileSizeBytes = 2048)]
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText(new string('x', 3000), "big.pdf"));

        Assert.Contains("2 KB", cut.Find(".edit-validation-message").TextContent);
    }

    [Fact]
    public void MaxTotalBytes_attribute_caps_the_aggregate_selection()
    {
        var model = new ConstrainedFileModel(); // [FileConstraints(MaxTotalBytes = 3072)], per-file cap 2048
        List<IBrowserFile>? changed = null;
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<List<IBrowserFile>>(this, v => changed = v));
            b.CloseComponent();
        }));

        // Each file (2000 B) is under the 2048 B per-file cap; together (4000 B) they exceed the 3072 B total.
        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText(new string('x', 2000), "a.pdf"),
            InputFileContent.CreateFromText(new string('x', 2000), "b.pdf"));

        Assert.NotNull(changed);
        Assert.Single(changed);
        Assert.Contains("Total size limit", cut.Find(".edit-validation-message").TextContent);
    }

    [Fact]
    public void AllowedExtensions_parameter_overrides_the_model_attribute()
    {
        var model = new ConstrainedFileModel(); // attribute restricts to .pdf/.png
        List<IBrowserFile>? changed = null;
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<List<IBrowserFile>>(this, v => changed = v));
            b.AddAttribute(4, "AllowedExtensions", new[] { ".txt" }); // parameter wins outright over the attribute
            b.CloseComponent();
        }));

        Assert.Equal(".txt", cut.Find("input[type=file]").GetAttribute("accept"));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.txt"));

        Assert.NotNull(changed);
        Assert.Single(changed);
        Assert.Empty(cut.FindAll("div.edit-validation-message[role='alert']"));
    }

    [Fact]
    public void MaxFiles_parameter_overrides_the_model_attribute()
    {
        var model = new ConstrainedFileModel(); // attribute caps at 2
        List<IBrowserFile>? changed = null;
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<List<IBrowserFile>>(this, v => changed = v));
            b.AddAttribute(4, "MaxFiles", 5); // parameter wins outright over the attribute's 2
            b.CloseComponent();
        }));

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "a.pdf"),
            InputFileContent.CreateFromText("2", "b.pdf"),
            InputFileContent.CreateFromText("3", "c.pdf"));

        Assert.NotNull(changed);
        Assert.Equal(3, changed.Count); // all three accepted -- the attribute's cap of 2 did not apply
        Assert.Empty(cut.FindAll("div.edit-validation-message[role='alert']"));
    }

    [Fact]
    public void Rendered_DOM_is_unchanged_when_neither_AllowedExtensions_parameter_nor_attribute_is_set()
    {
        var model = new UnconstrainedFileModel();
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        // No accept restriction and no "Supported formats" hint -- same DOM as before this feature existed.
        Assert.True(string.IsNullOrEmpty(cut.Find("input[type=file]").GetAttribute("accept")));
        Assert.Empty(cut.FindAll(".edit-file-formats"));
    }

    [Fact]
    public void Effective_values_default_to_the_original_constants_when_neither_parameter_nor_attribute_is_set()
    {
        // Reflection-based, like the reentrancy tests in EditFileTests -- the private Effective* getters
        // are the exact resolution the DOM/behavioral tests exercise indirectly; this pins their numeric
        // value directly so a byte-identical default is provable without uploading multi-megabyte files.
        var model = new UnconstrainedFileModel();
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var editFile = cut.FindComponent<EditFile>().Instance;
        var type = typeof(EditFile);
        object? EffectiveOf(string name) =>
            type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(editFile);

        Assert.Equal(10L * 1024 * 1024, (long)EffectiveOf("EffectiveMaxFileSizeBytes")!);
        Assert.Equal(0, (int)EffectiveOf("EffectiveMaxFiles")!);
        Assert.Equal(100L * 1024 * 1024, (long)EffectiveOf("EffectiveMaxTotalBytes")!);
        Assert.Empty((string[])EffectiveOf("EffectiveAllowedExtensions")!);
    }

    [Fact]
    public void Effective_values_fall_back_to_defaults_when_the_attribute_bound_is_negative()
    {
        // A negative [FileConstraints] value (a plausible "-1 means unlimited" typo) must resolve
        // exactly like 0/unset -- the built-in default -- not be taken literally as a real bound.
        var model = new NegativeFileConstraintsModel();
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var editFile = cut.FindComponent<EditFile>().Instance;
        var type = typeof(EditFile);
        object? EffectiveOf(string name) =>
            type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(editFile);

        Assert.Equal(10L * 1024 * 1024, (long)EffectiveOf("EffectiveMaxFileSizeBytes")!);
        Assert.Equal(0, (int)EffectiveOf("EffectiveMaxFiles")!);
        Assert.Equal(100L * 1024 * 1024, (long)EffectiveOf("EffectiveMaxTotalBytes")!);
    }

    [Fact]
    public void A_negative_MaxFileSizeBytes_attribute_does_not_reject_every_upload()
    {
        // Before the fix, EffectiveMaxFileSizeBytes resolved to -1, and `file.Size > -1` is true for
        // any file (size >= 0), so every upload was silently rejected as "too large".
        var model = new NegativeFileConstraintsModel();
        List<IBrowserFile>? changed = null;
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<List<IBrowserFile>>(this, v => changed = v));
            b.CloseComponent();
        }));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("small file", "a.pdf"));

        Assert.NotNull(changed);
        Assert.Single(changed);
        Assert.Empty(cut.FindAll("div.edit-validation-message[role='alert']"));
    }
}
