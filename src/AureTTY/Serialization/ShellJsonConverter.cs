using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AureTTY.Contracts.Enums;

namespace AureTTY.Serialization;

public sealed class ShellJsonConverter : JsonConverter<Shell>
{
    public override Shell Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Number)
        {
            if (!reader.TryGetInt32(out var numericValue))
            {
                throw new JsonException("Shell value must be a valid integer.");
            }

            if (!Enum.IsDefined(typeof(Shell), numericValue))
            {
                throw new JsonException($"Unsupported shell value '{numericValue}'.");
            }

            return (Shell)numericValue;
        }

        if (reader.TokenType is JsonTokenType.String)
        {
            var raw = reader.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new JsonException("Shell value cannot be empty.");
            }

            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericStringValue))
            {
                if (!Enum.IsDefined(typeof(Shell), numericStringValue))
                {
                    throw new JsonException($"Unsupported shell value '{raw}'.");
                }

                return (Shell)numericStringValue;
            }

            if (TryParseShell(raw, out var parsed))
            {
                return parsed;
            }

            throw new JsonException($"Unsupported shell value '{raw}'.");
        }

        throw new JsonException("Shell value must be a string or integer.");
    }

    public override void Write(Utf8JsonWriter writer, Shell value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue((int)value);
    }

    private static bool TryParseShell(string raw, out Shell shell)
    {
        switch (raw.Trim().ToLowerInvariant())
        {
            case "cmd":
                shell = Shell.Cmd;
                return true;
            case "powershell":
                shell = Shell.PowerShell;
                return true;
            case "pwsh":
                shell = Shell.Pwsh;
                return true;
            case "bash":
                shell = Shell.Bash;
                return true;
            case "sh":
            case "ash":
                shell = Shell.Sh;
                return true;
            default:
                shell = default;
                return false;
        }
    }
}
