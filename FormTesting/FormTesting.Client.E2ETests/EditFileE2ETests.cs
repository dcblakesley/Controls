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
}
