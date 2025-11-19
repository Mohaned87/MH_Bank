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
                CardNumber = c.CardNumber,
                MaskedCardNumber = MaskCardNumber(c.CardNumber),
                c.CardHolderName,
                c.ExpiryMonth,
                c.ExpiryYear,
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
            _logger.LogInformation("🔵 IssueCard request: AccountId={AccountId}, Brand={Brand}",
                request.AccountId, request.Brand);

            var userId = GetCurrentUserId();
            if (userId == null)
            {
                _logger.LogWarning("⚠️ Unauthorized - no userId");
                return Unauthorized(new { Success = false, Message = "غير مصرح" });
            }

            // التحقق من الحساب
            var account = await _context.BankAccounts
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.UserId == userId.Value);

            if (account == null)
            {
                _logger.LogWarning("⚠️ Account not found: {AccountId}", request.AccountId);
                return NotFound(new { Success = false, Message = "الحساب غير موجود" });
            }

            if (!account.IsActive)
            {
                _logger.LogWarning("⚠️ Account not active: {AccountId}", request.AccountId);
                return BadRequest(new { Success = false, Message = "الحساب غير نشط" });
            }

            // التحقق من نوع البطاقة
            CardType cardType = CardType.Debit;
            if (!string.IsNullOrEmpty(request.CardType))
            {
                if (!Enum.TryParse<CardType>(request.CardType, out cardType))
                {
                    cardType = CardType.Debit;
                }
            }

            // التحقق من Brand
            if (!Enum.IsDefined(typeof(CardBrand), request.Brand))
            {
                _logger.LogWarning("⚠️ Invalid brand: {Brand}", request.Brand);
                return BadRequest(new { Success = false, Message = "نوع البطاقة غير صحيح. استخدم: 1 للـ Visa أو 2 للـ Mastercard" });
            }

            var cardBrand = (CardBrand)request.Brand;

            // التحقق من عدم وجود بطاقة من نفس النوع
            var existingCard = await _context.Cards
                .FirstOrDefaultAsync(c => c.AccountId == request.AccountId && c.Brand == cardBrand);

            if (existingCard != null)
            {
                _logger.LogWarning("⚠️ Card already exists: {Brand} for {AccountId}", cardBrand, request.AccountId);
                return BadRequest(new
                {
                    Success = false,
                    Message = $"يوجد بطاقة {cardBrand} بالفعل لهذا الحساب"
                });
            }

            // إنشاء رقم بطاقة
            var cardNumber = GenerateCardNumber(cardBrand);
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
                Brand = cardBrand, // استخدام Brand من Request
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
                    Id = card.Id.ToString(),
                    CardNumber = cardNumber,
                    MaskedCardNumber = MaskCardNumber(cardNumber),
                    card.CardHolderName,
                    card.ExpiryMonth,
                    card.ExpiryYear,
                    CVV = cvv,
                    DefaultPIN = pin,
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
            return StatusCode(500, new { Success = false, Message = $"حدث خطأ: {ex.Message}" });
        }
    }

    /// <summary>
    /// تفعيل/تعطيل البطاقة
    /// </summary>
    [HttpPost("{id}/toggle")]
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
    /// حذف بطاقة
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCard(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { Success = false, Message = "غير مصرح" });

            var card = await _context.Cards
                .Include(c => c.Account)
                .FirstOrDefaultAsync(c => c.Id == id && c.Account.UserId == userId.Value);

            if (card == null)
                return NotFound(new { Success = false, Message = "البطاقة غير موجودة" });

            _context.Cards.Remove(card);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ تم حذف البطاقة: {CardNumber}", MaskCardNumber(card.CardNumber));

            return Ok(new
            {
                Success = true,
                Message = "تم حذف البطاقة بنجاح"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في حذف البطاقة");
            return StatusCode(500, new { Success = false, Message = "حدث خطأ" });
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

    /// <summary>
    /// إصدار بطاقة لأول مرة مع إنشاء حساب جديد
    /// </summary>
    [HttpPost("issue-with-new-account")]
    public async Task<IActionResult> IssueCardWithNewAccount([FromBody] IssueCardWithNewAccountRequest request)
    {
        try
        {
            _logger.LogInformation("🔵 IssueCardWithNewAccount: Brand={Brand}", request.Brand);

            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { Success = false, Message = "غير مصرح" });

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
                return NotFound(new { Success = false, Message = "المستخدم غير موجود" });

            if (!Enum.IsDefined(typeof(CardBrand), request.Brand))
                return BadRequest(new { Success = false, Message = "نوع البطاقة غير صحيح" });

            var cardBrand = (CardBrand)request.Brand;

            // إنشاء حساب جديد
            var random = new Random();
            var accountNumber = "100" + random.Next(1000000000, int.MaxValue).ToString();
            var iban = $"IQ98MHBN{accountNumber}";

            var newAccount = new BankAccount
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                AccountNumber = accountNumber,
                IBAN = iban,
                AccountType = AccountType.Checking,
                Balance = 0,
                Currency = "IQD",
                IsActive = true,
                OpenedAt = DateTime.UtcNow
            };

            _context.BankAccounts.Add(newAccount);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ Created account: {AccountNumber}", accountNumber);

            // إنشاء البطاقة
            var cardNumber = GenerateCardNumber(cardBrand);
            var cvv = GenerateCVV();
            var pin = GeneratePIN();

            var card = new Card
            {
                Id = Guid.NewGuid(),
                AccountId = newAccount.Id,
                CardNumber = cardNumber,
                CardHolderName = $"{user.FirstName} {user.LastName}",
                ExpiryMonth = DateTime.UtcNow.AddYears(5).Month.ToString("D2"),
                ExpiryYear = DateTime.UtcNow.AddYears(5).Year.ToString(),
                CVV = cvv,
                PinHash = BCrypt.Net.BCrypt.HashPassword(pin),
                Brand = cardBrand,
                CardType = CardType.Debit,
                IsActive = false, // غير مفعلة
                IsBlocked = false,
                DailyLimit = 5000000,
                MonthlyLimit = 50000000,
                IssuedAt = DateTime.UtcNow
            };

            _context.Cards.Add(card);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ Issued inactive card: {CardNumber}", MaskCardNumber(cardNumber));

            return Ok(new
            {
                Success = true,
                Message = "تم إنشاء الحساب وإصدار البطاقة بنجاح",
                Account = new
                {
                    Id = newAccount.Id.ToString(),
                    newAccount.AccountNumber,
                    newAccount.IBAN,
                    AccountType = newAccount.AccountType.ToString(),
                    newAccount.Balance,
                    newAccount.Currency
                },
                Card = new
                {
                    Id = card.Id.ToString(),
                    CardNumber = cardNumber,
                    MaskedCardNumber = MaskCardNumber(cardNumber),
                    card.CardHolderName,
                    card.ExpiryMonth,
                    card.ExpiryYear,
                    CVV = cvv,
                    DefaultPIN = pin,
                    Brand = card.Brand.ToString(),
                    IsActive = false,
                    Message = "⚠️ البطاقة غير مفعلة. يجب تفعيلها بكلمة سر التطبيق."
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ خطأ في إصدار البطاقة");
            return StatusCode(500, new { Success = false, Message = $"حدث خطأ: {ex.Message}" });
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

    private string GenerateCardNumber(CardBrand brand)
    {
        var random = new Random();
        var prefix = brand == CardBrand.Visa ? "4" : "5"; // Visa = 4, Mastercard = 5

        return $"{prefix}{random.Next(100, 999)}{random.Next(1000, 9999)}{random.Next(1000, 9999)}{random.Next(1000, 9999)}";
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
    public string CardType { get; init; } = "Debit"; // Debit or Credit
    public int Brand { get; init; } = 1; // 1 = Visa, 2 = Mastercard
}

public record IssueCardWithNewAccountRequest
{
    public int Brand { get; init; } = 1; // 1 = Visa, 2 = Mastercard
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