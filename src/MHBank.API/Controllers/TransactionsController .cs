using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MHBank.Core.Entities;
using MHBank.Infrastructure.Data;
using MHBank.Infrastructure.Services;

namespace MHBank.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly TransactionLimitsService _limitsService;
    private readonly NotificationService _notificationService;
    private readonly FraudDetectionService _fraudDetection;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(
        ApplicationDbContext context,
        TransactionLimitsService limitsService,
        NotificationService notificationService,
        FraudDetectionService fraudDetection,
        ILogger<TransactionsController> logger)
    {
        _context = context;
        _limitsService = limitsService;
        _notificationService = notificationService;
        _fraudDetection = fraudDetection;
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

            // التحقق من الحدود باستخدام الخدمة
            var (isAllowed, errorMessage) = await _limitsService.CanPerformTransactionAsync(fromAccount, request.Amount);
            if (!isAllowed)
                return BadRequest(new { Message = errorMessage });

            // فحص الاحتيال
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var fraudCheck = await _fraudDetection.CheckTransactionAsync(fromAccount, request.Amount, ipAddress);

            if (fraudCheck.IsBlocked)
            {
                _logger.LogWarning("🚨 تم حظر معاملة مشبوهة: {AccountNumber} - {Amount}",
                    fromAccount.AccountNumber, request.Amount);

                return BadRequest(new
                {
                    Message = "تم حظر المعاملة لأسباب أمنية. يرجى الاتصال بالدعم.",
                    RiskLevel = fraudCheck.RiskLevel.ToString(),
                    Reasons = fraudCheck.SuspiciousReasons
                });
            }

            if (fraudCheck.RequiresVerification)
            {
                _logger.LogWarning("⚠️ معاملة تحتاج تحقق: {AccountNumber} - {Amount}",
                    fromAccount.AccountNumber, request.Amount);

                // في الواقع، هنا نطلب OTP أو تأكيد إضافي
                // لكن للبساطة، نكمل المعاملة مع تحذير
            }

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

            // تحديث تاريخ آخر معاملة
            fromAccount.LastTransactionAt = DateTime.UtcNow;

            // تسجيل المعاملة في نظام الحدود
            await _limitsService.RecordTransactionAsync(fromAccount, request.Amount);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // إرسال إشعارات
            try
            {
                // إشعار للمُرسل
                await _notificationService.NotifyTransactionAsync(fromAccount.UserId, trans, false);

                // إشعار للمستقبل
                await _notificationService.NotifyTransactionAsync(toAccount.UserId, trans, true);
            }
            catch (Exception notifEx)
            {
                _logger.LogWarning(notifEx, "⚠️ فشل إرسال الإشعارات");
            }

            _logger.LogInformation("✅ تحويل ناجح: {Amount} من {From} إلى {To}",
                request.Amount, fromAccount.AccountNumber, toAccount.AccountNumber);

            return Ok(new
            {
                Success = true,
                Message = "تم التحويل بنجاح",
                TransactionId = trans.Id,  // ← أضف هذا
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

    /// <summary>
    /// تحديث حدود حساب معين (Admin أو Owner)
    /// </summary>
    [HttpPatch("limits/{accountId}")]
    public async Task<IActionResult> UpdateTransactionLimits(
        Guid accountId,
        [FromBody] UpdateLimitsRequest request)
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

            // تحديث الحدود
            if (request.DailyLimit.HasValue && request.DailyLimit.Value > 0)
            {
                account.DailyTransferLimit = request.DailyLimit.Value;
            }

            if (request.MonthlyLimit.HasValue && request.MonthlyLimit.Value > 0)
            {
                account.MonthlyTransferLimit = request.MonthlyLimit.Value;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تم تحديث حدود الحساب {AccountNumber}", account.AccountNumber);

            return Ok(new
            {
                Success = true,
                Message = "تم تحديث الحدود بنجاح",
                NewLimits = new
                {
                    account.DailyTransferLimit,
                    account.MonthlyTransferLimit
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تحديث الحدود");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// الحصول على ملخص حدود المعاملات للحساب
    /// </summary>
    [HttpGet("limits/{accountId}")]
    public async Task<IActionResult> GetTransactionLimits(Guid accountId)
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

            var summary = await _limitsService.GetLimitsSummaryAsync(accountId);

            return Ok(new
            {
                Success = true,
                Limits = new
                {
                    Daily = new
                    {
                        summary.DailyLimit,
                        summary.DailyUsed,
                        summary.DailyRemaining,
                        Percentage = summary.DailyLimit > 0
                            ? (summary.DailyUsed / summary.DailyLimit * 100)
                            : 0
                    },
                    Monthly = new
                    {
                        summary.MonthlyLimit,
                        summary.MonthlyUsed,
                        summary.MonthlyRemaining,
                        Percentage = summary.MonthlyLimit > 0
                            ? (summary.MonthlyUsed / summary.MonthlyLimit * 100)
                            : 0
                    },
                    summary.SingleTransactionMax
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في جلب الحدود");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// تحليل سلوك الحساب (كشف الأنماط المشبوهة)
    /// </summary>
    [HttpGet("fraud-analysis/{accountId}")]
    public async Task<IActionResult> GetFraudAnalysis(Guid accountId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var account = await _context.BankAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId.Value);

            if (account == null)
                return NotFound(new { Message = "الحساب غير موجود" });

            var analysis = await _fraudDetection.AnalyzeAccountBehaviorAsync(accountId);

            return Ok(new
            {
                Success = true,
                AccountNumber = account.AccountNumber,
                Analysis = new
                {
                    analysis.TotalTransactions,
                    AverageAmount = $"{analysis.AverageAmount:N2}",
                    MaxAmount = $"{analysis.MaxAmount:N2}",
                    MostActiveHour = $"{analysis.MostActiveHour}:00",
                    analysis.SuspiciousTransactionsCount,
                    Status = analysis.SuspiciousTransactionsCount > 0 ? "⚠️ نشاط مشبوه" : "✅ آمن"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تحليل الحساب");
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

    /// <summary>
    /// الحصول على جميع معاملات المستخدم (لكل حساباته)
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAllUserTransactions()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            _logger.LogInformation("Getting all transactions for user: {UserId}", userId);

            // جلب جميع حسابات المستخدم
            var userAccountIds = await _context.BankAccounts
                .Where(a => a.UserId == userId.Value)
                .Select(a => a.Id)
                .ToListAsync();

            // جلب المعاملات
            var transactions = await _context.Transactions
                .Include(t => t.Account)
                .Where(t => userAccountIds.Contains(t.AccountId))
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    t.Id,
                    t.ReferenceNumber,
                    Type = t.Type.ToString(),
                    Status = t.Status.ToString(),
                    t.Amount,
                    t.Currency,
                    t.Description,
                    t.CreatedAt,
                    t.CompletedAt,
                    AccountNumber = t.Account.AccountNumber
                })
                .ToListAsync();

            _logger.LogInformation("Found {Count} transactions", transactions.Count);

            return Ok(new
            {
                Success = true,
                TotalTransactions = transactions.Count,
                Transactions = transactions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user transactions");
            return StatusCode(500, new { Success = false, Message = "خطأ" });
        }
    }

    /// <summary>
    /// الحصول على آخر المعاملات (لكل حسابات المستخدم)
    /// </summary>
    [HttpGet("recent")]
    [Authorize]
    public async Task<IActionResult> GetRecentTransactions()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            // جلب جميع حسابات المستخدم
            var userAccountIds = await _context.BankAccounts
                .Where(a => a.UserId == userId.Value)
                .Select(a => a.Id)
                .ToListAsync();

            // جلب آخر 5 معاملات
            var transactions = await _context.Transactions
                .Include(t => t.Account)
                .Where(t => userAccountIds.Contains(t.AccountId))
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .Select(t => new
                {
                    t.Id,
                    t.ReferenceNumber,
                    Type = t.Type.ToString(),
                    Status = t.Status.ToString(),
                    t.Amount,
                    t.Currency,
                    t.Description,
                    t.CreatedAt,
                    AccountNumber = t.Account.AccountNumber
                })
                .ToListAsync();

            return Ok(new
            {
                Success = true,
                TotalTransactions = transactions.Count,
                Transactions = transactions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent transactions");
            return StatusCode(500, new { Success = false, Message = "خطأ" });
        }
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

public record UpdateLimitsRequest
{
    public decimal? DailyLimit { get; init; }
    public decimal? MonthlyLimit { get; init; }
}