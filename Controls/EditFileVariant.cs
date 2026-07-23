namespace Controls;

/// <summary>
/// Visual style for <see cref="EditFile"/>'s picker, mirroring Ant Design's Upload list types.
/// </summary>
public enum EditFileVariant
{
    /// <summary> The dashed drag-and-drop card (Ant Design's <c>Upload.Dragger</c>). Default. </summary>
    Dropzone,

    /// <summary>
    /// A normal-sized "Select Files" button, no dashed card and no drag-and-drop affordance (Ant
    /// Design's plain <c>Upload</c>). Same validation/caps/messages as <see cref="Dropzone"/>, and the
    /// same invisible-<c>InputFile</c>-overlay technique, so keyboard/focus/click behavior match.
    /// </summary>
    Button
}
