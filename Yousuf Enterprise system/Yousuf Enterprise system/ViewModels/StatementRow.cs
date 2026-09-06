using Yousuf_Enterprise_system.Models;

namespace Yousuf_Enterprise_system.ViewModels
{
    public class StatementRow
    {
        public BankTransaction Transaction { get; set; } = null!;
        public decimal RunningBalance { get; set; }
    }
}
