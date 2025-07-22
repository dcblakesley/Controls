using Controls;
using Controls.Helpers;
using Microsoft.AspNetCore.Components.Forms;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace FormTesting.Client.Components;

public partial class General
{
    public AnimalType SelectedAnimalType { get; set; } = AnimalType.Dog;

    readonly Person _model = new(); 
    EditForm? _editForm; // Set by @ref during Render
    public FormOptions FormOptions { get; set; } = new();
    public FormGroupOptions FormGroupOptions { get; set; } = new() { Name = "Blah" };
    public FormGroupOptions Address2 { get; set; } = new() { Name = "Address2" };

    public bool IsEditMode { get; set; } = true;

    protected override void OnAfterRender(bool firstRender) => _editForm!.EditContext!.Validate();

    public List<string> _options = ["Green", "Blue", "Red", "Yellow", "Orange", "Purple", "Black", "White"];
    public string _selectedOption = "Green";

    class Person
    {

        public string TestMethod() => "Hello";

        public decimal Age { get; set; } = 18.51111m;


        // Width
        public int EditorWidth { get; set; } = 400;

        [Required, MinLength(1), Description("My amazing description")]
        public string? BasicExample { get; set; }

        // Labels
        public string Name { get; set; } = "Green";
        [DisplayName("MiddleName modified with [DisplayNameAttribute]")]
        public string MiddleName { get; set; } = "";
        [DisplayName("<span style='color: cyan'>Last </span><span style='color: lime'>Name</span><span style='color: magenta'> with embedded html</span>")]
        public string LastName { get; set; } = "";

        // Descriptions
        [Description("This is a description for Dog")]
        public string Dog { get; set; } = "";
        [Description("<div style='color: lime; font-weight: bold'>Cats don't like dogs or water</div>")]
        public string Cat { get; set; } = "";

        // Validation
        [Required]
        public string Horse { get; set; } = "";
        [Required(ErrorMessage = "<div style='color: lime; font-weight: bold'>Required: Zebras don't like<ul><li style=\"color: red\">Lions</li><li style=\"color: cyan\">Newspapers</li></ul></div>")]
        public string Zebra { get; set; } = "";
        [MinLength(3)] public string Lion { get; set; } = "a";
        [MaxLength(5)] public string Tiger { get; set; } = "abcdefghi";

        public Address HomeAddress { get; set; } = new();
        public Address WorkAddress { get; set; } = new();
    }
    public enum AnimalType
    {
        Cat = 0,

        [EnumDisplayName("Puppy Dog")]
        Dog = 1,

        [EnumDisplayName("Tweety Bird")]
        Bird = 2,

        [EnumDisplayName("Gold Fish")]
        Fish = 3
    }
}

public class Address
{
    [DisplayName("Address")]
    [Required]
    public string Line1 { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
}