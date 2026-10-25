using Himma.Api.Domain.Strength;
using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Himma.Api.Features.Workouts;

[Route("api")]
public sealed class RecordsController(RecordsService records) : HimmaControllerBase
{
    [HttpGet("workouts/records")]
    public async Task<IReadOnlyList<PersonalRecordDto>> All(CancellationToken cancellationToken) =>
        await records.AllAsync(UserId, cancellationToken);

    [HttpGet("workouts/sessions/{id:int}/records")]
    public async Task<ActionResult<IReadOnlyList<NewRecordDto>>> NewInSession(int id, CancellationToken cancellationToken)
    {
        var result = await records.NewInSessionAsync(UserId, id, cancellationToken);
        return result is null ? NotFoundProblem() : Ok(result);
    }

    /// <summary>Estimated 1RM and a load chart from one set (weight × reps).</summary>
    [HttpGet("strength/one-rep-max")]
    public ActionResult<OneRepMaxEstimate> OneRepMaxEstimate([FromQuery] double? weightKg, [FromQuery] int? reps)
    {
        if (weightKg is not (> 0 and <= 500))
        {
            return InvalidField("weightKg", Lang.T("الوزن بين 0 و 500 كغ.", "Weight must be between 0 and 500 kg."));
        }
        if (reps is not (>= 1 and <= OneRepMax.MaxReps))
        {
            return InvalidField("reps", Lang.T("التكرارات بين 1 و 30.", "Reps must be between 1 and 30."));
        }
        return OneRepMax.Calculate(weightKg.Value, reps.Value);
    }
}
