using Himma.Api.Infrastructure.Http;
using Himma.Api.Infrastructure.Validation;
using Microsoft.AspNetCore.Mvc;

namespace Himma.Api.Features.Tips;

public sealed record TipDto(DateOnly Date, string Text);

[Route("api/tips")]
public sealed class TipsController : HimmaControllerBase
{
    /// <summary>The tip for the user's local <paramref name="date"/> (defaults to today in UTC).</summary>
    [HttpGet("today")]
    public ActionResult<TipDto> Today([FromQuery] string? date)
    {
        var day = ApiDate.UtcToday;
        if (date is not null && !ApiDate.TryParse(date, out day))
        {
            return InvalidField("date", ApiDate.InvalidMessage);
        }

        return new TipDto(day, DailyTips.For(day));
    }
}
