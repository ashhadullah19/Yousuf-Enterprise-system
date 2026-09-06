using System.ComponentModel.DataAnnotations;
using Yousuf_Enterprise_system.Models;

namespace Yousuf_Enterprise_system.ViewModels
{

    public class UserFormViewModel
    {
        public string? Id { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = AppRoles.Accountant;

        // Only required on Create — controller manually adds the error there.
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
