using System.Text.Json.Serialization;

namespace WasteSubmitRegPackagingApi.Obligations.Models;

[JsonConverter(typeof(JsonStringEnumConverter<SubmitterType>))]
public enum SubmitterType
{
    DirectRegistrant,
    ComplianceScheme
}