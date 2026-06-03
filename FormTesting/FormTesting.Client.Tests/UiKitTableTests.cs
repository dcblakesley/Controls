using Microsoft.AspNetCore.Components;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit smoke tests for the ported Table + PropertyColumn (selection uses raw checkboxes;
/// the Checkbox control is intentionally not part of this library).
/// </summary>
public class UiKitTableTests : TestContext
{
    record Person(string Name, int Age);

    static List<Person> Sample() => [new("Alice", 30), new("Bob", 25)];

    [Fact]
    public void Table_renders_headers_and_rows_from_property_columns()
    {
        var cut = RenderComponent<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name))
            .AddChildContent<PropertyColumn<Person, int>>(cp => cp
                .Add(c => c.Title, "Age")
                .Add(c => c.Property, x => x.Age)));

        Assert.Equal(2, cut.FindAll("thead .wss-table-cell").Count);
        Assert.Equal(2, cut.FindAll("tbody .wss-table-row").Count);
        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("30", cut.Markup);
    }

    [Fact]
    public void Table_empty_data_renders_placeholder()
    {
        var cut = RenderComponent<Table<Person>>(p => p
            .Add(t => t.DataSource, new List<Person>())
            .Add(t => t.EmptyText, "Nothing here")
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.Contains("Nothing here", cut.Find(".wss-table-placeholder").TextContent);
    }

    [Fact]
    public void Table_selectable_renders_checkboxes_and_raises_change()
    {
        List<Person>? selected = null;
        var cut = RenderComponent<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.Selectable, true)
            .Add(t => t.SelectedItemsChanged,
                EventCallback.Factory.Create<IEnumerable<Person>>(this, s => selected = s.ToList()))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        var checkboxes = cut.FindAll("tbody input.wss-table-checkbox");
        Assert.Equal(2, checkboxes.Count);

        checkboxes[0].Change(true);
        Assert.NotNull(selected);
        Assert.Single(selected!);
    }
}
