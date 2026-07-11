using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Regression coverage for the centralized <c>ShouldShowComponent</c> / <c>IsValueDefault</c>
/// pulled into <c>EditControlBase</c>. Each Theory walks one control through the matrix of
/// <see cref="HidingMode"/> × value-state × edit-mode to lock in the behavior the per-control
/// implementations used to provide.
/// </summary>
public class HidingModeTests : TestContext
{
    static RenderFragment WithForm<TModel>(TModel model, FormOptions? formOptions, RenderFragment inner)
        where TModel : class => builder =>
    {
        if (formOptions is not null)
        {
            builder.OpenComponent<CascadingValue<FormOptions>>(0);
            builder.AddAttribute(1, "Value", formOptions);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<EditForm>(0);
                b.AddAttribute(1, "Model", model);
                b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        }
        else
        {
            builder.OpenComponent<EditForm>(0);
            builder.AddAttribute(1, "Model", model);
            builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
            builder.CloseComponent();
        }
    };

    static bool IsRendered(IRenderedFragment cut) => cut.FindAll(".edit-control-wrapper").Count > 0;

    // ── EditString ────────────────────────────────────────────────────────────────────────────

    [Theory]
    // HidingMode.None: never hide.
    [InlineData(HidingMode.None, "x", true, true)]
    [InlineData(HidingMode.None, "", true, true)]
    [InlineData(HidingMode.None, null, true, true)]
    [InlineData(HidingMode.None, "x", false, true)]
    [InlineData(HidingMode.None, null, false, true)]
    // HidingMode.WhenNull: hide null in both modes.
    [InlineData(HidingMode.WhenNull, "x", true, true)]
    [InlineData(HidingMode.WhenNull, "", true, true)]
    [InlineData(HidingMode.WhenNull, null, true, false)]
    [InlineData(HidingMode.WhenNull, null, false, false)]
    // HidingMode.WhenNullOrDefault: hide null and "" in both modes.
    [InlineData(HidingMode.WhenNullOrDefault, "x", true, true)]
    [InlineData(HidingMode.WhenNullOrDefault, "", true, false)]
    [InlineData(HidingMode.WhenNullOrDefault, null, true, false)]
    [InlineData(HidingMode.WhenNullOrDefault, "x", false, true)]
    [InlineData(HidingMode.WhenNullOrDefault, "", false, false)]
    // HidingMode.WhenReadOnlyAndNull: hide null only in read-only mode.
    [InlineData(HidingMode.WhenReadOnlyAndNull, null, true, true)]
    [InlineData(HidingMode.WhenReadOnlyAndNull, null, false, false)]
    [InlineData(HidingMode.WhenReadOnlyAndNull, "", false, true)] // "" is not null → show in read-only
    [InlineData(HidingMode.WhenReadOnlyAndNull, "x", false, true)]
    // HidingMode.WhenReadOnlyAndNullOrDefault: hide null/"" only in read-only mode.
    [InlineData(HidingMode.WhenReadOnlyAndNullOrDefault, null, true, true)]
    [InlineData(HidingMode.WhenReadOnlyAndNullOrDefault, "", true, true)]
    [InlineData(HidingMode.WhenReadOnlyAndNullOrDefault, null, false, false)]
    [InlineData(HidingMode.WhenReadOnlyAndNullOrDefault, "", false, false)]
    [InlineData(HidingMode.WhenReadOnlyAndNullOrDefault, "x", false, true)]
    public void EditString_hiding_matrix(HidingMode mode, string? value, bool isEditMode, bool expectedVisible)
    {
        var model = new StringModel { Text = value };
        Expression<Func<string>> field = () => model.Text!;
        var cut = Render(WithForm(model, null, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Hiding", mode);
            b.AddAttribute(5, "IsEditMode", isEditMode);
            b.CloseComponent();
        }));

