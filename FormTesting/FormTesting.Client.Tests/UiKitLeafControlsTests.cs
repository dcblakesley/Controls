using Microsoft.AspNetCore.Components;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit smoke tests for the ported non-form UI-kit leaf controls (Alert, Skeleton, Tooltip,
/// Popover, Pagination). These are plain components (no EditForm / EditContext), so they render
/// directly via RenderComponent.
/// </summary>
public class UiKitLeafControlsTests : TestContext
{
    [Fact]
    public void Alert_renders_message_and_type_class()
    {
        var cut = RenderComponent<Alert>(p => p
            .Add(a => a.Type, AlertType.Error)
            .Add(a => a.Message, "Something failed"));

        Assert.Contains("wss-alert-error", cut.Find(".wss-alert").ClassList);
        Assert.Contains("Something failed", cut.Find(".wss-alert-message").TextContent);
    }

    [Fact]
    public void Alert_role_and_aria_live_match_severity()
    {
        // Error interrupts (assertive); non-error severities announce politely — matching the toast views.
        var error = RenderComponent<Alert>(p => p.Add(a => a.Type, AlertType.Error).Add(a => a.Message, "x"));
        Assert.Equal("alert", error.Find(".wss-alert").GetAttribute("role"));
        Assert.Equal("assertive", error.Find(".wss-alert").GetAttribute("aria-live"));

        var info = RenderComponent<Alert>(p => p.Add(a => a.Type, AlertType.Info).Add(a => a.Message, "x"));
        Assert.Equal("status", info.Find(".wss-alert").GetAttribute("role"));
        Assert.Equal("polite", info.Find(".wss-alert").GetAttribute("aria-live"));
    }

    [Fact]
    public void Alert_closable_invokes_OnClose()
    {
        var closed = false;
        var cut = RenderComponent<Alert>(p => p
            .Add(a => a.Message, "x")
            .Add(a => a.Closable, true)
            .Add(a => a.OnClose, EventCallback.Factory.Create(this, () => closed = true)));

        cut.Find(".wss-alert-close").Click();
        Assert.True(closed);
    }

    [Fact]
    public void Skeleton_loading_renders_requested_rows()
    {
        var cut = RenderComponent<Skeleton>(p => p.Add(s => s.Rows, 4));
        Assert.Equal(4, cut.FindAll(".wss-skeleton-paragraph > li").Count);
    }

    [Fact]
    public void Skeleton_not_loading_renders_child_content()
    {
        var cut = RenderComponent<Skeleton>(p => p
            .Add(s => s.Loading, false)
            .AddChildContent("<span class=\"loaded\">done</span>"));

        Assert.NotNull(cut.Find("span.loaded"));
        Assert.Empty(cut.FindAll(".wss-skeleton"));
    }

    [Fact]
    public void Popover_shows_content_after_trigger_click()
    {
        var cut = RenderComponent<Popover>(p => p
            .Add(pp => pp.Content, (RenderFragment)(b => b.AddMarkupContent(0, "<div class=\"pop-body\">body</div>")))
            .AddChildContent("<button>open</button>"));

        Assert.Empty(cut.FindAll(".wss-popover"));
        cut.Find(".wss-popover-trigger").Click();
        Assert.Contains("body", cut.Find(".wss-popover-content").TextContent);
    }

    [Fact]
    public void Pagination_renders_pages_and_raises_change_on_click()
    {
        var page = 1;
        var cut = RenderComponent<Pagination>(p => p
            .Add(pg => pg.Total, 45)
            .Add(pg => pg.PageSize, 10)
            .Add(pg => pg.Current, 1)
            .Add(pg => pg.CurrentChanged, EventCallback.Factory.Create<int>(this, v => page = v)));

        // 45 / 10 => 5 pages.
        Assert.Equal(5, cut.FindAll(".wss-pagination-item").Count);
        cut.FindAll(".wss-pagination-item")[2].Click(); // third page
        Assert.Equal(3, page);
    }

    [Fact]
    public void Pagination_clamps_an_out_of_range_current_so_prev_works_and_next_is_disabled()
    {
        var page = 0;
        var cut = RenderComponent<Pagination>(p => p
            .Add(pg => pg.Total, 25)    // 25 / 10 => 3 pages
            .Add(pg => pg.PageSize, 10)
            .Add(pg => pg.Current, 5)   // past the last page (the parent hasn't clamped it)
            .Add(pg => pg.CurrentChanged, EventCallback.Factory.Create<int>(this, v => page = v)));

        var prev = cut.Find(".wss-pagination-prev");
        var next = cut.Find(".wss-pagination-next");
        Assert.False(prev.HasAttribute("disabled")); // treated as the last page, so Previous is live...
        Assert.True(next.HasAttribute("disabled"));   // ...and Next is disabled (already at the end)
        Assert.Equal("3", cut.Find(".wss-pagination-item-active").TextContent); // clamped 3, not raw 5

        prev.Click();
        Assert.Equal(2, page); // Previous navigates from the clamped 3 to 2, not an inert no-op
    }

    [Fact]
    public void Pagination_windows_large_page_counts_with_ellipsis()
    {
        var cut = RenderComponent<Pagination>(p => p
            .Add(pg => pg.Total, 200)   // 200 / 10 => 20 pages
            .Add(pg => pg.PageSize, 10)
            .Add(pg => pg.Current, 10));

        // Far more pages than fit: the middle collapses behind ellipses instead of 20 buttons.
        var items = cut.FindAll(".wss-pagination-item");
        Assert.True(items.Count < 20, $"expected a windowed set, got {items.Count} page buttons");
        Assert.NotEmpty(cut.FindAll(".wss-pagination-ellipsis"));

        // First and last page stay reachable, and the current page is marked.
        Assert.Contains(items, i => i.TextContent == "1");
        Assert.Contains(items, i => i.TextContent == "20");
        Assert.Equal("10", cut.Find(".wss-pagination-item-active").TextContent);
    }

    [Fact]
    public void Pagination_shows_every_page_when_they_all_fit()
    {
        var cut = RenderComponent<Pagination>(p => p
            .Add(pg => pg.Total, 70)    // 70 / 10 => 7 pages (the no-ellipsis threshold)
            .Add(pg => pg.PageSize, 10)
            .Add(pg => pg.Current, 1));

        Assert.Equal(7, cut.FindAll(".wss-pagination-item").Count);
        Assert.Empty(cut.FindAll(".wss-pagination-ellipsis"));
    }
}
