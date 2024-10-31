namespace Controls.Helpers;

public static class EnumHelpers
{
    public static string GetName(this object value)
    {
        var fi = value.GetType().GetField(value.ToString() ?? string.Empty);
        var attributes = fi?.GetCustomAttributes(typeof(EnumDisplayNameAttribute), false) as EnumDisplayNameAttribute[];
        if (attributes != null && attributes.Any())
        {
            return attributes.First().Value;
        }

        var text = value.ToString();
        if (text != null)
        {
            // split by camel case
            text = string.Concat(text.Select(x => char.IsUpper(x) ? " " + x : x.ToString())).TrimStart(' ');
            return text;
        }

        return "";
    }
}