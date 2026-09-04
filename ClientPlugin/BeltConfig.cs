using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace ClientPlugin;

public class BeltConfig
{
    public bool Enabled = true;

    // Torus geometry. Defaults: SDX2 Belt (DX6) — 8,500 km central ring on Sol, 500 km tube,
    // in the Y=0 ecliptic (all SDX gas-giant zones sit at Y=0).
    public double CenterX = 0;
    public double CenterY = 0;
    public double CenterZ = 0;
    public double MajorRadius = 8500000;
    public double MinorRadius = 500000;
    public double NormalX = 0;
    public double NormalY = 1;
    public double NormalZ = 0;

    // Wireframe density.
    public int ToroidalRings = 6;
    public int ToroidalSegments = 256;
    public int PoloidalCircles = 36;
    public int PoloidalSegments = 28;

    // Line look. Thickness is angular: meters of width per meter of camera distance,
    // clamped to [MinThickness, MaxThickness]. 0.0015 is roughly one pixel at 1080p.
    public float ThicknessScale = 0.0015f;
    public float MinThickness = 40f;
    public float MaxThickness = 15000f;
    public byte ColorR = 222;
    public byte ColorG = 170;
    public byte ColorB = 90;
    public byte ColorA = 40;
    public string Material = "Square";
    public string Blend = "AdditiveBottom";

    // Segments closer to the camera than FadeFull fade out, fully gone by FadeNear —
    // stops the wireframe from filling the screen while flying inside the Belt.
    public double FadeNearMeters = 30000;
    public double FadeFullMeters = 300000;

    // Worlds the torus draws in (case-insensitive exact session names). Empty = all.
    public List<string> Worlds = new List<string> { "Expanse" };

    public static string ConfigPath
    {
        get
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "SpaceEngineers", "Storage", "SkylarkBelt.cfg");
        }
    }

    public static BeltConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var serializer = new XmlSerializer(typeof(BeltConfig));
                using (var reader = new StreamReader(ConfigPath))
                {
                    return (BeltConfig)serializer.Deserialize(reader);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log($"Failed to load config, using defaults: {ex.Message}");
        }
        var config = new BeltConfig();
        config.Save();
        return config;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            var serializer = new XmlSerializer(typeof(BeltConfig));
            using (var writer = new StreamWriter(ConfigPath))
            {
                serializer.Serialize(writer, this);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log($"Failed to save config: {ex.Message}");
        }
    }
}
