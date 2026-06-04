using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit smoke tests for the ported composite UI-kit controls (Modal, Drawer, Popconfirm).
/// Modal/Popconfirm use inlined native footer buttons (the Button control is intentionally
/// not part of this library).
/// </summary>
public class UiKitDialogControlsTests : TestContext
{
    [Fact]
    public void Modal_hidden_renders_nothing()
    {
        var cut = RenderComponent<Modal>(p => p.Add(m => m.Visible, false).Add(m => m.Title, "T"));
        Assert.Empty(cut.FindAll(".wss-modal"));
    }

    [Fact]
    public void Modal_visible_renders_title_body_and_two_footer_buttons()
    {
        var cut = RenderComponent<Modal>(p => p
            .Add(m => m.Visible, true)
            .Add(m => m.Title, "Confirm")
            .AddChildContent("<p class=\"body\">Are you sure?</p>"));

        Assert.Contains("Confirm", cut.Find(".wss-modal-title").TextContent);
        Assert.NotNull(cut.Find("p.body"));
        Assert.Equal(2, cut.FindAll(".wss-modal-footer .wss-dialog-btn").Count);
    }

    [Fact]
    public void Modal_ok_and_cancel_invoke_callbacks()
    {
        var okd = false;
        var canceled = false;
        var cut = RenderComponent<Modal>(p => p
            .Add(m => m.Visible, true)
            .Add(m => m.OnOk, EventCallback.Factory.Create(this, () => okd = true))
            .Add(m => m.OnCancel, EventCallback.Factory.Create(this, () => canceled = true)));

        cut.FindAll(".wss-modal-footer .wss-dialog-btn")[1].Click(); // OK (primary)
        Assert.True(okd);
        cut.FindAll(".wss-modal-footer .wss-dialog-btn")[0].Click(); // Cancel
        Assert.True(canceled);
    }

    [Fact]
    public void Drawer_visible_renders_placement_class_and_title()
    {
        var cut = RenderComponent<Drawer>(p => p
            .Add(d => d.Visible, true)
            .Add(d => d.Placement, DrawerPlacement.Left)
            .Add(d => d.Title, "Side panel"));

        Assert.NotNull(cut.Find(".wss-drawer-left"));
        Assert.Contains("Side panel", cut.Find(".wss-drawer-title").TextContent);
    }

    [Fact]
    public void Drawer_close_button_invokes_OnClose()
    {
        var closed = false;
        var cut = RenderComponent<Drawer>(p => p
            .Add(d => d.Visible, true)
            .Add(d => d.OnClose, EventCallback.Factory.Create(this, () => closed = true)));

        cut.Find(".wss-drawer-close").Click();
        Assert.True(closed);
    }

    [Fact]
    public void Popconfirm_opens_on_trigger_then_confirm_invokes_and_closes()
    {
        var confirmed = false;
        var cut = RenderComponent<Popconfirm>(p => p
            .Add(pc => pc.Title, "Delete?")
            .Add(pc => pc.OnConfirm, EventCallback.Factory.Create(this, () => confirmed = true))
            .AddChildContent("<button>del</button>"));

        Assert.Empty(cut.FindAll(".wss-popconfirm"));
        cut.Find(".wss-popconfirm-trigger").Click();
        Assert.Contains("Delete?", cut.Find(".wss-popconfirm-title").TextContent);

        cut.FindAll(".wss-popconfirm-buttons .wss-dialog-btn")[1].Click(); // OK
        Assert.True(confirmed);
        Assert.Empty(cut.FindAll(".wss-popconfirm")); // closes after confirm
    }

    [Fact]
    public void Popconfirm_dialog_is_labelled_by_its_title()
    {
        var cut = RenderComponent<Popconfirm>(p => p
            .Add(pc => pc.Title, "Delete?")
            .AddChildContent("<button>del</button>"));

        cut.Find(".wss-popconfirm-trigger").Click();
        var dialog = cut.Find("[role=dialog]");
        var labelledBy = dialog.GetAttribute("aria-labelledby");
        Assert.False(string.IsNullOrEmpty(labelledBy));
        Assert.Equal(labelledBy, cut.Find(".wss-popconfirm-title").Id);
    }

    [Fact]
    public void Popover_dialog_is_labelled_by_its_title_when_present()
    {
        var cut = RenderComponent<Popover>(p => p
            .Add(pv => pv.Title, "Info")
            .Add(pv => pv.Content, (RenderFragment)(b => b.AddContent(0, "details")))
            .AddChildContent("<span>?</span>"));

        cut.Find(".wss-popover-trigger").Click();
        var dialog = cut.Find("[role=dialog]");
        var labelledBy = dialog.GetAttribute("aria-labelledby");
        Assert.False(string.IsNullOrEmpty(labelledBy));
        Assert.Equal(labelledBy, cut.Find(".wss-popover-title").Id);
    }

