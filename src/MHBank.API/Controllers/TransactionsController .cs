using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MHBank.Core.Entities;
using MHBank.Infrastructure.Data;

namespace MHBank.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(ApplicationDbContext context, ILogger<TransactionsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// تحويل أموال بين حسابين
    /// </summary>
    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            // جلب الحساب المرسل
            var fromAccount = await _context.BankAccounts
                .FirstOrDefaultAsync(a => a.Id == request.FromAccountId && a.UserId == userId.Value);

            if (fromAccount == null)
                return NotFound(new { Message = "الحساب المرسل غير موجود" });

            if (!fromAccount.IsActive)
                return BadRequest(new { Message = "الحساب المرسل غير نشط" });

            // جلب الحساب المستقبل
            var toAccount = await _context.BankAccounts
                .FirstOrDefaultAsync(a => a.AccountNumber == request.ToAccountNumber);

            if (toAccount == null)
                return NotFound(new { Message = "الحساب المستقبل غير موجود" });

            if (!toAccount.IsActive)
                return BadRequest(new { Message = "الحساب المستقبل غير نشط" });

            // التحقق من الرصيد
            if (fromAccount.Balance < request.Amount)
                return BadRequest(new { Message = $"الرصيد غير كافٍ. الرصيد الحالي: {fromAccount.Balance}" });

            if (request.Amount <= 0)
                return BadRequest(new { Message = "المبلغ يجب أن يكون أكبر من صفر" });

            // إنشاء رقم مرجعي
            var referenceNumber = GenerateReferenceNumber();

            // خصم من الحساب المرسل
            fromAccount.Balance -= request.Amount;

            // إضافة للحساب المستقبل
            toAccount.Balance += request.Amount;

            // إنشاء سجل المعاملة
            var trans = new Core.Entities.Transaction
            {
                Id = Guid.NewGuid(),
                ReferenceNumber = referenceNumber,
                Type = TransactionType.Transfer,
                Status = TransactionStatus.Completed,
                AccountId = fromAccount.Id,
                Amount = request.Amount,
                Currency = fromAccount.Currency,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            _context.Transactions.Add(trans);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("✅ تحويل ناجح: {Amount} من {From} إلى {To}",
                request.Amount, fromAccount.AccountNumber, toAccount.AccountNumber);

            return Ok(new
            {
                Success = true,
                Message = "تم التحويل بنجاح",
                ReferenceNumber = referenceNumber,
                Transaction = new
                {
                    trans.Id,
                    trans.ReferenceNumber,
                    trans.Amount,
                    trans.Currency,
                    trans.Description,
                    From = fromAccount.AccountNumber,
                    To = toAccount.AccountNumber,
                    trans.CreatedAt,
                    NewBalance = fromAccount.Balance
                }
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ خطأ في التحويل");
            return StatusCode(500, new { Message = "حدث خطأ أثناء التحويل" });
        }
    }

    /// <summary>
    /// الحصول على سجل المعاملات لحساب معين
    /// </summary>
    [HttpGet("account/{accountId}")]
    public async Task<IActionResult> GetAccountTransactions(Guid accountId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            // التحقق من ملكية الحساب
            var account = await _context.BankAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId.Value);

            if (account == null)
                return NotFound(new { Message = "الحساب غير موجود" });

            var transactions = await _context.Transactions
                .Where(t => t.AccountId == accountId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(50) // آخر 50 معاملة
                .ToListAsync();

            var result = transactions.Select(t => new
            {
                t.Id,
                t.ReferenceNumber,
                Type = t.Type.ToString(),
                Status = t.Status.ToString(),
                t.Amount,
                t.Currency,
                t.Description,
                t.CreatedAt,
                t.CompletedAt
            });

            return Ok(new
            {
                Success = true,
                TotalTransactions = transactions.Count,
                Transactions = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في جلب المعاملات");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// الحصول على تفاصيل معاملة معينة
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetTransaction(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var trans = await _context.Transactions
                .Include(t => t.Account)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (trans == null)
                return NotFound(new { Message = "المعاملة غير موجودة" });

            // التحقق من الملكية
            if (trans.Account.UserId != userId.Value)
                return Forbid();

            return Ok(new
            {
                trans.Id,
                trans.ReferenceNumber,
                Type = trans.Type.ToString(),
                Status = trans.Status.ToString(),
                trans.Amount,
                trans.Currency,
                trans.Description,
                AccountNumber = trans.Account.AccountNumber,
                trans.CreatedAt,
                trans.CompletedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في جلب المعاملة");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// إيداع مبلغ في الحساب
    /// </summary>
    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit([FromBody] DepositRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var account = await _context.BankAccounts
                .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.UserId == userId.Value);

            if (account == null)
                return NotFound(new { Message = "الحساب غير موجود" });

            if (request.Amount <= 0)
                return BadRequest(new { Message = "المبلغ يجب أن يكون أكبر من صفر" });

            // إضافة المبلغ
            account.Balance += request.Amount;

            // إنشاء سجل المعاملة
            var trans = new Core.Entities.Transaction
            {
                Id = Guid.NewGuid(),
                ReferenceNumber = GenerateReferenceNumber(),
                Type = TransactionType.Deposit,
                Status = TransactionStatus.Completed,
                AccountId = account.Id,
                Amount = request.Amount,
                Currency = account.Currency,
                Description = "إيداع نقدي",
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            _context.Transactions.Add(trans);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ إيداع ناجح: {Amount} في حساب {Account}",
                request.Amount, account.AccountNumber);

            return Ok(new
            {
                Success = true,
                Message = "تم الإيداع بنجاح",
                ReferenceNumber = trans.ReferenceNumber,
                NewBalance = account.Balance
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في الإيداع");
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

    private string GenerateReferenceNumber()
    {
        return $"TRX{DateTime.UtcNow:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";
    }
}

// ═══════════════════════════════════════════════
// Request Models
// ═══════════════════════════════════════════════

public record TransferRequest
{
    public Guid FromAccountId { get; init; }
    public string ToAccountNumber { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Description { get; init; } = string.Empty;
}

public record DepositRequest
{
    public Guid AccountId { get; init; }
    public decimal Amount { get; init; }
}
