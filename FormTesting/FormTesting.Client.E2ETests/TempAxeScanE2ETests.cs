using System.Text.Json;
using Controls.Demo;

namespace FormTesting.Client.E2ETests;

// TEMPORARY measurement harness -- delete before committing. Injects axe-core 4.10.2 from CDN into
// the read-only All Controls demo form and prints the aria-prohibited-attr node count, every other
// rule that fires, and a dangling-IDREF audit of every aria-describedby/aria-labelledby on the page.
public class TempAxeScanE2ETests(AppFixture app, BrowserFixture browser) : PageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.AllControls;

    [Fact]
    public async Task Scan()
    {
        await NavigateAsync();

        // Flip the form into read-only mode (the toggle starts ON).
        await Page.Locator("button", new() { HasTextString = "Edit Mode" }).First.ClickAsync();
        await Expect(Page.Locator(".edit-readonly-value").First).ToBeVisibleAsync(new() { Timeout = 15_000 });

        await Page.AddScriptTagAsync(new PageAddScriptTagOptions
        {
            Url = "https://cdn.jsdelivr.net/npm/axe-core@4.10.2/axe.min.js"
        });

        var json = await Page.EvaluateAsync<JsonElement>(@"async () => {
            const r = await axe.run(document.body, { resultTypes: ['violations', 'incomplete'] });
            const summarize = (arr) => arr.map(x => x.id + ' x' + x.nodes.length);
            const prohibited = [...r.violations, ...r.incomplete]
                .filter(x => x.id === 'aria-prohibited-attr');

            // Every IDREF token on the page that points at nothing.
            const dangling = [];
            for (const attr of ['aria-describedby', 'aria-labelledby']) {
                for (const el of document.querySelectorAll('[' + attr + ']')) {
                    for (const token of el.getAttribute(attr).split(/\s+/).filter(Boolean)) {
                        if (!document.getElementById(token))
                            dangling.push((el.id || el.tagName) + ' ' + attr + ' -> ' + token);
                    }
                }
            }

            // Accessible name of the read-only checked-list groups, via axe's own accname impl
            // (needs axe's virtual tree set up first).
            axe.setup(document.body);
            const groupNames = [...document.querySelectorAll('fieldset.edit-checkedList-fieldset')]
                .map(f => axe.commons.text.accessibleText(f));
            axe.teardown();

            return {
                axeVersion: axe.version,
                readonlyNodes: document.querySelectorAll('.edit-readonly-value').length,
                colorNodes: document.querySelectorAll('.edit-color-readonly-value').length,
                prohibitedNodes: prohibited.reduce((n, x) => n + x.nodes.length, 0),
                prohibitedTargets: prohibited.flatMap(x => x.nodes.map(n => n.target.join(' '))),
                allViolations: summarize(r.violations),
                allIncomplete: summarize(r.incomplete),
                danglingIdrefs: dangling,
                checkedListGroupNames: groupNames
            };
        }");

        Console.WriteLine("AXE-RESULT-BEGIN");
        Console.WriteLine(JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("AXE-RESULT-END");
    }
}
