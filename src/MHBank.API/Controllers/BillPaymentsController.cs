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
public class BillPaymentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly NotificationService _notificationService;
    private readonly ILogger<BillPaymentsController> _logger;

    public BillPaymentsController(
        ApplicationDbContext context,
        NotificationService notificationService,
        ILogger<BillPaymentsController> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// دفع فاتورة
    /// </summary>
    [HttpPost("pay")]
    public async Task<IActionResult> PayBill([FromBody] PayBillRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            // التحقق من الحساب
            var account = await _context.BankAccounts
                .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.UserId == userId.Value);

            if (account == null)
                return NotFound(new { Message = "الحساب غير موجود" });

            if (!account.IsActive)
                return BadRequest(new { Message = "الحساب غير نشط" });

            // التحقق من الرصيد
            if (account.Balance < request.Amount)
            {
                return BadRequest(new
                {
                    Message = $"الرصيد غير كافٍ. الرصيد الحالي: {account.Balance:N2}"
                });
            }

            if (request.Amount <= 0)
                return BadRequest(new { Message = "المبلغ يجب أن يكون أكبر من صفر" });

            // إنشاء رقم مرجعي
            var referenceNumber = GenerateReferenceNumber();

            // خصم المبلغ من الحساب
            account.Balance -= request.Amount;

            // إنشاء سجل الدفع
            var billPayment = new BillPayment
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                BillType = request.BillType,
                BillNumber = request.BillNumber,
                ServiceProvider = request.ServiceProvider,
                Amount = request.Amount,
                Currency = account.Currency,
                ReferenceNumber = referenceNumber,
                Status = BillPaymentStatus.Completed,
                CreatedAt = DateTime.UtcNow,
                PaidAt = DateTime.UtcNow,
                Notes = request.Notes
            };

            _context.BillPayments.Add(billPayment);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // إرسال إشعار
            try
            {
                await _notificationService.CreateNotificationAsync(
                    userId.Value,
                    "تم دفع فاتورة",
                    $"تم دفع فاتورة {GetBillTypeName(request.BillType)} بمبلغ {request.Amount:N2}",
                    NotificationType.Transaction,
                    new { BillPaymentId = billPayment.Id }
                );
            }
            catch (Exception notifEx)
            {
                _logger.LogWarning(notifEx, "⚠️ فشل إرسال الإشعار");
            }

            _logger.LogInformation("✅ تم دفع فاتورة: {Type} - {Amount}",
                request.BillType, request.Amount);

            return Ok(new
            {
                Success = true,
                Message = "تم دفع الفاتورة بنجاح",
                Payment = new
                {
                    billPayment.Id,
                    billPayment.ReferenceNumber,
                    BillType = billPayment.BillType.ToString(),
                    billPayment.Amount,
                    billPayment.ServiceProvider,
                    billPayment.PaidAt,
                    NewBalance = account.Balance
                }
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ خطأ في دفع الفاتورة");
            return StatusCode(500, new { Message = "حدث خطأ أثناء دفع الفاتورة" });
        }
    }

    /// <summary>
    /// الحصول على سجل الفواتير المدفوعة
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetPaymentHistory([FromQuery] Guid? accountId = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var query = _context.BillPayments
                .Include(bp => bp.Account)
                .Where(bp => bp.Account.UserId == userId.Value);

            if (accountId.HasValue)
            {
                query = query.Where(bp => bp.AccountId == accountId.Value);
            }

            var payments = await query
                .OrderByDescending(bp => bp.CreatedAt)
                .Take(50)
                .ToListAsync();

            var result = payments.Select(bp => new
            {
                bp.Id,
                bp.ReferenceNumber,
                BillType = bp.BillType.ToString(),
                bp.BillNumber,
                bp.ServiceProvider,
                bp.Amount,
                bp.Currency,
                Status = bp.Status.ToString(),
                AccountNumber = bp.Account.AccountNumber,
                bp.CreatedAt,
                bp.PaidAt,
                bp.Notes
            });

            // إحصائيات
            var totalPaid = payments.Sum(p => p.Amount);
            var countByType = payments.GroupBy(p => p.BillType)
                .Select(g => new
                {
                    BillType = g.Key.ToString(),
                    Count = g.Count(),
                    TotalAmount = g.Sum(p => p.Amount)
                })
                .ToList();

            return Ok(new
            {
                Success = true,
                TotalPayments = payments.Count,
                TotalPaid = totalPaid,
                Statistics = countByType,
                Payments = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في جلب سجل الفواتير");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// الحصول على تفاصيل دفعة معينة
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetPaymentDetails(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var payment = await _context.BillPayments
                .Include(bp => bp.Account)
                .FirstOrDefaultAsync(bp => bp.Id == id && bp.Account.UserId == userId.Value);

            if (payment == null)
                return NotFound(new { Message = "الدفعة غير موجودة" });

            return Ok(new
            {
                Success = true,
                Payment = new
                {
                    payment.Id,
                    payment.ReferenceNumber,
                    BillType = payment.BillType.ToString(),
                    payment.BillNumber,
                    payment.ServiceProvider,
                    payment.Amount,
                    payment.Currency,
                    Status = payment.Status.ToString(),
                    AccountNumber = payment.Account.AccountNumber,
                    payment.CreatedAt,
                    payment.PaidAt,
                    payment.Notes
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في جلب تفاصيل الدفعة");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// الحصول على قائمة مزودي الخدمة حسب نوع الفاتورة
    /// </summary>
    [HttpGet("providers/{billType}")]
    public IActionResult GetServiceProviders(string billType)
    {
        try
        {
            // قائمة ثابتة للتجربة - في الواقع تُجلب من قاعدة بيانات
            var providers = billType.ToLower() switch
            {
                "electricity" => new[]
                {
                    new { Id = 1, Name = "وزارة الكهرباء", Code = "ELEC001" },
                    new { Id = 2, Name = "شركة توزيع الكهرباء", Code = "ELEC002" }
                },
                "water" => new[]
                {
                    new { Id = 1, Name = "أمانة المياه", Code = "WATER001" },
                    new { Id = 2, Name = "مديرية المياه", Code = "WATER002" }
                },
                "internet" => new[]
                {
                    new { Id = 1, Name = "Earthlink", Code = "ISP001" },
                    new { Id = 2, Name = "IQ Networks", Code = "ISP002" },
                    new { Id = 3, Name = "NEWROZ Telecom", Code = "ISP003" }
                },
                "phone" => new[]
                {
                    new { Id = 1, Name = "Zain Iraq", Code = "MOBILE001" },
                    new { Id = 2, Name = "Asiacell", Code = "MOBILE002" },
                    new { Id = 3, Name = "Korek", Code = "MOBILE003" }
                },
                _ => Array.Empty<object>()
            };

            return Ok(new
            {
                Success = true,
                BillType = billType,
                Providers = providers
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في جلب مزودي الخدمة");
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
        return $"BILL{DateTime.UtcNow:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";
    }

    private string GetBillTypeName(BillType billType)
    {
        return billType switch
        {
            BillType.Electricity => "الكهرباء",
            BillType.Water => "الماء",
            BillType.Internet => "الإنترنت",
            BillType.Phone => "الهاتف",
            BillType.Gas => "الغاز",
            BillType.Government => "حكومية",
            _ => "أخرى"
        };
    }
}

// ═══════════════════════════════════════════════
// Request Models
// ═══════════════════════════════════════════════

public record PayBillRequest
{
    public Guid AccountId { get; init; }
    public BillType BillType { get; init; }
    public string BillNumber { get; init; } = string.Empty;
    public string ServiceProvider { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? Notes { get; init; }
}