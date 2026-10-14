using System.Text.Json.Serialization;

namespace WasteSubmitRegPackagingApi.Obligations.Models;

[JsonConverter(typeof(JsonStringEnumConverter<EventAction>))]
public enum EventAction
{
    Joined,
    Left
}
