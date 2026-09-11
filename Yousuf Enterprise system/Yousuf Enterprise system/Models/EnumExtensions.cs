using System.ComponentModel.DataAnnotations;

namespace Yousuf_Enterprise_system.Models;

public static class EnumExtensions
{
    // Renders an enum's [Display(Name = "...")] text when present (e.g. CommissionHead -> "Commission"),
    // falling back to the raw enum name so unannotated values still render something.
    public static string GetDisplayName(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        var display = member?.GetCustomAttributes(typeof(DisplayAttribute), false)
            .Cast<DisplayAttribute>()
            .FirstOrDefault();
        return display?.Name ?? value.ToString();
    }
}
