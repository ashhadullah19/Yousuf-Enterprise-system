using System.ComponentModel.DataAnnotations;

namespace Yousuf_Enterprise_system.Models
{
    public class BankAccount
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string BankName { get; set; }
            
        [Required, StringLength(200)]
        public string AccountTitle { get; set; }

        [Required, StringLength(50)]
        public string AccountNumber { get; set; }

        [StringLength(1000)]
        public string? Remarks { get; set; }
    }
}
