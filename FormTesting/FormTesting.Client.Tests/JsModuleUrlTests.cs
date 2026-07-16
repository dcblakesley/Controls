// These tests construct FormDefaults directly to exercise JsModuleUrl.Resolve / cascade a fixed
// AssetBase; setting the [Parameter] AssetBase that way is exactly what BL0005 warns about, but it's
// intentional and safe here (mirrors SelectParsingTests' rationale).
#pragma warning disable BL0005

namespace FormTesting.Client.Tests;

/// <summary>
/// <see cref="JsModuleUrl"/> builds the specifier every <c>wss-*.js</c> lazy import resolves through.
/// Covers the pure URL-building logic plus one end-to-end check (via <see cref="Select{TValue}"/>)
/// that a cascaded <see cref="FormDefaults.AssetBase"/> actually reaches the JS interop call, not just
/// the helper in isolation.
/// </summary>
public class JsModuleUrlTests : TestContext
{
    [Fact]
    public void No_FormDefaults_resolves_the_existing_relative_path()
    {
        var url = JsModuleUrl.Resolve(null, "wss-overlay.js");
        Assert.Equal("./_content/WssBlazorControls/wss-overlay.js", url);
    }

    [Fact]
    public void FormDefaults_with_no_AssetBase_resolves_the_existing_relative_path()
    {
        var url = JsModuleUrl.Resolve(new FormDefaults(), "wss-select.js");
        Assert.Equal("./_content/WssBlazorControls/wss-select.js", url);
    }

    [Fact]
    public void AssetBase_is_prefixed_onto_the_module_path()
    {
        var url = JsModuleUrl.Resolve(new FormDefaults { AssetBase = "https://mfe.example.com" }, "wss-table.js");
        Assert.Equal("https://mfe.example.com/_content/WssBlazorControls/wss-table.js", url);
    }

    [Fact]
    public void A_trailing_slash_on_AssetBase_does_not_produce_a_double_slash()
    {
        var url = JsModuleUrl.Resolve(new FormDefaults { AssetBase = "https://mfe.example.com/" }, "wss-overlay.js");
        Assert.Equal("https://mfe.example.com/_content/WssBlazorControls/wss-overlay.js", url);
    }

    static List<SelectOption<string>> Opts(params string[] values) =>
        values.Select(v => new SelectOption<string>(v, v)).ToList();

    [Fact]
    public void A_cascaded_AssetBase_redirects_the_component_s_JS_import()
    {
        // If the cascade weren't wired through to the "import" call, this setup wouldn't match the
        // actual invocation, placeDropdown would never run, and the z-index would never appear.
        var module = JSInterop.SetupModule("https://mfe.example.com/_content/WssBlazorControls/wss-select.js");
        module.Setup<int>("placeDropdown", _ => true).SetResult(1051);

        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts("A", "B"))
            .Add(s => s.DefaultOpen, true)
            .AddCascadingValue(new FormDefaults { AssetBase = "https://mfe.example.com" }));

        cut.WaitForAssertion(() =>
            Assert.Contains("z-index:1051", cut.Find(".wss-select").GetAttribute("style") ?? string.Empty));
    }
}
