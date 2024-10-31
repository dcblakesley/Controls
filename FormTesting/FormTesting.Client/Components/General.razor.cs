using Controls;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Components;

public partial class General
{
    readonly Person _model = new(); 
    EditForm? _editForm; // Set by @ref during Render
    public FormOptions FormOptions { get; set; } = new();

    public bool IsEditMode { get; set; } = true;

    class Person
    {
        // Width
        public int EditorWidth { get; set; } = 400;
        [MinLength(1)]
        public string? Label { get; set; }

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
    }
}