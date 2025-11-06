using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MHBank.Core.Entities;
using MHBank.Infrastructure.Data;

namespace MHBank.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CardsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CardsController> _logger;

    public CardsController(ApplicationDbContext context, ILogger<CardsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// الحصول على جميع بطاقات المستخدم
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyCards()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var cards = await _context.Cards
                .Include(c => c.Account)
                .Where(c => c.Account.UserId == userId.Value)
                .OrderByDescending(c => c.IssuedAt)
                .ToListAsync();

            var result = cards.Select(c => new
            {
                c.Id,
                CardNumber = MaskCardNumber(c.CardNumber),
                c.CardHolderName,
                ExpiryDate = $"{c.ExpiryMonth}/{c.ExpiryYear}",
                CardType = c.CardType.ToString(),
                Brand = c.Brand.ToString(),
                c.IsActive,
                c.IsBlocked,
                AccountNumber = c.Account.AccountNumber,
                c.IssuedAt
            });

            return Ok(new
            {
                Success = true,
                TotalCards = cards.Count,
                Cards = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في جلب البطاقات");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// إصدار بطاقة جديدة
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> IssueCard([FromBody] IssueCardRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            // التحقق من الحساب
            var account = await _context.BankAccounts
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.UserId == userId.Value);

            if (account == null)
                return NotFound(new { Message = "الحساب غير موجود" });

            if (!account.IsActive)
                return BadRequest(new { Message = "الحساب غير نشط" });

            // التحقق من نوع البطاقة
            if (!Enum.TryParse<CardType>(request.CardType, out var cardType))
            {
                return BadRequest(new { Message = "نوع البطاقة غير صحيح. استخدم: Debit أو Credit" });
            }

            // إنشاء رقم بطاقة
            var cardNumber = GenerateCardNumber();
            var cvv = GenerateCVV();
            var pin = GeneratePIN();

            var card = new Card
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                CardNumber = cardNumber,
                CardHolderName = $"{account.User.FirstName} {account.User.LastName}".ToUpper(),
                ExpiryMonth = DateTime.UtcNow.AddYears(3).Month.ToString("00"),
                ExpiryYear = DateTime.UtcNow.AddYears(3).Year.ToString().Substring(2),
                CVV = cvv,
                CardType = cardType,
                Brand = CardBrand.Visa,
                PinHash = BCrypt.Net.BCrypt.HashPassword(pin),
                IsActive = true,
                IsBlocked = false,
                ContactlessEnabled = true,
                OnlinePaymentsEnabled = true,
                InternationalPaymentsEnabled = false,
                DailyLimit = 5000,
                MonthlyLimit = 50000,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddYears(3)
            };

            _context.Cards.Add(card);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تم إصدار بطاقة جديدة: {CardNumber}", MaskCardNumber(cardNumber));

            return Ok(new
            {
                Success = true,
                Message = "تم إصدار البطاقة بنجاح",
                Card = new
                {
                    card.Id,
                    CardNumber = MaskCardNumber(cardNumber),
                    FullCardNumber = cardNumber, // في الواقع، لا نرسل الرقم الكامل!
                    card.CardHolderName,
                    ExpiryDate = $"{card.ExpiryMonth}/{card.ExpiryYear}",
                    CVV = cvv, // في الواقع، لا نرسل CVV!
                    PIN = pin, // في الواقع، لا نرسل PIN!
                    CardType = card.CardType.ToString(),
                    Brand = card.Brand.ToString(),
                    card.DailyLimit,
                    card.IssuedAt
                },
                Warning = "⚠️ احفظ رقم البطاقة والـ PIN - لن يتم عرضهم مرة أخرى!"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في إصدار البطاقة");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// تفعيل/تعطيل البطاقة
    /// </summary>
    [HttpPatch("{id}/toggle")]
    public async Task<IActionResult> ToggleCard(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var card = await _context.Cards
                .Include(c => c.Account)
                .FirstOrDefaultAsync(c => c.Id == id && c.Account.UserId == userId.Value);

            if (card == null)
                return NotFound(new { Message = "البطاقة غير موجودة" });

            card.IsActive = !card.IsActive;
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تم {Action} البطاقة: {CardNumber}",
                card.IsActive ? "تفعيل" : "تعطيل",
                MaskCardNumber(card.CardNumber));

            return Ok(new
            {
                Success = true,
                Message = card.IsActive ? "تم تفعيل البطاقة" : "تم تعطيل البطاقة",
                IsActive = card.IsActive
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تفعيل/تعطيل البطاقة");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// حظر البطاقة
    /// </summary>
    [HttpPost("{id}/block")]
    public async Task<IActionResult> BlockCard(Guid id, [FromBody] BlockCardRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var card = await _context.Cards
                .Include(c => c.Account)
                .FirstOrDefaultAsync(c => c.Id == id && c.Account.UserId == userId.Value);

            if (card == null)
                return NotFound(new { Message = "البطاقة غير موجودة" });

            card.IsBlocked = true;
            card.BlockReason = request.Reason;
            card.BlockedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogWarning("⚠️ تم حظر البطاقة: {CardNumber} - السبب: {Reason}",
                MaskCardNumber(card.CardNumber), request.Reason);

            return Ok(new
            {
                Success = true,
                Message = "تم حظر البطاقة بنجاح"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في حظر البطاقة");
            return StatusCode(500, new { Message = "حدث خطأ" });
        }
    }

    /// <summary>
    /// تغيير PIN
    /// </summary>
    [HttpPost("{id}/change-pin")]
    public async Task<IActionResult> ChangePIN(Guid id, [FromBody] ChangePINRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var card = await _context.Cards
                .Include(c => c.Account)
                .FirstOrDefaultAsync(c => c.Id == id && c.Account.UserId == userId.Value);

            if (card == null)
                return NotFound(new { Message = "البطاقة غير موجودة" });

            // التحقق من PIN القديم
            if (!BCrypt.Net.BCrypt.Verify(request.OldPIN, card.PinHash))
            {
                card.FailedPinAttempts++;

                if (card.FailedPinAttempts >= 3)
                {
                    card.IsBlocked = true;
                    card.BlockReason = "محاولات خاطئة متعددة لإدخال PIN";
                }

                await _context.SaveChangesAsync();
                return BadRequest(new { Message = "PIN القديم غير صحيح" });
            }

            // تحديث PIN
            card.PinHash = BCrypt.Net.BCrypt.HashPassword(request.NewPIN);
            card.FailedPinAttempts = 0;
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تم تغيير PIN للبطاقة: {CardNumber}",
                MaskCardNumber(card.CardNumber));

            return Ok(new
            {
                Success = true,
                Message = "تم تغيير PIN بنجاح"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في تغيير PIN");
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

    private string GenerateCardNumber()
    {
        // Visa starts with 4
        var random = new Random();
        return $"4{random.Next(100, 999)}{random.Next(1000, 9999)}{random.Next(1000, 9999)}{random.Next(1000, 9999)}";
    }

    private string GenerateCVV()
    {
        return new Random().Next(100, 999).ToString();
    }

    private string GeneratePIN()
    {
        return new Random().Next(1000, 9999).ToString();
    }

    private string MaskCardNumber(string cardNumber)
    {
        if (cardNumber.Length < 16)
            return cardNumber;

        return $"{cardNumber.Substring(0, 4)} **** **** {cardNumber.Substring(12)}";
    }
}

// ═══════════════════════════════════════════════
// Request Models
// ═══════════════════════════════════════════════

public record IssueCardRequest
{
    public Guid AccountId { get; init; }
    public string CardType { get; init; } = string.Empty; // Debit or Credit
}

public record BlockCardRequest
{
    public string Reason { get; init; } = string.Empty;
}

public record ChangePINRequest
{
    public string OldPIN { get; init; } = string.Empty;
    public string NewPIN { get; init; } = string.Empty;
}