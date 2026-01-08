using Newtonsoft.Json;

public class NewtonsoftJsonSerializer : IJsonSerializer
{
    private static readonly JsonSerializerSettings Settings =
        new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

    public string Serialize<T>(T data)
    {
        return JsonConvert.SerializeObject(data, Settings);
    }

    public T Deserialize<T>(string json)
    {
        return JsonConvert.DeserializeObject<T>(json, Settings);
    }
}
