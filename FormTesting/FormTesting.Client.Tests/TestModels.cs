using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace FormTesting.Client.Tests;

// Sample models for binding scenarios. Kept in one file so individual test classes stay focused.

public class PersonModel
{
    [Required]
    [DisplayName("Full Name")]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [Required]
    [Range(1, 120)]
    public int? Age { get; set; }

    [Required]
    [Description("The person's birth date")]
    public DateTime? BirthDate { get; set; }

    public bool IsActive { get; set; }

    [Required]
    public Priority? Priority { get; set; }

    public List<string> Tags { get; set; } = [];

    public List<Color> FavoriteColors { get; set; } = [];

    [MinLength(2)]
    [MaxLength(10)]
    public string Username { get; set; } = "";

    [Range(int.MinValue, 100)]
    public int CappedValue { get; set; }

    [Range(0, int.MaxValue)]
    public int FloorValue { get; set; }
}

public enum Priority
{
    Low,
    Medium,
    High,
    Critical
}

public enum Color
{
    Red,

    [EnumDisplayName("Forest Green")]
    Green,

    [Display(Name = "Sky Blue")]
    Blue,

    PaleYellow
}
