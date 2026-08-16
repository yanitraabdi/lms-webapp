using System.Text.Json;
using Academy.Application.Billing;
using Academy.Application.Programs;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Billing;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Programs;

/// <summary>
/// One-time program purchase (KAK §9.4). Creates a PendingPayment enrollment plus a single
/// hosted invoice. It GRANTS NOTHING — only the verified webhook activates an enrollment (GR-2).
/// </summary>
public class EnrollmentService(AppDbContext db, IPaymentGateway gateway) : IEnrollmentService
{
    public async Task<CheckoutSession> CheckoutAsync(
        Guid userId, Guid programId, Guid? batchId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new ProgramException("Pengguna tidak ditemukan.", 404);
        if (!user.EmailVerified)
            throw new ProgramException("Verifikasi email Anda sebelum mendaftar.", 403);

        var program = await db.Programs.FirstOrDefaultAsync(p => p.Id == programId, ct)
            ?? throw new ProgramException("Program tidak ditemukan.", 404);
        if (program.Status != ProgramStatus.Published)
            throw new ProgramException("Program belum dibuka untuk pendaftaran.", 400);

        if (batchId is Guid bid && !await db.ProgramBatches.AnyAsync(b => b.Id == bid && b.ProgramId == programId, ct))
            throw new ProgramException("Batch tidak ditemukan pada program ini.", 400);

        var existing = await db.Enrollments
            .FirstOrDefaultAsync(e => e.UserId == userId && e.ProgramId == programId, ct);
        if (existing is not null && existing.GrantsAccess)
            throw new ProgramException("Anda sudah terdaftar pada program ini.", 409);

        var session = await gateway.CreateCheckoutAsync(
            new CheckoutIntent(userId, null, null, program.PriceIdr, CheckoutKind.ProgramPurchase, null, programId), ct);

        // Reuse the row for a retried/abandoned purchase — UNIQUE(user_id, program_id).
        var enrollment = existing ?? new Enrollment
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            ProgramId = programId,
        };
        enrollment.BatchId = batchId;
        enrollment.Status = EnrollmentStatus.PendingPayment;   // grants nothing
        enrollment.AmountPaidIdr = 0m;                         // set by the webhook on success
        enrollment.ProviderRef = session.ProviderRef;
        if (existing is null) db.Enrollments.Add(enrollment);

        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SubscriptionId = null,
            AmountIdr = program.PriceIdr,
            Kind = PaymentKind.ProgramPurchase,
            Status = PaymentStatus.Pending,
            ProviderRef = session.ProviderRef,
            XenditIds = JsonSerializer.Serialize(new { provider = gateway.Source, providerRef = session.ProviderRef }),
            RawPayload = JsonSerializer.Serialize(new CheckoutIntentSnapshot(
                null, BillingCycle.Monthly, CheckoutKind.ProgramPurchase, null, programId, enrollment.Id)),
        });

        await db.SaveChangesAsync(ct);
        return session;
    }

    public async Task<IReadOnlyList<EnrollmentDto>> ListMineAsync(Guid userId, CancellationToken ct = default)
        => await db.Enrollments
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new EnrollmentDto(
                e.Id, e.ProgramId, e.Program.Name, e.Program.Slug,
                e.Status.ToString(), e.AmountPaidIdr, e.EnrolledAt))
            .ToListAsync(ct);

    public Task<bool> IsEnrolledAsync(Guid userId, Guid programId, CancellationToken ct = default)
        => db.Enrollments.AnyAsync(
            e => e.UserId == userId && e.ProgramId == programId
                 && (e.Status == EnrollmentStatus.Active || e.Status == EnrollmentStatus.Completed), ct);
}
