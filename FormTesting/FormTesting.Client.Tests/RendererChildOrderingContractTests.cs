using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace FormTesting.Client.Tests;

/// <summary>
/// Pins the Blazor renderer behaviour that <c>Table.MergeCollectedColumns</c> (and the same pattern
/// in <c>Tabs</c>) reasons from. None of it is our code — these tests exist because the merge is
/// only correct if these three facts hold, and a framework upgrade that changed any of them would
/// otherwise show up as a baffling column-ordering bug rather than a failed assertion here.
/// </summary>
public class RendererChildOrderingContractTests : BunitContext
{
    public class Parent : ComponentBase
    {
        [Parameter] public RenderFragment? ChildContent { get; set; }
        [Parameter] public bool Fixed { get; set; } = true;
        public readonly List<string> Log = new();

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<CascadingValue<Parent>>(0);
            builder.AddAttribute(1, "Value", this);
            builder.AddAttribute(2, "IsFixed", Fixed);
            builder.AddAttribute(3, "ChildContent", ChildContent);
            builder.CloseComponent();
        }
    }

    /// <summary>Stands in for a <c>Column</c>: registers from OnParametersSet, renders nothing.</summary>
    public class Child : ComponentBase
    {
        [CascadingParameter] public Parent? Parent { get; set; }
        [Parameter] public string? Title { get; set; }
        /// <summary>Stands in for ChildContent/Property/SortBy/OnFilter/FilterOptions — any parameter
        /// whose type Blazor does not treat as immutable, so it can never prove it unchanged.</summary>
        [Parameter] public Func<int, int>? Template { get; set; }

        protected override void OnParametersSet() => Parent?.Log.Add(Title!);
        protected override void BuildRenderTree(RenderTreeBuilder builder) { }
    }

    static RenderFragment Children(bool showLead, bool withTemplate) => builder =>
    {
        void Child(int seq, string title)
        {
            builder.OpenComponent<Child>(seq);
            builder.AddAttribute(seq + 1, "Title", title);
            if (withTemplate) builder.AddAttribute(seq + 2, "Template", (Func<int, int>)(x => x + 1));
            builder.CloseComponent();
        }

        if (showLead) Child(0, "Lead");
        Child(10, "A");
        Child(20, "B");
        Child(30, "C");
    };

    IRenderedComponent<Parent> RenderParent(bool showLead, bool withTemplate, bool isFixed = true) =>
        Render<Parent>(p => p.Add(x => x.Fixed, isFixed).Add(x => x.ChildContent, Children(showLead, withTemplate)));

    [Fact]
    public void Children_whose_parameters_may_have_changed_are_visited_in_document_order()
    {
        // FACT 1 — the merge's only source of declaration order. The parent's diff walks retained
        // child component frames in declaration order, and a child inserted at the FRONT is visited
        // before the siblings it was inserted ahead of.
        var cut = RenderParent(showLead: false, withTemplate: true);
        Assert.Equal(["A", "B", "C"], cut.Instance.Log);

        cut.Instance.Log.Clear();
        cut.Render(p => p.Add(x => x.ChildContent, Children(showLead: true, withTemplate: true)));
        Assert.Equal(["Lead", "A", "B", "C"], cut.Instance.Log);
    }

    [Fact]
    public void Children_with_only_immutable_parameters_are_skipped_entirely()
    {
        // FACT 2 — where stragglers come from. A child whose every parameter is one Blazor treats as
        // immutable (here a literal string) gets no SetParametersAsync at all once it has rendered
        // once with those values, so it cannot re-register and cannot report its position.
        var cut = RenderParent(showLead: false, withTemplate: false);
        Assert.Equal(["A", "B", "C"], cut.Instance.Log);

        cut.Instance.Log.Clear();
        cut.Render(p => p.Add(x => x.ChildContent, Children(showLead: true, withTemplate: false)));
        Assert.Equal(["Lead"], cut.Instance.Log);
    }

    [Fact]
    public void A_non_fixed_cascade_re_invokes_everyone_but_in_subscription_order()
    {
        // FACT 3 — why Table keeps IsFixed="true". Dropping it does make every child re-run each
        // pass (CascadingValue notifies its subscribers because a component instance is not a type
        // it can prove unchanged), which looks like the fix — but the notification fires before the
        // ChildContent re-renders and walks the subscriber list in FIRST-SUBSCRIPTION order, so the
        // newcomer lands last no matter where it was declared, and it never self-corrects. Using
        // that order would silently reshuffle the table on an unrelated re-render.
        var cut = RenderParent(showLead: false, withTemplate: false, isFixed: false);
        Assert.Equal(["A", "B", "C"], cut.Instance.Log);

        cut.Instance.Log.Clear();
        cut.Render(p => p.Add(x => x.Fixed, false).Add(x => x.ChildContent, Children(true, false)));
        Assert.Equal(["A", "B", "C", "Lead"], cut.Instance.Log);

        // Still wrong on a later, structurally-identical pass — subscription order is permanent.
        cut.Instance.Log.Clear();
        cut.Render(p => p.Add(x => x.Fixed, false).Add(x => x.ChildContent, Children(true, false)));
        Assert.Equal(["A", "B", "C", "Lead"], cut.Instance.Log);
    }
}
