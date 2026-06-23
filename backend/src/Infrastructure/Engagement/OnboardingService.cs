using System.Text.Json;
using Academy.Application.Engagement;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Engagement;

public class OnboardingService(AppDbContext db) : IOnboardingService
{
    public async Task<OnboardingStateDto> GetStateAsync(Guid userId, CancellationToken ct = default)
    {
        var tour = await db.TourStates.AnyAsync(
            t => t.UserId == userId && t.TourKey == IOnboardingService.FirstRunTourKey, ct);
        var survey = await db.OnboardingSurveys.AnyAsync(s => s.UserId == userId, ct);
        return new OnboardingStateDto(tour, survey);
    }

    public async Task CompleteTourAsync(Guid userId, string tourKey, TourStatus status, CancellationToken ct = default)
    {
        var existing = await db.TourStates.FirstOrDefaultAsync(t => t.UserId == userId && t.TourKey == tourKey, ct);
        if (existing is null)
            db.TourStates.Add(new TourState { Id = Guid.CreateVersion7(), UserId = userId, TourKey = tourKey, Status = status });
        else
            existing.Status = status;
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveSurveyAsync(Guid userId, SaveSurveyRequest req, CancellationToken ct = default)
    {
        var existing = await db.OnboardingSurveys.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        var goals = JsonSerializer.Serialize(req.Goals);
        var tools = JsonSerializer.Serialize(req.PreferredTools);

        if (existing is null)
        {
            db.OnboardingSurveys.Add(new OnboardingSurvey
            {
                Id = Guid.CreateVersion7(), UserId = userId, Role = req.Role, Goals = goals, PreferredTools = tools,
            });
        }
        else
        {
            existing.Role = req.Role;
            existing.Goals = goals;
            existing.PreferredTools = tools;
        }
        await db.SaveChangesAsync(ct);
    }
}
