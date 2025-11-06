using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MHBank.Core.DTOs;
using MHBank.Core.Entities;
using MHBank.Infrastructure.Data;

namespace MHBank.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountsController> _logger;

    public AccountsController(ApplicationDbContext context, ILogger<AccountsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// الحصول على جميع حسابات المستخدم
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyAccounts()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var accounts = await _context.BankAccounts
                .Where(a => a.UserId == userId.Value)
                .OrderByDescending(a => a.OpenedAt)
                .ToListAsync();

            var result = accounts.Select(a => new AccountDto
            {
                Id = a.Id,
                AccountNumber = a.AccountNumber,
                IBAN = a.IBAN,
                AccountType = a.AccountType.ToString(),
                Balance = a.Balance,
                Currency = a.Currency,
                IsActive = a.IsActive
            });

            return Ok(new
            {
                Success = true,
                TotalAccounts = accounts.Count,
                TotalBalance = accounts.Sum(a => a.Balance),
                Accounts = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في جلب الحسابات");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// الحصول على حساب معين
    /// </summary>
    /// 
    [HttpGet("{id}")]
    public async Task<IActionResult> GetAccount(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var account = await _context.BankAccounts
                .Include(a => a.Transactions)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId.Value);

            if (account == null)
                return NotFound(new { Message = "الحساب غير موجود" });

            return Ok(new AccountDto
            {
                Id = account.Id,
                AccountNumber = account.AccountNumber,
                IBAN = account.IBAN,
                AccountType = account.AccountType.ToString(),
                Balance = account.Balance,
                Currency = account.Currency,
                IsActive = account.IsActive
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في جلب الحساب");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }
    /// <summary>
    /// إنشاء حساب بنكي جديد
    /// </summary>
    /// 
    [HttpPost]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            // التحقق من نوع الحساب
            if (!Enum.TryParse<AccountType>(request.AccountType, out var accountType))
            {
                return BadRequest(new { Message = "نوع الحساب غير صحيح. استخدم: Checking أو Savings" });
            }

            // إنشاء رقم حساب فريد (مبسط)
            var accountNumber = GenerateAccountNumber();
            var iban = GenerateIBAN(accountNumber);

            var account = new BankAccount
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                AccountNumber = accountNumber,
                IBAN = iban,
                AccountType = accountType,
                Currency = request.Currency,
                Balance = 0,
                IsActive = true,
                OpenedAt = DateTime.UtcNow
            };

            _context.BankAccounts.Add(account);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تم إنشاء حساب جديد: {AccountNumber}", accountNumber);

            return Ok(new
            {
                Success = true,
                Message = "تم إنشاء الحساب بنجاح",
                Account = new AccountDto
                {
                    Id = account.Id,
                    AccountNumber = account.AccountNumber,
                    IBAN = account.IBAN,
                    AccountType = account.AccountType.ToString(),
                    Balance = account.Balance,
                    Currency = account.Currency,
                    IsActive = account.IsActive
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في إنشاء الحساب");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }
    // ═══════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return string.IsNullOrEmpty(userIdClaim) ? null : Guid.Parse(userIdClaim);
    }

    private string GenerateAccountNumber()
    {
        // رقم حساب من 10 أرقام (مبسط)
        var random = new Random();
        return $"10{random.Next(10000000, 99999999)}";
    }

    private string GenerateIBAN(string accountNumber)
    {
        // IBAN مبسط (عادة يكون أطول وأكثر تعقيداً)
        return $"IQ98MHBK{accountNumber}";
    }
}