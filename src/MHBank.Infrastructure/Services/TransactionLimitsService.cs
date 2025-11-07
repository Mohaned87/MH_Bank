using MHBank.Core.Entities;
using MHBank.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MHBank.Infrastructure.Services;

/// <summary>
/// خدمة إدارة حدود المعاملات
/// </summary>
public class TransactionLimitsService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TransactionLimitsService> _logger;

    public TransactionLimitsService(ApplicationDbContext context, ILogger<TransactionLimitsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// التحقق من إمكانية إجراء معاملة
    /// </summary>
    public async Task<(bool IsAllowed, string? ErrorMessage)> CanPerformTransactionAsync(
        BankAccount account,
        decimal amount)
    {
        // إعادة تعيين الحدود إذا لزم الأمر
        await ResetLimitsIfNeededAsync(account);

        // 1. التحقق من الحد الأدنى
        if (amount < 1)
        {
            return (false, "المبلغ يجب أن يكون 1 على الأقل");
        }

        // 2. التحقق من الحد الأقصى للمعاملة الواحدة
        const decimal MAX_SINGLE_TRANSACTION = 100000;
        if (amount > MAX_SINGLE_TRANSACTION)
        {
            return (false, $"المبلغ يتجاوز الحد الأقصى للمعاملة الواحدة ({MAX_SINGLE_TRANSACTION:N0})");
        }

        // 3. التحقق من الرصيد
        if (account.Balance < amount)
        {
            return (false, $"الرصيد غير كافٍ. الرصيد الحالي: {account.Balance:N2}");
        }

        // 4. التحقق من الحد اليومي
        if (account.CurrentDailyTransferred + amount > account.DailyTransferLimit)
        {
            var remaining = account.DailyTransferLimit - account.CurrentDailyTransferred;
            return (false, $"تجاوزت الحد اليومي. المتبقي اليوم: {remaining:N2}");
        }

        // 5. التحقق من الحد الشهري
        if (account.CurrentMonthlyTransferred + amount > account.MonthlyTransferLimit)
        {
            var remaining = account.MonthlyTransferLimit - account.CurrentMonthlyTransferred;
            return (false, $"تجاوزت الحد الشهري. المتبقي هذا الشهر: {remaining:N2}");
        }

        return (true, null);
    }

    /// <summary>
    /// تسجيل معاملة (تحديث الحدود)
    /// </summary>
    public async Task RecordTransactionAsync(BankAccount account, decimal amount)
    {
        account.CurrentDailyTransferred += amount;
        account.CurrentMonthlyTransferred += amount;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "📊 تم تحديث حدود الحساب {AccountNumber} - يومي: {Daily}/{DailyLimit}, شهري: {Monthly}/{MonthlyLimit}",
            account.AccountNumber,
            account.CurrentDailyTransferred,
            account.DailyTransferLimit,
            account.CurrentMonthlyTransferred,
            account.MonthlyTransferLimit
        );
    }

    /// <summary>
    /// إعادة تعيين الحدود إذا انتهى اليوم/الشهر
    /// </summary>
    private async Task ResetLimitsIfNeededAsync(BankAccount account)
    {
        var now = DateTime.UtcNow;
        var lastTransaction = account.LastTransactionAt ?? account.OpenedAt;

        bool needsUpdate = false;

        // إعادة تعيين الحد اليومي (إذا تغير اليوم)
        if (lastTransaction.Date < now.Date)
        {
            account.CurrentDailyTransferred = 0;
            needsUpdate = true;
            _logger.LogInformation("🔄 إعادة تعيين الحد اليومي للحساب: {AccountNumber}", account.AccountNumber);
        }

        // إعادة تعيين الحد الشهري (إذا تغير الشهر)
        if (lastTransaction.Year < now.Year || lastTransaction.Month < now.Month)
        {
            account.CurrentMonthlyTransferred = 0;
            needsUpdate = true;
            _logger.LogInformation("🔄 إعادة تعيين الحد الشهري للحساب: {AccountNumber}", account.AccountNumber);
        }

        if (needsUpdate)
        {
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// الحصول على ملخص الحدود للحساب
    /// </summary>
    public async Task<TransactionLimitsSummary> GetLimitsSummaryAsync(Guid accountId)
    {
        var account = await _context.BankAccounts.FindAsync(accountId);
        if (account == null)
            throw new ArgumentException("الحساب غير موجود");

        await ResetLimitsIfNeededAsync(account);

        return new TransactionLimitsSummary
        {
            DailyLimit = account.DailyTransferLimit,
            DailyUsed = account.CurrentDailyTransferred,
            DailyRemaining = account.DailyTransferLimit - account.CurrentDailyTransferred,
            MonthlyLimit = account.MonthlyTransferLimit,
            MonthlyUsed = account.CurrentMonthlyTransferred,
            MonthlyRemaining = account.MonthlyTransferLimit - account.CurrentMonthlyTransferred,
            SingleTransactionMax = 100000
        };
    }

    /// <summary>
    /// تحديث حدود الحساب (Admin)
    /// </summary>
    public async Task UpdateLimitsAsync(Guid accountId, decimal? dailyLimit, decimal? monthlyLimit)
    {
        var account = await _context.BankAccounts.FindAsync(accountId);
        if (account == null)
            throw new ArgumentException("الحساب غير موجود");

        if (dailyLimit.HasValue)
        {
            account.DailyTransferLimit = dailyLimit.Value;
        }

        if (monthlyLimit.HasValue)
        {
            account.MonthlyTransferLimit = monthlyLimit.Value;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("✅ تم تحديث حدود الحساب {AccountNumber}", account.AccountNumber);
    }
}

/// <summary>
/// ملخص حدود المعاملات
/// </summary>
public class TransactionLimitsSummary
{
    public decimal DailyLimit { get; set; }
    public decimal DailyUsed { get; set; }
    public decimal DailyRemaining { get; set; }
    public decimal MonthlyLimit { get; set; }
    public decimal MonthlyUsed { get; set; }
    public decimal MonthlyRemaining { get; set; }
    public decimal SingleTransactionMax { get; set; }
}