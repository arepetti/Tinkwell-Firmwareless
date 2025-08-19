using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tinkkwell.Firmwareless;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options;

    static JsonDefaults()
    {
        Options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Options.Converters.Add(new JsonStringEnumConverter());

    }
}
