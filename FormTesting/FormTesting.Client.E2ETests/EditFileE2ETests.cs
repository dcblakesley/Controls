using System.Text;
using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditFileE2ETests(AppFixture app, BrowserFixture browser) : PageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.File;

    static FilePayload TextFile(string name, string content = "hello") => new()
    {
        Name = name,
        MimeType = "text/plain",
        Buffer = Encoding.UTF8.GetBytes(content),
    };

    [Fact]
    public async Task Demo_page_renders_with_expected_heading()
    {
        await NavigateAsync();
        await Expect(Page.Locator("h1", new() { HasTextString = "EditFile Demo" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Upload_lists_the_file_and_remove_deletes_it()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section").First; // Basic — all file types

        await section.Locator("input[type=file]").SetInputFilesAsync(TextFile("hello.txt"));
        await Expect(section.Locator(".edit-file-item")).ToHaveCountAsync(1);
        await Expect(section.Locator(".edit-file-name")).ToHaveTextAsync("hello.txt");

        // The remove button reveals on row hover (or keyboard focus).
        await section.Locator(".edit-file-item").HoverAsync();
        await section.Locator(".edit-file-delete-btn").ClickAsync();
        await Expect(section.Locator(".edit-file-item")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Rejected_extension_reports_the_file_by_name()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section").Nth(1); // Extension filter + max files (.pdf/.xlsx)

        await section.Locator("input[type=file]").SetInputFilesAsync(TextFile("notes.txt"));

        await Expect(section.Locator(".edit-validation-message").First).ToContainTextAsync("notes.txt");
        await Expect(section.Locator(".edit-file-item")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Clicking_the_drop_zone_away_from_the_top_edge_opens_the_file_chooser()
    {
        await NavigateAsync();
        var dropZone = Page.Locator("section.demo-section").First.Locator(".edit-file-drop-zone");
        var box = await dropZone.BoundingBoxAsync();

        // Regression: the invisible <input type=file> only stretched over the zone via `inset: 0`,
        // with nothing to outweigh a host app's own `input { height: ... }` reset for the `height`
        // property specifically -- it shrank to that fixed height at the top of the zone, so a click
        // anywhere lower (where the icon/text actually sit) missed the input and no picker opened.
        var fileChooser = await Page.RunAndWaitForFileChooserAsync(() => dropZone.ClickAsync(new()
        {
            Position = new Position { X = box!.Width / 2, Y = box.Height - 8 },
        }));

        Assert.NotNull(fileChooser);
    }

    [Fact]
    public async Task Toggling_FormOptions_edit_mode_hides_the_drop_zone()
    {
        await NavigateAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Edit Mode" }).ClickAsync();

        var firstSection = Page.Locator("section.demo-section").First;
        await Expect(firstSection.Locator(".edit-file-drop-zone")).Not.ToBeVisibleAsync();
        await Expect(firstSection.Locator(".edit-readonly-value").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Visual_baseline_basic_section()
    {
        await NavigateAsync();
        var firstSection = Page.Locator("section.demo-section").First;
        await Expect(firstSection).ToBeVisibleAsync();
        await ExpectMatchesBaselineAsync(firstSection, "basic-section");
    }

    [Fact]
    public async Task Button_variant_has_no_drop_zone_and_lists_a_real_file_pick()
    {
        await NavigateAsync();
        // Heading-scoped rather than positional -- resilient to future sections being inserted
        // between this one and the ones the other tests in this class index by Nth().
        var section = Page.Locator("section.demo-section", new() { HasTextString = "Compact button" });

        await Expect(section.Locator(".edit-file-drop-zone")).Not.ToBeVisibleAsync();
        var selectBtn = section.Locator(".edit-file-select-btn");
        await Expect(selectBtn).ToBeVisibleAsync();
        await Expect(selectBtn).ToContainTextAsync("Select Files");

        await section.Locator("input[type=file]").SetInputFilesAsync(TextFile("picked.txt"));

        await Expect(section.Locator(".edit-file-item")).ToHaveCountAsync(1);
        await Expect(section.Locator(".edit-file-name")).ToHaveTextAsync("picked.txt");
    }
}
