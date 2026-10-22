using Himma.Api.Data;
using Himma.Api.Domain.Entities;
using Himma.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Himma.Api.Features.Foods;

[Route("api/customfoods")]
public sealed class CustomFoodsController(AppDbContext db) : HimmaControllerBase
{
    [HttpPost]
    public async Task<ActionResult<FoodDto>> Create(CustomFoodRequest request, CancellationToken cancellationToken)
    {
        var food = new CustomFood
        {
            UserId = UserId,
            Name = request.Name.Trim(),
            KcalPer100g = request.KcalPer100g!.Value,
            ProteinPer100g = request.ProteinPer100g!.Value,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.CustomFoods.Add(food);
        await db.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, FoodDto.From(food));
    }

    /// <summary>Meals already logged from this food keep their copied name and values.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = UserId;
        var deleted = await db.CustomFoods
            .Where(f => f.Id == id && f.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? NotFoundProblem() : NoContent();
    }
}