        Assert.Equal(expectedVisible, IsRendered(cut));
    }

    // ── EditNumber<int?> ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(HidingMode.None, 0, true, true)]
    [InlineData(HidingMode.WhenNull, null, true, false)]
    [InlineData(HidingMode.WhenNull, 0, true, true)] // 0 is not null
    [InlineData(HidingMode.WhenNullOrDefault, 0, true, false)] // numeric zero counts as default
    [InlineData(HidingMode.WhenNullOrDefault, null, true, false)]
    [InlineData(HidingMode.WhenNullOrDefault, 5, true, true)]
    [InlineData(HidingMode.WhenReadOnlyAndNullOrDefault, 0, true, true)] // edit mode: show
    [InlineData(HidingMode.WhenReadOnlyAndNullOrDefault, 0, false, false)] // read-only + default: hide
    [InlineData(HidingMode.WhenReadOnlyAndNullOrDefault, 5, false, true)]
    [InlineData(HidingMode.WhenReadOnlyAndNull, null, false, false)]
    [InlineData(HidingMode.WhenReadOnlyAndNull, 0, false, true)] // 0 is not null
    public void EditNumber_hiding_matrix(HidingMode mode, int? value, bool isEditMode, bool expectedVisible)
    {
        var model = new IntModel { N = value };
        Expression<Func<int?>> field = () => model.N;
        var cut = Render(WithForm(model, null, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.N);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Hiding", mode);
            b.AddAttribute(5, "IsEditMode", isEditMode);
            b.CloseComponent();
        }));

        Assert.Equal(expectedVisible, IsRendered(cut));
    }

    // ── EditDate<DateTime?> — the special-case override for default(DateTime) ────────────────

    [Fact]
    public void EditDate_treats_default_DateTime_inside_nullable_as_default()
    {
        var model = new DateModel { D = default(DateTime) };
        Expression<Func<DateTime?>> field = () => model.D;
        var cut = Render(WithForm(model, null, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.D);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Hiding", HidingMode.WhenNullOrDefault);
            b.CloseComponent();
        }));

        // The override unwraps the nullable so default(DateTime) counts as default — should be hidden.
        Assert.False(IsRendered(cut));
    }

    [Fact]
    public void EditDate_shows_real_date_under_WhenNullOrDefault()
    {
        var model = new DateModel { D = new DateTime(2026, 1, 1) };
        Expression<Func<DateTime?>> field = () => model.D;
        var cut = Render(WithForm(model, null, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.D);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Hiding", HidingMode.WhenNullOrDefault);
            b.CloseComponent();
        }));

        Assert.True(IsRendered(cut));
    }

    // ── FormOptions.IsEditMode now respected by every control (bug-fix coverage) ─────────────

    [Fact]
    public void Form_wide_IsEditMode_false_hides_EditString_under_WhenReadOnlyAndNull()
    {
        // Pre-refactor, controls that used bare IsEditMode in their hiding logic missed this case.
        // After centralization on ShowEditor, FormOptions.IsEditMode=false correctly counts as read-only.
        var model = new StringModel { Text = null };
        Expression<Func<string>> field = () => model.Text!;
        var formOptions = new FormOptions { IsEditMode = false };
        var cut = Render(WithForm(model, formOptions, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Hiding", HidingMode.WhenReadOnlyAndNull);
            b.AddAttribute(5, "IsEditMode", true); // per-control says edit
            b.CloseComponent();
        }));

        // FormOptions.IsEditMode=false overrides per-control IsEditMode=true → read-only → null → hide.
        Assert.False(IsRendered(cut));
    }

    [Fact]
    public void Form_wide_IsEditMode_true_with_per_control_IsEditMode_false_treats_as_read_only()
    {
        // Either gate being false makes ShowEditor false — verifies the AND semantics of ShowEditor.
        var model = new StringModel { Text = null };
        Expression<Func<string>> field = () => model.Text!;
        var formOptions = new FormOptions { IsEditMode = true };
        var cut = Render(WithForm(model, formOptions, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Hiding", HidingMode.WhenReadOnlyAndNull);
            b.AddAttribute(5, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.False(IsRendered(cut));
    }

    // ── Test models ──────────────────────────────────────────────────────────────────────────

    class StringModel { public string? Text { get; set; } }
    class IntModel { public int? N { get; set; } }
    class DateModel { public DateTime? D { get; set; } }
}
