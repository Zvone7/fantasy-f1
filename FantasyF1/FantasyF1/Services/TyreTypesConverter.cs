using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using FantasyF1.Models;

namespace FantasyF1.Services;

public class TyreTypesConverter : JsonConverter<TyreType>
{
    public override TyreType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string value = reader.GetString()?.ToLower(); // Convert to lowercase for case-insensitive comparison

        return value switch
        {
            "soft" => TyreType.Soft,
            "medium" => TyreType.Medium,
            "hard" => TyreType.Hard,
            "wet" => TyreType.Wet,
            "wet2" => TyreType.Wet2,
            _ => throw new JsonException($"Invalid Fp1Tyres value: {value}")
        };
    }

    public override void Write(Utf8JsonWriter writer, TyreType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString().ToLower()); // Write enum value as lowercase string
    }
}