    [Fact]
    public void Popconfirm_escape_inside_the_panel_cancels_and_closes()
    {
        var canceled = false;
        var confirmed = false;
        var cut = RenderComponent<Popconfirm>(p => p
            .Add(pc => pc.Title, "Delete?")
            .Add(pc => pc.OnCancel, EventCallback.Factory.Create(this, () => canceled = true))
            .Add(pc => pc.OnConfirm, EventCallback.Factory.Create(this, () => confirmed = true))
            .AddChildContent("<button>del</button>"));

        cut.Find(".wss-popconfirm-trigger").Click();
        // Escape with focus inside the panel (a sibling of the trigger) must still work.
        cut.Find("[role=dialog]").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll(".wss-popconfirm")); // closed
        Assert.True(canceled);                        // Escape maps to Cancel
        Assert.False(confirmed);
    }

    [Fact]
    public void Popover_escape_inside_the_panel_closes_it()
    {
        var cut = RenderComponent<Popover>(p => p
            .Add(pv => pv.Title, "Info")
            .Add(pv => pv.Content, (RenderFragment)(b => b.AddContent(0, "details")))
            .AddChildContent("<span>?</span>"));

        cut.Find(".wss-popover-trigger").Click();
        Assert.NotEmpty(cut.FindAll("[role=dialog]"));
        cut.Find("[role=dialog]").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll(".wss-popover")); // closed from inside the panel
    }

    [Fact]
    public void Popover_trigger_aria_expanded_is_lowercase_and_toggles_on_open()
    {
        var cut = RenderComponent<Popover>(p => p
            .Add(pv => pv.Title, "Info")
            .Add(pv => pv.Content, (RenderFragment)(b => b.AddContent(0, "details")))
            .AddChildContent("<span>?</span>"));

        // Lowercase ARIA boolean, not Blazor's bool ToString ("False"/"True").
        Assert.Equal("false", cut.Find(".wss-popover-trigger").GetAttribute("aria-expanded"));
        cut.Find(".wss-popover-trigger").Click();
        Assert.Equal("true", cut.Find(".wss-popover-trigger").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Popconfirm_without_a_title_omits_aria_labelledby_and_the_title_element()
    {
        var cut = RenderComponent<Popconfirm>(p => p
            .AddChildContent("<button>del</button>")); // no Title

        cut.Find(".wss-popconfirm-trigger").Click();
        var dialog = cut.Find("[role=dialog]");
        Assert.False(dialog.HasAttribute("aria-labelledby")); // no title element to point at
        Assert.Empty(cut.FindAll(".wss-popconfirm-title"));
    }

    [Fact]
    public void Popconfirm_without_a_title_falls_back_to_aria_label_for_its_accessible_name()
    {
        var cut = RenderComponent<Popconfirm>(p => p
            .Add(pc => pc.AriaLabel, "Confirm deletion")
            .AddChildContent("<button>del</button>")); // no Title

        cut.Find(".wss-popconfirm-trigger").Click();
        var dialog = cut.Find("[role=dialog]");
        Assert.False(dialog.HasAttribute("aria-labelledby")); // still no title element
        Assert.Equal("Confirm deletion", dialog.GetAttribute("aria-label"));
    }

    [Fact]
    public void Popover_without_a_title_falls_back_to_aria_label_for_its_accessible_name()
    {
        var cut = RenderComponent<Popover>(p => p
            .Add(pv => pv.AriaLabel, "More info")
            .Add(pv => pv.Content, (RenderFragment)(b => b.AddContent(0, "details")))
            .AddChildContent("<span>?</span>")); // no Title

        cut.Find(".wss-popover-trigger").Click();
        var dialog = cut.Find("[role=dialog]");
        Assert.False(dialog.HasAttribute("aria-labelledby"));
        Assert.Equal("More info", dialog.GetAttribute("aria-label"));
    }

    [Fact]
    public void Titled_dialog_does_not_also_emit_aria_label()
    {
        var cut = RenderComponent<Popover>(p => p
            .Add(pv => pv.Title, "Info")
            .Add(pv => pv.AriaLabel, "ignored when titled")
            .Add(pv => pv.Content, (RenderFragment)(b => b.AddContent(0, "details")))
            .AddChildContent("<span>?</span>"));

        cut.Find(".wss-popover-trigger").Click();
        var dialog = cut.Find("[role=dialog]");
        // A title wins; we don't double up the accessible name with a redundant aria-label.
        Assert.False(dialog.HasAttribute("aria-label"));
        Assert.False(string.IsNullOrEmpty(dialog.GetAttribute("aria-labelledby")));
    }

    [Fact]
    public void Popconfirm_disabled_trigger_is_aria_disabled_and_out_of_the_tab_order()
    {
        var cut = RenderComponent<Popconfirm>(p => p
            .Add(pc => pc.Title, "Delete?")
            .Add(pc => pc.Disabled, true)
            .AddChildContent("<button>del</button>"));

        var trigger = cut.Find(".wss-popconfirm-trigger");
        Assert.Equal("true", trigger.GetAttribute("aria-disabled"));
        Assert.False(trigger.HasAttribute("tabindex"));      // removed from the tab order
        Assert.False(trigger.HasAttribute("aria-haspopup")); // not announced as opening a dialog
        cut.Find(".wss-popconfirm-trigger").Click();
        Assert.Empty(cut.FindAll(".wss-popconfirm"));        // disabled → never opens
    }
}
