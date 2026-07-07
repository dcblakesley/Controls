using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Coverage for <c>EditFile</c>, starting with the null-bound-list regressions: a model whose
/// <c>List&lt;IBrowserFile&gt;</c> property is never initialized must render and accept uploads
/// rather than throwing.
/// </summary>
public class EditFileTests : TestContext
{
    class FileModel
    {
        public List<IBrowserFile> Files { get; set; } = null!; // required only guarantees set, not non-null
    }

    static RenderFragment WithForm(FileModel model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    IRenderedFragment RenderEditFile(
        FileModel model,
        Action<List<IBrowserFile>>? onChanged = null,
        int maxFiles = 0,
        bool isDisabled = false,
        long maxFileSizeBytes = 0,
        string[]? allowedExtensions = null)
    {
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        return Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "Field", field);
            if (onChanged is not null)
                b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<List<IBrowserFile>>(this, onChanged));
            if (maxFiles > 0)
                b.AddAttribute(4, "MaxFiles", maxFiles);
            if (isDisabled)
                b.AddAttribute(5, "IsDisabled", true);
            if (maxFileSizeBytes > 0)
                b.AddAttribute(6, "MaxFileSizeBytes", maxFileSizeBytes);
            if (allowedExtensions is not null)
                b.AddAttribute(7, "AllowedExtensions", allowedExtensions);
            b.CloseComponent();
        }));
    }

    [Fact]
    public void Null_bound_list_renders_the_drop_zone_without_throwing()
    {
        var cut = RenderEditFile(new FileModel()); // Files is null

        Assert.Single(cut.FindAll(".edit-file-drop-zone"));
        Assert.Empty(cut.FindAll(".edit-file-list"));
    }

    [Fact]
    public void Null_bound_list_with_MaxFiles_renders_the_drop_zone_without_throwing()
    {
        var cut = RenderEditFile(new FileModel(), maxFiles: 3); // hits the Value.Count < MaxFiles branch

        Assert.Single(cut.FindAll(".edit-file-drop-zone"));
    }

    [Fact]
    public void Upload_into_a_null_bound_list_creates_the_list()
    {
        var model = new FileModel();
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello", "a.txt"));

        Assert.NotNull(changed);
        Assert.Single(changed);
        Assert.Equal("a.txt", changed[0].Name);
    }

    [Fact]
    public void IsDisabled_disables_the_file_input_and_remove_buttons()
    {
        List<IBrowserFile>? uploaded = null;
        var enabled = RenderEditFile(new FileModel { Files = [] }, v => uploaded = v);
        enabled.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hi", "a.txt"));
        Assert.NotNull(uploaded);

        var disabled = RenderEditFile(new FileModel { Files = uploaded }, isDisabled: true);

        Assert.True(disabled.Find("input[type=file]").HasAttribute("disabled"));
        Assert.Contains("disabled", disabled.Find(".edit-file-drop-zone").ClassList);
        Assert.True(disabled.Find(".edit-file-delete-btn").HasAttribute("disabled"));
    }

    [Fact]
    public void Files_beyond_MaxFiles_are_reported_not_silently_dropped()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, maxFiles: 1);

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "a.txt"),
            InputFileContent.CreateFromText("2", "b.txt"));

        Assert.NotNull(changed);
        Assert.Single(changed);
        Assert.Contains("Only 1 file allowed — 1 not added.", cut.Find(".edit-validation-message").TextContent);
    }

    [Fact]
    public void Sub_megabyte_size_cap_is_reported_in_KB_not_zero_MB()
    {
        var cut = RenderEditFile(new FileModel { Files = [] }, maxFileSizeBytes: 500 * 1024);

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText(new string('x', 600 * 1024), "big.txt"));

        var message = cut.Find(".edit-validation-message").TextContent;
        Assert.Contains("500 KB", message);
        Assert.DoesNotContain("0 MB", message);
    }

    [Fact]
    public void Every_rejected_file_gets_its_own_error_message()
    {
        var cut = RenderEditFile(new FileModel { Files = [] }, allowedExtensions: [".pdf"]);

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "a.txt"),
            InputFileContent.CreateFromText("2", "b.csv"));

        // Two rejections used to overwrite each other, leaving only the last visible.
        var message = cut.Find(".edit-validation-message").TextContent;
        Assert.Contains("a.txt", message);
        Assert.Contains("b.csv", message);
    }

    [Fact]
    public void Toggle_on_a_null_bound_checked_list_creates_the_list()
    {
        // Same base-class fix, exercised through EditCheckedStringList.ToggleAsync.
        var model = new PersonModel { Tags = null! };
        Expression<Func<List<string>>> field = () => model.Tags;
        List<string>? changed = null;
        var cut = Render(builder =>
        {
            builder.OpenComponent<EditForm>(0);
            builder.AddAttribute(1, "Model", model);
            builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => b =>
            {
                b.OpenComponent<EditCheckedStringList>(0);
                b.AddAttribute(1, "Value", model.Tags);
                b.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<List<string>>(this, v => changed = v));
                b.AddAttribute(3, "Field", field);
                b.AddAttribute(4, "Options", new List<string> { "a", "b" });
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.FindAll("input[type=checkbox]")[0].Change(true);

        Assert.NotNull(changed);
        Assert.Equal(["a"], changed);
    }
}
