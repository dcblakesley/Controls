using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FormTesting.Client.Tests;

/// <summary>
/// <see cref="FormDefaults"/> rendered OUT OF BAND — through
/// <see cref="Microsoft.AspNetCore.Components.Web.HtmlRenderer"/> with a bare service provider, the
/// shape an email template, a PDF pipeline or a static-HTML export uses. Deliberately not a
/// <c>BunitContext</c> test: bUnit registers a full Blazor service set (an <see cref="IJSRuntime"/>
/// included), which is exactly the dependency these tests exist to prove the component does NOT
/// require. Every service this renderer has is listed in <see cref="RenderAsync"/> — nothing.
/// </summary>
public class FormDefaultsHtmlRendererTests
{
    static async Task<string> RenderAsync(bool? focusFirstField)
    {
        // No AddLogging, no interop, no Blazor services at all: HtmlRenderer takes its logger factory
        // directly, so an EMPTY container is a legal host for it (and the closest thing to the
        // minimal provider the framework's own out-of-band rendering sample builds).
        var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(FormDefaults.FocusFirstField)] = focusFirstField,
                [nameof(FormDefaults.ChildContent)] = (RenderFragment)(b =>
                {
                    b.OpenElement(0, "p");
                    b.AddContent(1, "hi");
                    b.CloseElement();
                }),
            });
            var output = await renderer.RenderComponentAsync<FormDefaults>(parameters);
            return output.ToHtmlString();
        });
    }

    [Fact]
    public async Task Renders_without_an_IJSRuntime_registered_while_the_feature_is_off()
    {
        // The regression this pins: an unconditional [Inject] IJSRuntime resolves during
        // SetParametersAsync -- before anything looks at whether the feature is even on -- so merely
        // having <FormDefaults> in the tree threw InvalidOperationException ("no registered service
        // of type 'Microsoft.JSInterop.IJSRuntime'") in every non-host renderer. Default-off has to
        // be byte-identical in the DI contract too, not just in the render tree.
        Assert.Equal("<p>hi</p>", await RenderAsync(null));
    }

    [Fact]
    public async Task Renders_without_an_IJSRuntime_registered_while_the_feature_is_on()
    {
        // Armed, and still no interop dependency: the markers render, and the focus move simply never
        // happens -- the same graceful degradation as prerender (see JsInteropEc's best-effort
        // contract). HtmlRenderer never calls OnAfterRender at all, so this is purely about the
        // component's DI requirements.
        var html = await RenderAsync(true);

        Assert.Contains("<template id=\"wss-focus-scope-", html);
        Assert.Contains("<p>hi</p>", html);
    }
}
