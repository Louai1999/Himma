using Himma.Api.Data;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Himma.Api.Infrastructure.Localization;

namespace Himma.Api.Features.Profile;

[Route("api/profile")]
public sealed class ProfileController(AppDbContext db) : HimmaControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProfileResponse>> Get(CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == UserId, cancellationToken);
        return profile is null
            ? NotFoundProblem(Lang.T("لم يتم إعداد الملف الشخصي بعد.", "Your profile is not set up yet."))
            : ProfileResponse.From(profile);
    }

    [HttpPut]
    public async Task<ActionResult<ProfileResponse>> Put(ProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        var isFirstSave = profile is null;
        if (profile is null)
        {
            profile = new UserProfile { UserId = userId };
            db.UserProfiles.Add(profile);
        }

        profile.Gender = request.Gender!.Value;
        profile.Age = request.Age!.Value;
        profile.HeightCm = request.HeightCm!.Value;
        profile.WeightKg = request.WeightKg!.Value;
        profile.ActivityLevel = request.ActivityLevel!.Value;
        profile.Goal = request.Goal!.Value;
        profile.TargetWeightKg = request.TargetWeightKg;
        profile.UpdatedAtUtc = DateTime.UtcNow;

        if (isFirstSave)
        {
            await SeedFirstWeightAsync(userId, ApiDate.Parse(request.Date), profile.WeightKg, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ProfileResponse.From(profile);
    }

    /// <summary>The first profile save starts the weight history, so progress has a starting point.</summary>
    private async Task SeedFirstWeightAsync(string userId, DateOnly date, double weightKg, CancellationToken cancellationToken)
    {
        if (!await db.WeightEntries.AnyAsync(w => w.UserId == userId && w.Date == date, cancellationToken))
        {
            db.WeightEntries.Add(new WeightEntry
            {
                UserId = userId,
                Date = date,
                WeightKg = weightKg,
                CreatedAtUtc = DateTime.UtcNow,
            });
        }
    }
}
