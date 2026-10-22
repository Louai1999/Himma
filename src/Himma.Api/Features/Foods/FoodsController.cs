using Himma.Api.Data;
using Himma.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Foods;

[Route("api/foods")]
public sealed class FoodsController(AppDbContext db) : HimmaControllerBase
{
    private const int MaxResultsPerSource = 50;
    private const int MaxSearchLength = 60;

    /// <summary>The user's own custom foods first, then system foods.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FoodDto>>> Search([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var term = search?.Trim() ?? string.Empty;
        if (term.Length > MaxSearchLength)
        {
            return InvalidField("search", "نص البحث طويل جداً.");
        }

        var userId = UserId;
        var customQuery = db.CustomFoods.AsNoTracking().Where(f => f.UserId == userId);
        var systemQuery = db.Foods.AsNoTracking();
        if (term.Length > 0)
        {
            customQuery = customQuery.Where(f => f.Name.Contains(term));
            systemQuery = systemQuery.Where(f => f.Name.Contains(term));
        }

        var custom = await customQuery.OrderBy(f => f.Name).Take(MaxResultsPerSource).ToListAsync(cancellationToken);
        var system = await systemQuery.OrderBy(f => f.Name).Take(MaxResultsPerSource).ToListAsync(cancellationToken);

        return custom.Select(FoodDto.From).Concat(system.Select(FoodDto.From)).ToList();
    }
}
