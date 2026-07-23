using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for the standalone <see cref="Pagination"/>'s AntD 4.x parity batch: ShowTotal, the
/// PageSizeOptions size-changer, ShowQuickJumper, and the Small compact variant. All four are
/// additive/opt-in — the baseline (existing) rendering test guards that leaving them unset keeps the
/// original DOM shape.
/// </summary>
public class PaginationTests : TestContext
{
    [Fact]
    public void With_no_new_parameters_set_the_DOM_shape_is_unchanged()
    {
        var cut = RenderComponent<Pagination>(p => p
            .Add(c => c.Total, 95)
            .Add(c => c.PageSize, 10)
            .Add(c => c.Current, 1));

        Assert.Empty(cut.FindAll(".wss-pagination-total"));
        Assert.Empty(cut.FindAll(".wss-pagination-size-changer"));
        Assert.Empty(cut.FindAll(".wss-pagination-jumper"));
        Assert.DoesNotContain("wss-pagination-sm", cut.Find(".wss-pagination").ClassList);
    }

    [Fact]
    public void ShowTotal_renders_the_leading_total_text_before_the_pager_buttons()
    {
        var cut = RenderComponent<Pagination>(p => p
            .Add(c => c.Total, 95)
            .Add(c => c.PageSize, 10)
            .Add(c => c.Current, 3)
            .Add(c => c.ShowTotal, (Func<(int Start, int End, int Total), string>)(w => $"{w.Start}-{w.End} of {w.Total} items")));

        var total = cut.Find(".wss-pagination-total");
        Assert.Equal("21-30 of 95 items", total.TextContent);

        // Leads the pager: the first child of the nav.
        Assert.Contains("wss-pagination-total", cut.Find(".wss-pagination").Children[0].ClassList);
    }

    [Fact]
    public void ShowTotal_reports_an_empty_window_when_Total_is_zero()
    {
        var cut = RenderComponent<Pagination>(p => p
            .Add(c => c.Total, 0)
            .Add(c => c.ShowTotal, (Func<(int Start, int End, int Total), string>)(w => $"{w.Start}-{w.End} of {w.Total}")));

        Assert.Equal("0-0 of 0", cut.Find(".wss-pagination-total").TextContent);
    }

    [Fact]
    public void PageSizeOptions_renders_a_size_changer_with_the_current_size_folded_in()
    {
        var cut = RenderComponent<Pagination>(p => p
            .Add(c => c.Total, 95)
            .Add(c => c.PageSize, 25) // not present in PageSizeOptions below
            .Add(c => c.Current, 1)
            .Add(c => c.PageSizeOptions, new[] { 10, 20, 50 }));

        var options = cut.FindAll(".wss-pagination-size-select option");
        var values = options.Select(o => o.GetAttribute("value")).ToArray();
        Assert.Equal(new[] { "10", "20", "25", "50" }, values); // sorted, current size included

        var selected = options.Single(o => o.HasAttribute("selected"));
        Assert.Equal("25", selected.GetAttribute("value"));
        Assert.Equal("25 / page", selected.TextContent);
    }

    [Fact]
    public void Changing_the_page_size_reclamps_Current_to_keep_the_same_data_window()
    {
        int? newSize = null;
        int? newCurrent = null;
        // 95 items, PageSize 10, on page 3 -> first visible item is index 20 (0-based).
        var cut = RenderComponent<Pagination>(p => p
            .Add(c => c.Total, 95)
            .Add(c => c.PageSize, 10)
            .Add(c => c.Current, 3)
            .Add(c => c.PageSizeOptions, new[] { 10, 20, 50 })
            .Add(c => c.PageSizeChanged, EventCallback.Factory.Create<int>(this, s => newSize = s))
            .Add(c => c.CurrentChanged, EventCallback.Factory.Create<int>(this, cur => newCurrent = cur)));

        cut.Find(".wss-pagination-size-select").Change("20");

        Assert.Equal(20, newSize);
        Assert.Equal(2, newCurrent); // floor(20 / 20) + 1 = 2
    }

    [Fact]
    public void Changing_the_page_size_does_not_raise_CurrentChanged_when_the_page_would_not_move()
    {
        int? newCurrent = null;
        var cut = RenderComponent<Pagination>(p => p
            .Add(c => c.Total, 95)
            .Add(c => c.PageSize, 10)
            .Add(c => c.Current, 1) // first item index 0
            .Add(c => c.PageSizeOptions, new[] { 10, 20, 50 })
            .Add(c => c.CurrentChanged, EventCallback.Factory.Create<int>(this, cur => newCurrent = cur)));

        cut.Find(".wss-pagination-size-select").Change("20"); // floor(0/20)+1 = 1, unchanged

        Assert.Null(newCurrent);
    }

    [Fact]
    public void ShowQuickJumper_commits_a_clamped_page_on_enter_and_clears_the_input()
    {
        int? jumped = null;
        var cut = RenderComponent<Pagination>(p => p
            .Add(c => c.Total, 95)
            .Add(c => c.PageSize, 10)
            .Add(c => c.Current, 1)
            .Add(c => c.ShowQuickJumper, true)
            .Add(c => c.CurrentChanged, EventCallback.Factory.Create<int>(this, cur => jumped = cur)));

        var input = cut.Find(".wss-pagination-jumper-input");
        input.Input("99"); // beyond PageCount (10 pages) -> clamps
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(10, jumped);
        Assert.Equal(string.Empty, cut.Find(".wss-pagination-jumper-input").GetAttribute("value"));
    }

    [Fact]
    public void ShowQuickJumper_ignores_non_numeric_text()
    {
        int? jumped = null;
        var cut = RenderComponent<Pagination>(p => p
            .Add(c => c.Total, 95)
            .Add(c => c.PageSize, 10)
            .Add(c => c.Current, 1)
            .Add(c => c.ShowQuickJumper, true)
            .Add(c => c.CurrentChanged, EventCallback.Factory.Create<int>(this, cur => jumped = cur)));

        var input = cut.Find(".wss-pagination-jumper-input");
        input.Input("abc");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Null(jumped);
    }

    [Fact]
    public void Small_adds_the_compact_modifier_class()
    {
        var cut = RenderComponent<Pagination>(p => p
            .Add(c => c.Total, 95)
            .Add(c => c.PageSize, 10)
            .Add(c => c.Current, 1)
            .Add(c => c.Small, true));

        Assert.Contains("wss-pagination-sm", cut.Find(".wss-pagination").ClassList);
    }
}
