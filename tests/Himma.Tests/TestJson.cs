using System.Text.Json;
using System.Text.Json.Serialization;

namespace Himma.Tests;

/// <summary>Same JSON rules as the API: web defaults plus enums as camelCase strings.</summary>
internal static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };
}
