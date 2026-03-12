using System;
using System.IO;
using System.Text.Json;
using SlotTheory.Core;

namespace SlotTheory.Tools;

public static class SpectacleTuningLoader
{
    public static bool TryLoadFromFile(string path, out SpectacleTuningProfile profile, out string error)
    {
        profile = new SpectacleTuningProfile();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "No tuning file path was provided.";
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            return TryLoadFromJson(json, out profile, out error);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryLoadFromJson(string json, out SpectacleTuningProfile profile, out string error)
    {
        profile = new SpectacleTuningProfile();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Tuning JSON is empty.";
            return false;
        }

        try
        {
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };
            SpectacleTuningProfile? parsed = JsonSerializer.Deserialize<SpectacleTuningProfile>(json, opts);
            if (parsed == null)
            {
                error = "Deserialized tuning profile was null.";
                return false;
            }

            profile = parsed.CloneNormalized();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
