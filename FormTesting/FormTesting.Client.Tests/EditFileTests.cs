using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Coverage for <c>EditFile</c>, starting with the null-bound-list regressions: a model whose
/// <c>List&lt;IBrowserFile&gt;</c> property is never initialized must render and accept uploads
/// rather than throwing.
/// </summary>
public class EditFileTests : BunitContext
{
    class FileModel
    {
        public List<IBrowserFile> Files { get; set; } = null!; // required only guarantees set, not non-null
    }

    // A count-based annotation on the bound list -- distinct from FileModel above, which carries none
    // and so never trips EditContext-level validation. Used only by the aria-errormessage tests below.
    class RequiredFilesModel
    {
        [MinLength(1)]
        public List<IBrowserFile> Files { get; set; } = [];
    }

    IRenderedComponent<ContainerFragment> RenderEditFile(
        FileModel model,
        Action<List<IBrowserFile>>? onChanged = null,
        int maxFiles = 0,
        bool isDisabled = false,
        long maxFileSizeBytes = 0,
        string[]? allowedExtensions = null,
        long? maxTotalBytes = null,   // nullable so a test can pass 0 explicitly (0 = unlimited) vs. leave the 100 MB default
        EditFileVariant? variant = null,
        string? buttonText = null,
        Func<IBrowserFile, Task<bool>>? beforeAdd = null,
        string? beforeAddRejectedMessageFormat = null,
        bool bordered = false,
        bool allowDownload = false)
    {
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        return Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
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
            if (maxTotalBytes is not null)
                b.AddAttribute(8, "MaxTotalBytes", maxTotalBytes.Value);
            if (variant is not null)
                b.AddAttribute(9, "Variant", variant.Value);
            if (buttonText is not null)
                b.AddAttribute(10, "ButtonText", buttonText);
            if (beforeAdd is not null)
                b.AddAttribute(11, "BeforeAdd", beforeAdd);
            if (beforeAddRejectedMessageFormat is not null)
                b.AddAttribute(12, "BeforeAddRejectedMessageFormat", beforeAddRejectedMessageFormat);
            if (bordered)
                b.AddAttribute(13, "Bordered", true);
            if (allowDownload)
                b.AddAttribute(14, "AllowDownload", true);
            b.CloseComponent();
        }));
    }

    [Fact]
    public void Consumer_class_is_forwarded_to_the_drop_zone()
    {
        // EditControlListBase captures the unmatched class attribute; EditFile must actually render
        // it (ac15622 only wired up EditMultiSelect — the other list controls silently swallowed it).
        var model = new FileModel { Files = [] };
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "class", "my-upload-class");
            b.CloseComponent();
        }));

        Assert.Contains("my-upload-class", cut.Find(".edit-file-drop-zone").ClassList);
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
    public void Aria_errormessage_is_set_only_while_the_bound_list_fails_EditContext_validation()
    {
        var model = new RequiredFilesModel(); // Files = [] -> [MinLength(1)] fails
        var editContext = new EditContext(model);
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<DataAnnotationsValidator>(0);
                content.CloseComponent();
                content.OpenComponent<EditFile>(1);
                content.AddAttribute(2, "Value", model.Files);
                content.AddAttribute(3, "ValueExpression", field);
                content.CloseComponent();
            }));
            b.CloseComponent();
        });

        // Not yet validated -- no error state.
        var input = cut.Find("input[type=file]");
        Assert.Null(input.GetAttribute("aria-invalid"));
        Assert.False(input.HasAttribute("aria-errormessage"));

        cut.InvokeAsync(() => editContext.Validate());

        input = cut.Find("input[type=file]");
        Assert.Equal("true", input.GetAttribute("aria-invalid"));
        // Same id error-msg-{Id} FieldValidationDisplay renders, and it actually carries the message.
        Assert.Equal("error-msg-Files", input.GetAttribute("aria-errormessage"));
        Assert.NotEmpty(cut.Find("#error-msg-Files").TextContent);
    }

    [Fact]
    public void Aria_errormessage_is_not_set_from_an_upload_rejection_alone()
    {
        // _hasError -- which drives aria-invalid -- also lights up for a pure upload-time rejection
        // (bad extension, duplicate, over a cap) that never touches the EditContext. The
        // error-msg-{id} element FieldValidationDisplay renders only ever contains EditContext-sourced
        // messages, so aria-errormessage stays keyed off IsInvalid, not _hasError -- otherwise it would
        // point at that element while it's empty, even though aria-invalid reports true.
        var model = new FileModel { Files = [] };
        var cut = RenderEditFile(model, allowedExtensions: [".pdf"]);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.txt"));

        var input = cut.Find("input[type=file]");
        Assert.Equal("true", input.GetAttribute("aria-invalid"));  // _hasError: the rejection above
        Assert.False(input.HasAttribute("aria-errormessage"));     // IsInvalid: no EditContext message
        Assert.Contains("a.txt", cut.Find(".edit-validation-message[role='alert']").TextContent);
    }

    [Fact]
    public void Input_and_drop_zone_carry_no_aria_label_so_the_FormLabel_for_wiring_supplies_the_name()
    {
        // Finding 62: aria-label wins by accname precedence over an associated <label for>, so a
        // literal "Choose files"/"File upload area" aria-label meant the field's own label text (and
        // required/description state FormLabel wires up) was never actually announced. The <label for>
        // is the whole reason FormLabel's IsForLabelable wiring exists -- let it supply the name.
        var model = new FileModel { Files = [] };
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Label", "Attachments");
            b.CloseComponent();
        }));

        var input = cut.Find("input[type=file]");
        Assert.False(input.HasAttribute("aria-label"));
        Assert.False(cut.Find(".edit-file-drop-zone").HasAttribute("aria-label"));
        // The label association itself is unaffected by the removal.
        Assert.Equal(input.GetAttribute("id"), cut.Find("label.edit-label").GetAttribute("for"));
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

    // A text file of exactly n bytes ('x' is one byte in UTF-8), for driving the byte-size caps precisely.
    static InputFileContent FileOfBytes(int n, string name) => InputFileContent.CreateFromText(new string('x', n), name);

    [Fact]
    public void Upload_error_message_formats_are_localizable()
    {
        // The default English strings are pinned by the surrounding tests; this pins the override
        // path: consumer-supplied formats replace the built-ins, including a plural-handling format
        // that ignores the pre-pluralized English unit argument.
        var model = new FileModel { Files = [] };
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "MaxFiles", 1);
            b.AddAttribute(4, "AllowedExtensions", new[] { ".txt" });
            b.AddAttribute(5, "MaxFilesMessageFormat", "Maximal {0} Dateien erlaubt — {1} nicht hinzugefügt.");
            b.AddAttribute(6, "UnsupportedFormatMessageFormat", "{0}: Format nicht unterstützt ({1}).");
            b.CloseComponent();
        }));

        // Batch 1: extension rejection (the count cap runs first, so keep this batch under it).
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "b.csv"));
        Assert.Contains("b.csv: Format nicht unterstützt (.txt).",
            cut.Find(".edit-validation-message").TextContent);

        // Batch 2: one accepted, one over the count cap.
        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("2", "a.txt"),
            InputFileContent.CreateFromText("3", "c.txt"));
        Assert.Contains("Maximal 1 Dateien erlaubt — 1 nicht hinzugefügt.",
            cut.Find(".edit-validation-message").TextContent);
    }

    [Fact]
    public void Files_within_the_per_file_cap_but_over_the_total_cap_stop_buffering_at_the_cap()
    {
        // M12: each file passes MaxFileSizeBytes (4 KB) individually, but the aggregate cap (2 KB) admits
        // only the first two 1 KB files; the third would push the running total to 3 KB and is skipped.
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, maxFileSizeBytes: 4096, maxTotalBytes: 2048);

        cut.FindComponent<InputFile>().UploadFiles(
            FileOfBytes(1024, "a.txt"),
            FileOfBytes(1024, "b.txt"),
            FileOfBytes(1024, "c.txt"));

        Assert.NotNull(changed);
        Assert.Equal(2, changed.Count);   // only up to the cap got buffered
        var message = cut.Find(".edit-validation-message").TextContent;
        Assert.Contains("Total size limit", message);
        Assert.Contains("1 file not added", message);
    }

    [Fact]
    public void MaxTotalBytes_zero_disables_the_aggregate_cap()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, maxTotalBytes: 0);

        cut.FindComponent<InputFile>().UploadFiles(
            FileOfBytes(1024, "a.txt"),
            FileOfBytes(1024, "b.txt"),
            FileOfBytes(1024, "c.txt"));

        Assert.NotNull(changed);
        Assert.Equal(3, changed.Count);   // 0 = unlimited, so nothing is turned away
        // The upload-error block is the only role="alert" (FieldValidationDisplay renders always-present,
        // empty .edit-validation-message divs), so its absence means no cap message was produced.
        Assert.Empty(cut.FindAll("div.edit-validation-message[role='alert']"));
    }

    [Fact]
    public void The_total_cap_counts_already_selected_files_from_earlier_batches()
    {
        // The cap must include the bytes already buffered: batch 1 fills 1 KB of a 2 KB budget, so batch 2
        // can add only one more 1 KB file — the second exceeds the total ONLY because of batch 1.
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => { changed = v; model.Files = v; }, maxFileSizeBytes: 4096, maxTotalBytes: 2048);

        cut.FindComponent<InputFile>().UploadFiles(FileOfBytes(1024, "a.txt"));                       // batch 1: total now 1 KB
        cut.FindComponent<InputFile>().UploadFiles(FileOfBytes(1024, "b.txt"), FileOfBytes(1024, "c.txt")); // batch 2

        Assert.NotNull(changed);
        Assert.Equal(2, changed.Count);                       // a.txt + b.txt; c.txt turned away by the running total
        Assert.Equal(["a.txt", "b.txt"], changed.Select(f => f.Name));
        var message = cut.Find(".edit-validation-message").TextContent;
        Assert.Contains("Total size limit", message);
        Assert.Contains("1 file not added", message);
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
    public void Reselecting_the_same_file_is_skipped_and_reported_not_added_twice()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => { changed = v; model.Files = v; });

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello", "a.txt"));
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello", "a.txt")); // re-pick, same name/size/last-modified

        Assert.NotNull(changed);
        Assert.Single(changed); // not two slots for the same file
        Assert.Contains("a.txt is already added.", cut.Find(".edit-validation-message").TextContent);
    }

    [Fact]
    public void Dropping_two_copies_of_the_same_file_in_one_batch_keeps_only_one()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v);

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("hello", "a.txt"),
            InputFileContent.CreateFromText("hello", "a.txt"));

        Assert.NotNull(changed);
        Assert.Single(changed);
        Assert.Contains("a.txt is already added.", cut.Find(".edit-validation-message").TextContent);
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
    public void Remove_button_removes_only_that_file()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => { changed = v; model.Files = v; });

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "a.txt"),
            InputFileContent.CreateFromText("2", "b.txt"));
        Assert.Equal(2, changed!.Count);

        cut.FindAll(".edit-file-delete-btn")[0].Click();

        Assert.Single(changed);
        Assert.Equal("b.txt", changed[0].Name);
    }

    [Fact]
    public void Disabled_drop_zone_does_not_show_the_drag_hover_highlight()
    {
        var cut = RenderEditFile(new FileModel { Files = [] }, isDisabled: true);
        var zone = cut.Find(".edit-file-drop-zone");
        Assert.Contains("disabled", zone.ClassList);

        zone.DragEnter(); // drag a file over the disabled zone

        // The drop is refused when disabled, so the zone must not light up as if it accepts one.
        Assert.DoesNotContain("hover", cut.Find(".edit-file-drop-zone").ClassList);
    }

    [Fact]
    public void Drop_zone_has_no_managed_dragover_handler()
    {
        // Finding 64: dragover fires continuously (~60/s) while a file is dragged over the zone; on
        // Blazor Server each one used to ship a serialized DataTransfer payload over SignalR for a
        // no-op re-render. dragenter/dragleave alone drive the hover highlight now -- dragover keeps
        // only the (handler-less) :preventDefault directive HTML5 drag-and-drop needs for the drop
        // event to fire at all. Triggering a raw dragover with no registered handler throws
        // MissingEventHandlerException, proving the managed handler is actually gone (not just unused).
        var cut = RenderEditFile(new FileModel { Files = [] });
        var zone = cut.Find(".edit-file-drop-zone");

        Assert.Throws<Bunit.MissingEventHandlerException>(() => zone.DragOver());

        // dragenter still drives the highlight on its own, unaffected by the removal.
        zone.DragEnter();
        Assert.Contains("hover", cut.Find(".edit-file-drop-zone").ClassList);
    }

    [Fact]
    public void Read_only_mode_lists_the_file_names_without_a_drop_zone()
    {
        List<IBrowserFile>? uploaded = null;
        var upload = RenderEditFile(new FileModel { Files = [] }, v => uploaded = v);
        upload.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "report.pdf"));

        var model = new FileModel { Files = uploaded! };
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll(".edit-file-drop-zone"));
        Assert.Contains("report.pdf", cut.Find(".edit-file-list--readonly").TextContent);
    }

    // Reads a selected file with NO size argument — a buffered file must ignore the framework's
    // 500 KB default, so a bare OpenReadStream() always succeeds regardless of size.
    static string ReadAll(IBrowserFile f)
    {
        using var s = f.OpenReadStream();
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    [Fact]
    public void Selected_files_are_buffered_into_memory_and_stay_readable()
    {
        // H1: the control buffers each file's bytes at pick time (BufferedBrowserFile) rather than
        // holding the framework IBrowserFile, whose OpenReadStream dies once Blazor wipes the file map.
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello world", "a.txt"));

        Assert.NotNull(changed);
        var stored = Assert.Single(changed);
        Assert.Equal("BufferedBrowserFile", stored.GetType().Name); // not the framework's transient file
        Assert.Equal("a.txt", stored.Name);
        Assert.Equal("hello world", ReadAll(stored));               // bare OpenReadStream() works
    }

    [Fact]
    public void Files_from_multiple_selection_batches_all_survive_and_stay_readable()
    {
        // The core H1 regression: pick one file, then pick another. Both must remain in the list AND
        // both must still be readable. (bUnit can't reproduce the browser file-map wipe that breaks an
        // un-buffered earlier batch, but this proves the buffering that defeats it — batch 1 is read
        // back after batch 2 arrived.)
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => { changed = v; model.Files = v; });

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("first", "a.txt"));
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("second", "b.txt"));

        Assert.NotNull(changed);
        Assert.Equal(2, changed.Count);
        Assert.Equal(["a.txt", "b.txt"], changed.Select(f => f.Name));
        Assert.Equal("first", ReadAll(changed[0]));  // earlier batch still readable after the later one
        Assert.Equal("second", ReadAll(changed[1]));
    }

    [Fact]
    public void Buffered_file_reads_without_a_size_argument_even_above_the_500KB_framework_default()
    {
        // A 600 KB file: the framework's bare OpenReadStream() (512,000-byte default) would throw.
        // The buffered copy serves the whole thing because the bytes are already in memory.
        var big = new string('x', 600 * 1024);
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, maxFileSizeBytes: 1024 * 1024);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(big, "big.txt"));

        Assert.NotNull(changed);
        Assert.Equal(big.Length, ReadAll(Assert.Single(changed)).Length);
    }

    [Fact]
    public void AllowedExtensions_without_a_leading_dot_still_match_and_emit_a_valid_accept()
    {
        // Path.GetExtension always returns the dot, so a consumer's bare "pdf" used to silently
        // reject every file and emit an invalid accept attribute.
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, allowedExtensions: ["pdf", ".txt"]);

        Assert.Equal(".pdf,.txt", cut.Find("input[type=file]").GetAttribute("accept"));

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "a.pdf"),
            InputFileContent.CreateFromText("2", "b.txt"));

        Assert.NotNull(changed);
        Assert.Equal(2, changed.Count);
    }

    [Fact]
    public void At_the_MaxFiles_cap_the_label_stops_pointing_at_the_unmounted_input()
    {
        // The <InputFile> (which carries the control id) unmounts at the cap — the label's `for`
        // must drop with it rather than dangle at a missing id.
        var model = new FileModel { Files = [] };
        var cut = RenderEditFile(model, v => model.Files = v, maxFiles: 1);
        Assert.True(cut.Find("label.edit-label").HasAttribute("for")); // labelable while under the cap

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.txt"));

        Assert.Empty(cut.FindAll("input[type=file]"));
        Assert.False(cut.Find("label.edit-label").HasAttribute("for"));
    }

    [Fact]
    public void Read_only_file_list_is_labelled_by_the_field_label()
    {
        List<IBrowserFile>? uploaded = null;
        var upload = RenderEditFile(new FileModel { Files = [] }, v => uploaded = v);
        upload.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "report.pdf"));

        var model = new FileModel { Files = uploaded! };
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsEditMode", false);
            b.CloseComponent();
        }));

        var list = cut.Find(".edit-file-list--readonly");
        // The naming anchor, not the lbl-{id} label element that also holds the tooltip trigger --
        // see FormLabel's remarks.
        Assert.Equal($"lbltext-{list.GetAttribute("id")}", list.GetAttribute("aria-labelledby"));
    }

    // ------------------------------------------------------------------------------------------
    // Accept tokens: MIME types and MIME wildcards (AntD/native `accept` parity), alongside the
    // pre-existing bare/dotted extension shape covered above.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Full_MIME_type_accept_tokens_are_not_dot_prefixed_and_match_by_ContentType()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, allowedExtensions: ["application/pdf"]);

        // The old extension-only normalizer would have turned this into the meaningless ".application/pdf".
        Assert.Equal("application/pdf", cut.Find("input[type=file]").GetAttribute("accept"));

        // Matches by ContentType, not extension -- a mismatched extension is irrelevant for a MIME token.
        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "report.dat", contentType: "application/pdf"));
        Assert.NotNull(changed);
        Assert.Single(changed);

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("2", "other.pdf", contentType: "application/xml"));
        Assert.Single(changed); // still 1 -- the second file's ContentType doesn't match the token
        Assert.Contains("other.pdf", cut.Find(".edit-validation-message").TextContent);
    }

    [Fact]
    public void MIME_wildcard_accept_tokens_match_any_subtype_case_insensitively()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, allowedExtensions: ["image/*"]);

        Assert.Equal("image/*", cut.Find("input[type=file]").GetAttribute("accept"));

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "a.bin", contentType: "IMAGE/PNG"), // case-insensitive
            InputFileContent.CreateFromText("2", "b.bin", contentType: "image/jpeg"),
            InputFileContent.CreateFromText("3", "c.bin", contentType: "application/pdf"));

        Assert.NotNull(changed);
        Assert.Equal(2, changed.Count); // the two image/* files, not the pdf
        var message = cut.Find(".edit-validation-message").TextContent;
        Assert.Contains("c.bin", message);
        Assert.Contains("image/*", message); // human-readable token list in the rejection message
    }

    [Fact]
    public void Extension_and_MIME_wildcard_accept_tokens_combine_in_one_AllowedExtensions_list()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, allowedExtensions: [".pdf", "image/*"]);

        Assert.Equal(".pdf,image/*", cut.Find("input[type=file]").GetAttribute("accept"));

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "a.pdf", contentType: "application/pdf"), // extension match
            InputFileContent.CreateFromText("2", "b.dat", contentType: "image/png"),        // MIME wildcard match
            InputFileContent.CreateFromText("3", "c.dat", contentType: "text/plain"));      // matches neither

        Assert.NotNull(changed);
        Assert.Equal(["a.pdf", "b.dat"], changed.Select(f => f.Name));
        Assert.Contains("c.dat", cut.Find(".edit-validation-message").TextContent);
    }

    // ------------------------------------------------------------------------------------------
    // Accept-all tokens ("*" / "*/*") and whitespace tolerance in accept tokens.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Bare_star_accept_token_accepts_anything_and_renders_as_the_MIME_wildcard()
    {
        // Before the fix, a bare "*" normalized to the extension ".*" -- which Path.GetExtension never
        // returns -- so it silently rejected every file instead of accepting everything.
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, allowedExtensions: ["*"]);

        Assert.Equal("*/*", cut.Find("input[type=file]").GetAttribute("accept"));

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "a.pdf", contentType: "application/pdf"),
            InputFileContent.CreateFromText("2", "b.exe", contentType: "application/octet-stream"));

        Assert.NotNull(changed);
        Assert.Equal(2, changed.Count);
    }

    [Fact]
    public void Star_slash_star_accept_token_accepts_anything()
    {
        // Before the fix, "*/*" was treated as a MIME wildcard ("image/*"-shaped): stripping its
        // trailing "*" left ContentType.StartsWith("*/"), which is never true.
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, allowedExtensions: ["*/*"]);

        Assert.Equal("*/*", cut.Find("input[type=file]").GetAttribute("accept"));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.txt", contentType: "text/plain"));

        Assert.NotNull(changed);
        Assert.Single(changed);
    }

    [Fact]
    public void Accept_all_token_ORs_with_the_rest_of_a_mixed_AllowedExtensions_list()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        // ".pdf" alone wouldn't match this file -- only the "*" entry does, proving the two tokens OR
        // together rather than the accept-all token requiring the array to contain nothing else.
        var cut = RenderEditFile(model, v => changed = v, allowedExtensions: [".pdf", "*"]);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.bin", contentType: "application/octet-stream"));

        Assert.NotNull(changed);
        Assert.Single(changed);
    }

    [Fact]
    public void Whitespace_around_an_extension_accept_token_is_trimmed_not_rejected()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, allowedExtensions: [" .pdf "]);

        Assert.Equal(".pdf", cut.Find("input[type=file]").GetAttribute("accept"));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.pdf", contentType: "application/pdf"));

        Assert.NotNull(changed);
        Assert.Single(changed);
    }

    [Fact]
    public void Whitespace_around_a_MIME_wildcard_accept_token_is_trimmed_not_rejected()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, allowedExtensions: [" image/* "]);

        Assert.Equal("image/*", cut.Find("input[type=file]").GetAttribute("accept"));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.png", contentType: "image/png"));

        Assert.NotNull(changed);
        Assert.Single(changed);
    }

    // ------------------------------------------------------------------------------------------
    // BeforeAdd: async per-file gate between the built-in checks and buffering.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void BeforeAdd_returning_true_lets_the_file_through()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, beforeAdd: _ => Task.FromResult(true));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.txt"));

        Assert.NotNull(changed);
        Assert.Single(changed);
    }

    [Fact]
    public void BeforeAdd_returning_false_rejects_the_file_with_the_default_message()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v,
            beforeAdd: f => Task.FromResult(f.Name != "blocked.txt"));

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "ok.txt"),
            InputFileContent.CreateFromText("2", "blocked.txt"));

        Assert.NotNull(changed);
        Assert.Equal(["ok.txt"], changed.Select(f => f.Name));
        Assert.Contains("blocked.txt was rejected.", cut.Find(".edit-validation-message").TextContent);
    }

    [Fact]
    public void BeforeAdd_rejection_message_is_localizable()
    {
        var model = new FileModel { Files = [] };
        var cut = RenderEditFile(model, v => model.Files = v,
            beforeAdd: _ => Task.FromResult(false),
            beforeAddRejectedMessageFormat: "{0} wurde abgelehnt.");

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.txt"));

        Assert.Contains("a.txt wurde abgelehnt.", cut.Find(".edit-validation-message").TextContent);
    }

    // Minimal hand-written IBrowserFile -- lets the reentrancy/exception-propagation tests below call
    // LoadFiles directly (via reflection) without going through bUnit's InputFile/dispatcher plumbing,
    // whose exception-surfacing/scheduling behavior for a component event handler is not something to
    // depend on for those assertions. Name/size are overridable so the reentrancy tests can drive two
    // distinguishable (or, for the duplicate-pick test, identical) files through overlapping calls.
    sealed class FakeBrowserFile(string name = "a.txt", long size = 5) : IBrowserFile
    {
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => size;
        public string ContentType => "text/plain";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new MemoryStream(new byte[size]);
    }

    [Fact]
    public async Task BeforeAdd_exceptions_propagate_instead_of_being_swallowed_as_a_rejection()
    {
        // A throwing hook is a bug in the consumer's code, not a file rejection -- LoadFiles must not
        // catch it and turn it into an upload-error message like every other rejection path is.
        // Invoked directly (reflection) rather than through bUnit's UploadFiles/dispatcher plumbing,
        // whose exception-surfacing behavior for a faulted component event handler isn't something to
        // depend on for this assertion -- this calls the exact method under test and awaits its Task.
        var model = new FileModel { Files = [] };
        var cut = RenderEditFile(model, v => model.Files = v,
            beforeAdd: _ => throw new InvalidOperationException("boom"));

        var editFile = cut.FindComponent<EditFile>().Instance;
        var loadFiles = typeof(EditFile).GetMethod("LoadFiles", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var args = new InputFileChangeEventArgs([new FakeBrowserFile()]);

        var task = (Task)loadFiles.Invoke(editFile, [args])!;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);

        Assert.Equal("boom", ex.Message);
    }

    // ------------------------------------------------------------------------------------------
    // Re-entrancy: a second InputFile change event firing while the first is still suspended
    // inside BeforeAdd (the hunter's repro) must not corrupt shared state. Each test gates
    // BeforeAdd on a shared, deliberately-uncompleted TaskCompletionSource, invokes LoadFiles
    // directly via reflection (same rationale as the exception-propagation test above -- this is
    // the exact method under test, not bUnit's InputFile/dispatcher plumbing) once for a "first"
    // pick, then again -- while the first call is still suspended awaiting the gate -- for a
    // "second", overlapping pick. The reentrancy guard is synchronous (set before any await), so
    // the second call must return an already-completed Task without touching Value/_uploadErrors.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Overlapping_LoadFiles_invocations_do_not_bypass_MaxFiles()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var gate = new TaskCompletionSource<bool>();
        var cut = RenderEditFile(model, v => { changed = v; model.Files = v; }, maxFiles: 1,
            beforeAdd: _ => gate.Task);

        var editFile = cut.FindComponent<EditFile>().Instance;
        var loadFiles = typeof(EditFile).GetMethod("LoadFiles", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var firstArgs = new InputFileChangeEventArgs([new FakeBrowserFile("a.txt")]);
        var secondArgs = new InputFileChangeEventArgs([new FakeBrowserFile("b.txt")]);

        var firstTask = (Task)loadFiles.Invoke(editFile, [firstArgs])!; // runs synchronously up to `await gate.Task` and suspends
        var secondTask = (Task)loadFiles.Invoke(editFile, [secondArgs])!; // fires while the first is still in flight

        Assert.True(secondTask.IsCompleted); // the guard short-circuits synchronously -- no interleaving with the first batch

        gate.SetResult(true);
        await firstTask;
        await secondTask;

        Assert.NotNull(changed);
        Assert.Single(changed); // MaxFiles=1 held: the reentrant call never got a chance to add its own file
        Assert.Equal("a.txt", changed[0].Name);
    }

    [Fact]
    public async Task Overlapping_LoadFiles_invocations_do_not_bypass_MaxTotalBytes()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var gate = new TaskCompletionSource<bool>();
        var cut = RenderEditFile(model, v => { changed = v; model.Files = v; },
            maxFileSizeBytes: 4096, maxTotalBytes: 1024, beforeAdd: _ => gate.Task);

        var editFile = cut.FindComponent<EditFile>().Instance;
        var loadFiles = typeof(EditFile).GetMethod("LoadFiles", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var firstArgs = new InputFileChangeEventArgs([new FakeBrowserFile("a.txt", size: 1024)]);
        var secondArgs = new InputFileChangeEventArgs([new FakeBrowserFile("b.txt", size: 1024)]);

        var firstTask = (Task)loadFiles.Invoke(editFile, [firstArgs])!;
        var secondTask = (Task)loadFiles.Invoke(editFile, [secondArgs])!;

        Assert.True(secondTask.IsCompleted);

        gate.SetResult(true);
        await firstTask;
        await secondTask;

        Assert.NotNull(changed);
        // Without the guard, the reentrant call's runningTotal snapshot (taken before the first
        // call's Value assignment) would have let both 1 KB files through against a 1 KB cap.
        Assert.Single(changed);
        Assert.Equal("a.txt", changed[0].Name);
    }

    [Fact]
    public async Task Overlapping_LoadFiles_invocations_with_the_same_file_do_not_crash()
    {
        // The hunter's third repro: two overlapping picks racing to add the same logical file used to
        // risk an ArgumentException from EditContext.NotifyFieldChanged firing concurrently for the
        // same field. With the guard, only one batch ever runs at a time, so this must complete clean.
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var gate = new TaskCompletionSource<bool>();
        var cut = RenderEditFile(model, v => { changed = v; model.Files = v; }, beforeAdd: _ => gate.Task);

        var editFile = cut.FindComponent<EditFile>().Instance;
        var loadFiles = typeof(EditFile).GetMethod("LoadFiles", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var firstArgs = new InputFileChangeEventArgs([new FakeBrowserFile("a.txt")]);
        var secondArgs = new InputFileChangeEventArgs([new FakeBrowserFile("a.txt")]); // same name/size/last-modified shape

        var firstTask = (Task)loadFiles.Invoke(editFile, [firstArgs])!;
        var secondTask = (Task)loadFiles.Invoke(editFile, [secondArgs])!;

        gate.SetResult(true);
        await firstTask;
        await secondTask; // must not throw

        Assert.NotNull(changed);
        Assert.Single(changed); // the reentrant pick of "the same file" never ran far enough to duplicate or crash
    }

    [Fact]
    public void BeforeAdd_does_not_run_for_a_file_rejected_by_the_extension_filter()
    {
        // Format rejection happens before BeforeAdd -- prove the hook never sees a rejected file by
        // making it throw if it's ever invoked (the upload must complete normally, hook untouched).
        var model = new FileModel { Files = [] };
        var cut = RenderEditFile(model,
            allowedExtensions: [".txt"],
            beforeAdd: _ => throw new InvalidOperationException("BeforeAdd must not run for a rejected file."));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.pdf"));

        Assert.Contains("a.pdf", cut.Find(".edit-validation-message").TextContent);
        Assert.Empty(cut.FindAll(".edit-file-item"));
    }

    [Fact]
    public void BeforeAdd_does_not_run_for_a_duplicate_file()
    {
        // Duplicate rejection also happens before BeforeAdd. Seed the list with a file already
        // selected (as an earlier batch would), then re-pick the same file -- it must be caught by
        // the duplicate check and never reach the always-throwing hook.
        var seed = InputFileContent.CreateFromText("hi", "b.txt");
        var seeded = new FileModel { Files = [] };
        var seedCut = RenderEditFile(seeded, v => seeded.Files = v);
        seedCut.FindComponent<InputFile>().UploadFiles(seed);

        var model = new FileModel { Files = seeded.Files };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v,
            beforeAdd: _ => throw new InvalidOperationException("BeforeAdd must not run for a duplicate file."));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hi", "b.txt"));

        Assert.Contains("b.txt is already added.", cut.Find(".edit-validation-message").TextContent);
        Assert.Null(changed); // ValueChanged never fires -- nothing new was added
    }

    // ------------------------------------------------------------------------------------------
    // File size in the list rows (edit-mode and read-only).
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Edit_mode_list_shows_each_files_formatted_size()
    {
        var model = new FileModel { Files = [] };
        var cut = RenderEditFile(model, v => model.Files = v);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(new string('x', 2048), "a.txt"));

        Assert.Equal("2 KB", cut.Find(".edit-file-size").TextContent);
    }

    [Fact]
    public void Read_only_list_also_shows_each_files_formatted_size()
    {
        List<IBrowserFile>? uploaded = null;
        var upload = RenderEditFile(new FileModel { Files = [] }, v => uploaded = v);
        upload.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello", "report.pdf"));

        var model = new FileModel { Files = uploaded! };
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsEditMode", false);
            b.CloseComponent();
        }));

        var item = cut.Find(".edit-file-list--readonly .edit-file-item");
        Assert.Contains("report.pdf", item.TextContent);
        Assert.Equal("5 B", cut.Find(".edit-file-list--readonly .edit-file-size").TextContent);
    }

    [Fact]
    public void Empty_state_DOM_is_unchanged_by_the_size_display_and_Button_variant_additions()
    {
        // No files, default (unset) Variant -- none of this batch's new markup should appear.
        var cut = RenderEditFile(new FileModel { Files = [] });

        Assert.Single(cut.FindAll(".edit-file-drop-zone"));
        Assert.Empty(cut.FindAll(".edit-file-list"));
        Assert.Empty(cut.FindAll(".edit-file-size"));
        Assert.Empty(cut.FindAll(".edit-file-select-btn"));
    }

    // ------------------------------------------------------------------------------------------
    // Variant="Button": compact plain-button picker, same validation/caps, no dropzone.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Button_variant_renders_a_button_not_a_drop_zone()
    {
        var cut = RenderEditFile(new FileModel { Files = [] }, variant: EditFileVariant.Button);

        Assert.Empty(cut.FindAll(".edit-file-drop-zone"));
        var btn = cut.Find(".edit-file-select-btn");
        Assert.Equal("Select Files", btn.TextContent.Trim());
        Assert.NotNull(cut.Find(".edit-file-select-btn input[type=file]"));
    }

    [Fact]
    public void Button_variant_ButtonText_is_overridable()
    {
        var cut = RenderEditFile(new FileModel { Files = [] }, variant: EditFileVariant.Button, buttonText: "Upload Documents");

        Assert.Equal("Upload Documents", cut.Find(".edit-file-select-btn").TextContent.Trim());
    }

    [Fact]
    public void Button_variant_applies_the_same_validation_as_the_drop_zone()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, variant: EditFileVariant.Button, maxFiles: 1);

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "a.txt"),
            InputFileContent.CreateFromText("2", "b.txt"));

        Assert.NotNull(changed);
        Assert.Single(changed);
        Assert.Contains("Only 1 file allowed — 1 not added.", cut.Find(".edit-validation-message").TextContent);
    }

    [Fact]
    public void Button_variant_disables_its_input_when_IsDisabled()
    {
        var cut = RenderEditFile(new FileModel { Files = [] }, variant: EditFileVariant.Button, isDisabled: true);

        Assert.True(cut.Find("input[type=file]").HasAttribute("disabled"));
        Assert.Contains("disabled", cut.Find(".edit-file-select-btn").ClassList);
    }

    [Fact]
    public void Button_variant_unmounts_its_input_at_the_MaxFiles_cap_like_the_drop_zone()
    {
        var model = new FileModel { Files = [] };
        var cut = RenderEditFile(model, v => model.Files = v, variant: EditFileVariant.Button, maxFiles: 1);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.txt"));

        Assert.Empty(cut.FindAll("input[type=file]"));
        Assert.Empty(cut.FindAll(".edit-file-select-btn"));
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
                b.AddAttribute(3, "ValueExpression", field);
                b.AddAttribute(4, "Options", new List<string> { "a", "b" });
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.FindAll("input[type=checkbox]")[0].Change(true);

        Assert.NotNull(changed);
        Assert.Equal(["a"], changed);
    }

    // ----- Upload-error state vs. parameter changes ---------------------------
    // _uploadErrors drives aria-invalid, the red drop zone and the role="alert" block, and nothing
    // used to reset it on a parameter change -- so a rejection outlived the IsEditMode round trip
    // that hid it and followed the control onto a swapped bound record.

    class ErrorStateHolder
    {
        public FileModel Model = new() { Files = [] };
        public bool IsEditMode = true;
        public bool IsDisabled;
    }

    // The one harness these need that RenderEditFile can't give: a re-renderable parent, so
    // IsEditMode/IsDisabled/the bound record can each change AFTER the first render. Every attribute
    // is read from the holder at render time; `cut.Render(...)` on the EditForm replays the fragment.
    IRenderedComponent<EditForm> RenderErrorStateHost(ErrorStateHolder holder) =>
        Render<EditForm>(ps => ps
            .Add(f => f.Model, holder.Model)
            .Add(f => f.ChildContent, (RenderFragment<EditContext>)(_ => b =>
            {
                b.OpenComponent<EditFile>(0);
                b.AddAttribute(1, "Value", holder.Model.Files);
                b.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<List<IBrowserFile>>(
                    this, v => holder.Model.Files = v));
                b.AddAttribute(3, "ValueExpression", (Expression<Func<List<IBrowserFile>>>)(() => holder.Model.Files));
                b.AddAttribute(4, "IsEditMode", holder.IsEditMode);
                b.AddAttribute(5, "IsDisabled", holder.IsDisabled);
                b.AddAttribute(6, "AllowedExtensions", new[] { ".pdf" });
                b.CloseComponent();
            })));

    // Re-runs the parent's render with whatever the holder now says, the way a real parent re-renders
    // after its own state changed.
    static void Rerender(IRenderedComponent<EditForm> cut, ErrorStateHolder holder) =>
        cut.Render(ps => ps.Add(f => f.Model, holder.Model));

    [Fact]
    public void An_upload_rejection_does_not_survive_an_IsEditMode_round_trip()
    {
        var holder = new ErrorStateHolder();
        var cut = RenderErrorStateHost(holder);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.txt")); // wrong extension
        Assert.Single(cut.FindAll(".edit-validation-message[role='alert']"));
        Assert.Equal("true", cut.Find("input[type=file]").GetAttribute("aria-invalid"));

        holder.IsEditMode = false;
        Rerender(cut, holder);
        Assert.Empty(cut.FindAll(".edit-validation-message[role='alert']"));

        holder.IsEditMode = true;
        Rerender(cut, holder);

        // The rejection belonged to an upload gesture two mode flips ago -- it must not come back with
        // the editor, red drop zone and aria-invalid included.
        Assert.Empty(cut.FindAll(".edit-validation-message[role='alert']"));
        Assert.False(cut.Find("input[type=file]").HasAttribute("aria-invalid"));
        Assert.DoesNotContain("error", cut.Find(".edit-file-drop-zone").ClassList);
    }

    [Fact]
    public void An_upload_rejection_does_not_follow_a_swapped_bound_record()
    {
        var holder = new ErrorStateHolder();
        var cut = RenderErrorStateHost(holder);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.txt"));
        Assert.Single(cut.FindAll(".edit-validation-message[role='alert']"));

        // The parent swaps the bound record (an empty file list either side, so only the list's
        // identity distinguishes them -- exactly the case a content comparison would miss).
        holder.Model = new FileModel { Files = [] };
        Rerender(cut, holder);

        Assert.Empty(cut.FindAll(".edit-validation-message[role='alert']"));
        Assert.False(cut.Find("input[type=file]").HasAttribute("aria-invalid"));
    }

    [Fact]
    public void A_batch_that_both_accepts_and_rejects_keeps_its_rejection_messages()
    {
        // The other half of the record-swap reset: one batch can accept some files (a commit, whose
        // parameter echo must NOT read as an external change) while rejecting others, and those
        // rejections are the only feedback the user gets about the skipped files.
        var holder = new ErrorStateHolder();
        var cut = RenderErrorStateHost(holder);

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "good.pdf"),
            InputFileContent.CreateFromText("2", "bad.txt"));

        // The parent re-renders on the commit, as a real @bind-Value parent does.
        Rerender(cut, holder);

        Assert.Single(holder.Model.Files);
        Assert.Contains("bad.txt", cut.Find(".edit-validation-message[role='alert']").TextContent);
    }

    [Fact]
    public void Going_disabled_mid_drag_clears_the_drop_zone_hover_highlight()
    {
        // OnDragEnter's own guard only covers a drag that STARTS while disabled -- a drag already in
        // progress left the zone rendering "hover disabled", lit up as accepting a drop it refuses.
        var holder = new ErrorStateHolder();
        var cut = RenderErrorStateHost(holder);

        cut.Find(".edit-file-drop-zone").DragEnter();
        Assert.Contains("hover", cut.Find(".edit-file-drop-zone").ClassList);

        holder.IsDisabled = true;
        Rerender(cut, holder);

        var zone = cut.Find(".edit-file-drop-zone");
        Assert.DoesNotContain("hover", zone.ClassList);
        Assert.Contains("disabled", zone.ClassList);
    }

    // ------------------------------------------------------------------------------------------
    // Bordered: wraps the label and the picker/file-list together in one card. AllowDownload:
    // the file name becomes a clickable link that re-saves the already-buffered bytes.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Bordered_defaults_to_false_and_renders_no_card()
    {
        var cut = RenderEditFile(new FileModel { Files = [] });

        Assert.Empty(cut.FindAll(".edit-file-card"));
    }

    [Fact]
    public void Bordered_wraps_the_label_and_the_drop_zone_in_one_card()
    {
        var model = new FileModel { Files = [] };
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Label", "Attachments");
            b.AddAttribute(4, "Bordered", true);
            b.CloseComponent();
        }));

        var card = cut.Find(".edit-file-card");
        Assert.Contains("Attachments", card.QuerySelector("label.edit-label")!.TextContent);
        Assert.NotNull(card.QuerySelector(".edit-file-drop-zone"));
    }

    [Fact]
    public void Bordered_wraps_the_read_only_file_list_too()
    {
        List<IBrowserFile>? uploaded = null;
        var upload = RenderEditFile(new FileModel { Files = [] }, v => uploaded = v);
        upload.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "report.pdf"));

        var model = new FileModel { Files = uploaded! };
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsEditMode", false);
            b.AddAttribute(4, "Bordered", true);
            b.CloseComponent();
        }));

        var card = cut.Find(".edit-file-card");
        Assert.Contains("report.pdf", card.QuerySelector(".edit-file-list--readonly")!.TextContent);
    }

    [Fact]
    public void AllowDownload_defaults_to_false_and_renders_a_plain_name_span()
    {
        var model = new FileModel { Files = [] };
        var cut = RenderEditFile(model, v => model.Files = v);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.txt"));

        Assert.NotNull(cut.Find("span.edit-file-name"));
        Assert.Empty(cut.FindAll(".edit-file-name-link"));
    }

    [Fact]
    public void AllowDownload_renders_the_name_as_a_clickable_link_in_edit_mode()
    {
        var model = new FileModel { Files = [] };
        var cut = RenderEditFile(model, v => model.Files = v, allowDownload: true);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.txt"));

        var link = cut.Find("button.edit-file-name-link");
        Assert.Equal("a.txt", link.TextContent);
    }

    [Fact]
    public void AllowDownload_renders_the_name_as_a_clickable_link_in_read_only_mode()
    {
        List<IBrowserFile>? uploaded = null;
        var upload = RenderEditFile(new FileModel { Files = [] }, v => uploaded = v);
        upload.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "report.pdf"));

        var model = new FileModel { Files = uploaded! };
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsEditMode", false);
            b.AddAttribute(4, "AllowDownload", true);
            b.CloseComponent();
        }));

        var link = cut.Find(".edit-file-list--readonly button.edit-file-name-link");
        Assert.Equal("report.pdf", link.TextContent);
    }

    [Fact]
    public async Task Clicking_the_download_link_hands_the_buffered_bytes_to_JS()
    {
        // Loose mode + inspecting JSInterop.Invocations, not SetupVoid with exact args: the .NET-side
        // byte[] this passes isn't something a Setup's arg matcher can be pre-told to expect (it's
        // built from the buffered file at click time), so the interesting assertions are on what the
        // interop call actually carried, not on whether a pre-registered exact-args match fired.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var model = new FileModel { Files = [] };
        var cut = RenderEditFile(model, v => model.Files = v, allowDownload: true);
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello world", "a.txt"));

        await cut.Find("button.edit-file-name-link").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        var invocation = Assert.Single(JSInterop.Invocations, i => i.Identifier == "WssEditControls.downloadFile");
        var bytes = Assert.IsType<byte[]>(invocation.Arguments[0]);
        Assert.Equal("hello world", System.Text.Encoding.UTF8.GetString(bytes));
        Assert.Equal("a.txt", invocation.Arguments[1]);
        Assert.Equal("", invocation.Arguments[2]); // InputFileContent.CreateFromText leaves ContentType unset
    }
}
