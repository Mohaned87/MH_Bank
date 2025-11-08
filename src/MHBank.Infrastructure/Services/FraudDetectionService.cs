using MHBank.Core.Entities;
using MHBank.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MHBank.Infrastructure.Services;

/// <summary>
/// خدمة كشف الاحتيال
/// </summary>
public class FraudDetectionService
{
    private readonly ApplicationDbContext _context;
    private readonly NotificationService _notificationService;
    private readonly ILogger<FraudDetectionService> _logger;

    public FraudDetectionService(
        ApplicationDbContext context,
        NotificationService notificationService,
        ILogger<FraudDetectionService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// فحص معاملة قبل تنفيذها
    /// </summary>
    public async Task<FraudCheckResult> CheckTransactionAsync(
        BankAccount account,
        decimal amount,
        string? ipAddress = null)
    {
        var suspiciousReasons = new List<string>();
        var riskScore = 0;

        // 1. مبلغ كبير غير معتاد
        var avgTransaction = await GetAverageTransactionAmountAsync(account.Id);
        if (amount > avgTransaction * 5)
        {
            suspiciousReasons.Add($"المبلغ ({amount:N0}) أكبر بـ 5 أضعاف من المعدل ({avgTransaction:N0})");
            riskScore += 30;
        }

        // 2. عدد كبير من المعاملات في وقت قصير
        var recentTransactionsCount = await GetRecentTransactionsCountAsync(account.Id, minutes: 10);
        if (recentTransactionsCount > 5)
        {
            suspiciousReasons.Add($"عدد كبير من المعاملات ({recentTransactionsCount}) في 10 دقائق");
            riskScore += 25;
        }

        // 3. معاملات في ساعات غير عادية (منتصف الليل)
        var currentHour = DateTime.UtcNow.Hour;
        if (currentHour >= 2 && currentHour <= 5)
        {
            suspiciousReasons.Add("معاملة في وقت غير معتاد (2-5 صباحاً)");
            riskScore += 15;
        }

        // 4. تجاوز الرصيد المعتاد
        if (amount > account.Balance * 0.8m)
        {
            suspiciousReasons.Add($"المبلغ يمثل أكثر من 80% من الرصيد");
            riskScore += 20;
        }

        // 5. تغيير IP فجأة (محاكاة)
        var lastTransaction = await _context.Transactions
            .Where(t => t.AccountId == account.Id)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        // تحديد مستوى الخطر
        var riskLevel = riskScore switch
        {
            >= 70 => RiskLevel.High,
            >= 40 => RiskLevel.Medium,
            >= 20 => RiskLevel.Low,
            _ => RiskLevel.Safe
        };

        var result = new FraudCheckResult
        {
            IsBlocked = riskLevel == RiskLevel.High,
            RequiresVerification = riskLevel == RiskLevel.Medium,
            RiskLevel = riskLevel,
            RiskScore = riskScore,
            SuspiciousReasons = suspiciousReasons
        };

        // تسجيل في Logs
        if (result.IsBlocked || result.RequiresVerification)
        {
            _logger.LogWarning(
                "🚨 نشاط مشبوه: حساب {AccountNumber} - خطورة: {RiskLevel} ({RiskScore}) - الأسباب: {Reasons}",
                account.AccountNumber,
                riskLevel,
                riskScore,
                string.Join(", ", suspiciousReasons)
            );

            // إرسال إشعار أمني
            if (result.IsBlocked)
            {
                await _notificationService.NotifySecurityEventAsync(
                    account.UserId,
                    $"تم حظر معاملة مشبوهة بمبلغ {amount:N2}. إذا لم تكن أنت، يرجى الاتصال بنا فوراً."
                );

                // تجميد الحساب (اختياري)
                // account.IsActive = false;
                // await _context.SaveChangesAsync();
            }
            else if (result.RequiresVerification)
            {
                await _notificationService.NotifySecurityEventAsync(
                    account.UserId,
                    $"تم اكتشاف نشاط غير معتاد. يرجى التحقق من معاملتك بمبلغ {amount:N2}"
                );
            }
        }

        return result;
    }

    /// <summary>
    /// الحصول على متوسط المعاملات
    /// </summary>
    private async Task<decimal> GetAverageTransactionAmountAsync(Guid accountId)
    {
        var last30Days = DateTime.UtcNow.AddDays(-30);

        var avgAmount = await _context.Transactions
            .Where(t => t.AccountId == accountId && t.CreatedAt >= last30Days)
            .AverageAsync(t => (decimal?)t.Amount);

        return avgAmount ?? 1000; // قيمة افتراضية
    }

    /// <summary>
    /// عدد المعاملات الأخيرة
    /// </summary>
    private async Task<int> GetRecentTransactionsCountAsync(Guid accountId, int minutes)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-minutes);

        return await _context.Transactions
            .Where(t => t.AccountId == accountId && t.CreatedAt >= cutoff)
            .CountAsync();
    }

    /// <summary>
    /// تحليل أنماط الحساب
    /// </summary>
    public async Task<AccountBehaviorAnalysis> AnalyzeAccountBehaviorAsync(Guid accountId)
    {
        var last30Days = DateTime.UtcNow.AddDays(-30);

        var transactions = await _context.Transactions
            .Where(t => t.AccountId == accountId && t.CreatedAt >= last30Days)
            .ToListAsync();

        var analysis = new AccountBehaviorAnalysis
        {
            TotalTransactions = transactions.Count,
            AverageAmount = transactions.Any() ? transactions.Average(t => t.Amount) : 0,
            MaxAmount = transactions.Any() ? transactions.Max(t => t.Amount) : 0,
            MostActiveHour = transactions.GroupBy(t => t.CreatedAt.Hour)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault(),
            SuspiciousTransactionsCount = 0 // يمكن تحسينه
        };

        return analysis;
    }
}

/// <summary>
/// نتيجة فحص الاحتيال
/// </summary>
public class FraudCheckResult
{
    public bool IsBlocked { get; set; }
    public bool RequiresVerification { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public int RiskScore { get; set; }
    public List<string> SuspiciousReasons { get; set; } = new();
}

/// <summary>
/// مستوى الخطر
/// </summary>
public enum RiskLevel
{
    Safe = 0,
    Low = 1,
    Medium = 2,
    High = 3
}

/// <summary>
/// تحليل سلوك الحساب
/// </summary>
public class AccountBehaviorAnalysis
{
    public int TotalTransactions { get; set; }
    public decimal AverageAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int MostActiveHour { get; set; }
    public int SuspiciousTransactionsCount { get; set; }
}