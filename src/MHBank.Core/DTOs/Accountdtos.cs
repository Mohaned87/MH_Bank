using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MHBank.Core.DTOs;

/// <summary>
/// بيانات الحساب البنكي
/// </summary>
public record AccountDto
{
    public Guid Id { get; init; }
    public string AccountNumber { get; init; } = string.Empty;
    public string IBAN { get; init; } = string.Empty;
    public string AccountType { get; init; } = string.Empty;
    public decimal Balance { get; init; }
    public string Currency { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

/// <summary>
/// طلب إنشاء حساب جديد
/// </summary>
public record CreateAccountRequest
{
    public string AccountType { get; init; } = string.Empty;
    public string Currency { get; init; } = "USD";
}