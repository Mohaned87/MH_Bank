using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MHBank.Mobile.Models;

public class BankAccount
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string? IBAN { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "IQD";
    public bool IsActive { get; set; }
    public DateTime OpenedAt { get; set; }
    public decimal DailyTransferLimit { get; set; }
    public decimal MonthlyTransferLimit { get; set; }
    public decimal CurrentDailyTransferred { get; set; }
    public decimal CurrentMonthlyTransferred { get; set; }
}
