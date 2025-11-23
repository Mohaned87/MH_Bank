using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MHBank.Core.Entities;
using MHBank.Infrastructure.Data;
using System.Security.Claims;

namespace MHBank.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KYCController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<KYCController> _logger;

    public KYCController(ApplicationDbContext context, ILogger<KYCController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private string? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                 ?? User.FindFirst("sub")
                 ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

        return claim?.Value;
    }
    /// <summary>
    /// Test endpoint - للتأكد من اتصال Mobile
    /// </summary>
    [HttpPost("test")]
    public IActionResult TestConnection([FromBody] TestRequest request)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("🧪 Test endpoint called by user: {UserId}", userId);
        _logger.LogInformation("📥 Received data: {Data}", request.TestData);

        return Ok(new
        {
            Success = true,
            Message = "Mobile connected successfully!",
            UserId = userId,
            ReceivedData = request.TestData,
            Timestamp = DateTime.UtcNow
        });
    }
    [HttpGet("{accountId}/status")]
    public async Task<IActionResult> GetKYCStatus(Guid accountId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var account = await _context.BankAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == Guid.Parse(userId));

            if (account == null)
                return NotFound();

            var kycRequest = await _context.KYCRequests
                .Include(k => k.Documents)
                .Where(k => k.AccountId == accountId)
                .OrderByDescending(k => k.CreatedAt)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                Success = true,
                KYCStatus = account.KYCStatus,
                Request = kycRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting KYC status");
            return StatusCode(500);
        }
    }

    [HttpPost("{accountId}/upload")]
    public async Task<IActionResult> UploadDocument(Guid accountId, [FromBody] UploadDocumentRequest request)
    {
        try
        {
            _logger.LogInformation("📤 Upload attempt for account: {AccountId}", accountId);

            var userId = GetCurrentUserId();
            _logger.LogInformation("👤 UserId from token: {UserId}", userId ?? "NULL");

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("❌ No userId found in token!");
                return Unauthorized(new { Success = false, Message = "غير مصرح - لا يوجد معرف مستخدم" });
            }

            _logger.LogInformation("🔍 Looking for account {AccountId} for user {UserId}", accountId, userId);

            var account = await _context.BankAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == Guid.Parse(userId));

            if (account == null)
            {
                _logger.LogWarning("❌ Account not found or doesn't belong to user");
                return NotFound(new { Success = false, Message = "الحساب غير موجود" });
            }

            _logger.LogInformation("✅ Account found: {AccountNumber}", account.AccountNumber);

            var kycRequest = await _context.KYCRequests
                .FirstOrDefaultAsync(k => k.AccountId == accountId && k.Status == KYCStatus.NotStarted);

            if (kycRequest == null)
            {
                _logger.LogInformation("📝 Creating new KYC request");
                kycRequest = new KYCRequest
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.Parse(userId),
                    AccountId = accountId,
                    Status = KYCStatus.NotStarted,
                    CreatedAt = DateTime.UtcNow
                };
                _context.KYCRequests.Add(kycRequest);
            }
            else
            {
                _logger.LogInformation("📋 Using existing KYC request: {RequestId}", kycRequest.Id);
            }

            _logger.LogInformation("📄 Creating document: Type={Type}", request.Type);

            var document = new KYCDocument
            {
                Id = Guid.NewGuid(),
                KYCRequestId = kycRequest.Id,
                Type = request.Type,
                FileName = request.FileName ?? $"{request.Type}.jpg",
                MimeType = request.MimeType ?? "image/jpeg",
                FileSize = request.Base64Data.Length,
                Base64Data = request.Base64Data,
                UploadedAt = DateTime.UtcNow
            };

            _context.KYCDocuments.Add(document);

            var changes = await _context.SaveChangesAsync();
            _logger.LogInformation("✅ Saved {Changes} changes. Document ID: {DocId}", changes, document.Id);

            return Ok(new
            {
                Success = true,
                Document = new
                {
                    document.Id,
                    document.Type,
                    document.FileName,
                    document.MimeType,
                    document.FileSize,
                    document.UploadedAt,
                    KYCRequestId = document.KYCRequestId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error uploading document for account {AccountId}", accountId);
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

    [HttpPost("{accountId}/submit")]
    public async Task<IActionResult> SubmitKYCRequest(Guid accountId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var kycRequest = await _context.KYCRequests
                .Include(k => k.Documents)
                .Include(k => k.Account)
                .FirstOrDefaultAsync(k => k.AccountId == accountId && k.UserId == Guid.Parse(userId));

            if (kycRequest == null)
                return NotFound();

            kycRequest.Status = KYCStatus.Pending;
            kycRequest.SubmittedAt = DateTime.UtcNow;
            kycRequest.Account.KYCStatus = KYCStatus.Pending;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Success = true,
                Request = new
                {
                    kycRequest.Id,
                    kycRequest.Status,
                    kycRequest.SubmittedAt,
                    DocumentsCount = kycRequest.Documents.Count
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting KYC");
            return StatusCode(500);
        }
    }
}

public record UploadDocumentRequest
{
    public DocumentType Type { get; init; }
    public string Base64Data { get; init; } = string.Empty;
    public string? FileName { get; init; }
    public string? MimeType { get; init; }
}
public record TestRequest
{
    public string TestData { get; init; } = string.Empty;
}