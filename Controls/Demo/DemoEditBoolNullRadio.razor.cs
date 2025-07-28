namespace Controls.Demo;

public partial class DemoEditBoolNullRadio
{

    EditForm _form;
    protected override void OnAfterRender(bool firstRender) => _form.EditContext!.Validate();
    class DemoModel
    {
        public bool? BasicOption { get; set; }
        public bool? HorizontalLayout { get; set; }
        public bool? VerticalLayout { get; set; }
        public bool? CustomText { get; set; }
        
        [Required]
        public bool? RequiredField { get; set; }
        
        public bool? DisabledField { get; set; } = true;
        public bool? ReadOnlyField { get; set; } = true;
        public bool? NoNullOption { get; set; } = false;
        public bool? HiddenWhenNull { get; set; }
        public bool? HiddenWhenNullOrDefault { get; set; }
    }

    readonly DemoModel _model = new();
}