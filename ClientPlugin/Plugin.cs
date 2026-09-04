using System;
using System.Collections.Generic;
using System.IO;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Plugins;
using VRage.Utils;
using VRageMath;
using BlendType = VRageRender.MyBillboard.BlendTypeEnum;

namespace ClientPlugin;

// Client-side Pulsar plugin that draws a true wireframe torus for the SDX2 Belt instance
// (DX6) using the same transparent-geometry line primitive the NavMarkers mod draws its
// zone spheres with. Pure rendering — it touches nothing server-side.
//
// ReSharper disable once UnusedType.Global
public class Plugin : IPlugin
{
    public const string Name = "SkylarkBelt";
    public const string Version = "1.0.0";

    private BeltConfig config;
    private bool sessionInit;
    private bool chatHooked;
    private bool drawThisWorld;
    private int consecutiveErrors;
    private bool disabledByErrors;

    // One entry per polyline; each is a closed loop (last point equals first).
    private readonly List<Vector3D[]> polylines = new List<Vector3D[]>();
    private MyStringId lineMaterial;
    private BlendType blendType;
    private Vector4 lineColor;

    public void Init(object gameInstance)
    {
        Log($"v{Version} initialized, waiting for session");
    }

    public void Update()
    {
        try
        {
            if (disabledByErrors)
                return;

            if (MyAPIGateway.Session == null || MyAPIGateway.Utilities == null)
            {
                if (sessionInit)
                    EndSession();
                return;
            }

            if (MySession.Static == null || !MySession.Static.Ready)
                return;

            if (!sessionInit)
                BeginSession();

            if (!drawThisWorld || config == null || !config.Enabled)
                return;

            if (MyAPIGateway.Session.Config != null && MyAPIGateway.Session.Config.HudState == 0)
                return;

            var camera = MyAPIGateway.Session.Camera;
            if (camera == null)
                return;

            DrawTorus(camera.WorldMatrix.Translation);
            consecutiveErrors = 0;
        }
        catch (Exception ex)
        {
            consecutiveErrors++;
            Log($"Update failed ({consecutiveErrors}): {ex}");
            if (consecutiveErrors > 10)
            {
                disabledByErrors = true;
                Log("Too many consecutive errors — plugin disabled for this game run");
            }
        }
    }

    public void Dispose()
    {
        UnhookChat();
    }

    private void BeginSession()
    {
        config = BeltConfig.Load();
        RebuildRenderState();
        drawThisWorld = WorldMatches();
        HookChat();
        sessionInit = true;
        Log($"session '{MyAPIGateway.Session.Name}' — belt {(drawThisWorld ? "active" : "inactive (world not in list)")}");
    }

    private void EndSession()
    {
        UnhookChat();
        sessionInit = false;
        drawThisWorld = false;
    }

    private void HookChat()
    {
        if (!chatHooked && MyAPIGateway.Utilities != null)
        {
            MyAPIGateway.Utilities.MessageEnteredSender += OnMessageEntered;
            chatHooked = true;
        }
    }

    private void UnhookChat()
    {
        if (chatHooked && MyAPIGateway.Utilities != null)
        {
            MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEntered;
            chatHooked = false;
        }
    }

    private bool WorldMatches()
    {
        if (config.Worlds == null || config.Worlds.Count == 0)
            return true;
        string raw = MyAPIGateway.Session.Name ?? "";
        string stripped = string.Concat(raw.Split(Path.GetInvalidFileNameChars()));
        foreach (string world in config.Worlds)
        {
            string w = (world ?? "").Trim();
            if (w.Length == 0)
                continue;
            if (string.Equals(w, raw, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(w, stripped, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private void RebuildRenderState()
    {
        lineMaterial = MyStringId.GetOrCompute(string.IsNullOrWhiteSpace(config.Material) ? "Square" : config.Material);
        blendType = ParseBlend(config.Blend);
        lineColor = new Vector4(config.ColorR / 255f, config.ColorG / 255f, config.ColorB / 255f, config.ColorA / 255f);
        BuildGeometry();
    }

    private static BlendType ParseBlend(string value)
    {
        BlendType parsed;
        if (Enum.TryParse(value, true, out parsed))
            return parsed;
        return BlendType.AdditiveBottom;
    }

    private void BuildGeometry()
    {
        polylines.Clear();

        Vector3D center = new Vector3D(config.CenterX, config.CenterY, config.CenterZ);
        Vector3D normal = new Vector3D(config.NormalX, config.NormalY, config.NormalZ);
        if (normal.LengthSquared() < 1e-9)
            normal = Vector3D.Up;
        normal.Normalize();

        Vector3D seed = Math.Abs(normal.X) < 0.9 ? Vector3D.UnitX : Vector3D.UnitZ;
        Vector3D u = Vector3D.Normalize(Vector3D.Cross(normal, seed));
        Vector3D v = Vector3D.Cross(normal, u);

        double R = config.MajorRadius;
        double r = config.MinorRadius;

        int toroidalRings = MathHelper.Clamp(config.ToroidalRings, 1, 32);
        int toroidalSegments = MathHelper.Clamp(config.ToroidalSegments, 16, 1024);
        int poloidalCircles = MathHelper.Clamp(config.PoloidalCircles, 0, 360);
        int poloidalSegments = MathHelper.Clamp(config.PoloidalSegments, 8, 128);

        for (int j = 0; j < toroidalRings; j++)
        {
            double phi = 2 * Math.PI * j / toroidalRings;
            double ringRadius = R + r * Math.Cos(phi);
            Vector3D lift = normal * (r * Math.Sin(phi));
            var points = new Vector3D[toroidalSegments + 1];
            for (int k = 0; k <= toroidalSegments; k++)
            {
                double theta = 2 * Math.PI * k / toroidalSegments;
                points[k] = center + (u * Math.Cos(theta) + v * Math.Sin(theta)) * ringRadius + lift;
            }
            polylines.Add(points);
        }

        for (int i = 0; i < poloidalCircles; i++)
        {
            double theta = 2 * Math.PI * i / poloidalCircles;
            Vector3D radial = u * Math.Cos(theta) + v * Math.Sin(theta);
            var points = new Vector3D[poloidalSegments + 1];
            for (int k = 0; k <= poloidalSegments; k++)
            {
                double phi = 2 * Math.PI * k / poloidalSegments;
                points[k] = center + radial * (R + r * Math.Cos(phi)) + normal * (r * Math.Sin(phi));
            }
            polylines.Add(points);
        }
    }

    private void DrawTorus(Vector3D cameraPosition)
    {
        double fadeNear = config.FadeNearMeters;
        double fadeFull = Math.Max(config.FadeFullMeters, fadeNear + 1);

        for (int p = 0; p < polylines.Count; p++)
        {
            Vector3D[] points = polylines[p];
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3D a = points[i];
                Vector3D b = points[i + 1];
                Vector3D mid = (a + b) * 0.5;
                double distance = Vector3D.Distance(mid, cameraPosition);

                float fade = 1f;
                if (distance < fadeFull)
                {
                    fade = (float)((distance - fadeNear) / (fadeFull - fadeNear));
                    if (fade <= 0f)
                        continue;
                }

                float thickness = (float)MathHelperD.Clamp(distance * config.ThicknessScale, config.MinThickness, config.MaxThickness);
                Vector4 color = lineColor * fade;
                MySimpleObjectDraw.DrawLine(a, b, lineMaterial, ref color, thickness, blendType);
            }
        }
    }

    private void OnMessageEntered(ulong sender, string messageText, ref bool sendToOthers)
    {
        if (messageText == null)
            return;
        string text = messageText.Trim();
        if (!text.StartsWith("/belt", StringComparison.OrdinalIgnoreCase))
            return;
        sendToOthers = false;

        string[] args = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string command = args.Length > 1 ? args[1].ToLowerInvariant() : "toggle";

        switch (command)
        {
            case "toggle":
                config.Enabled = !config.Enabled;
                config.Save();
                Message($"Belt torus {(config.Enabled ? "on" : "off")}");
                break;
            case "on":
                config.Enabled = true;
                config.Save();
                Message("Belt torus on");
                break;
            case "off":
                config.Enabled = false;
                config.Save();
                Message("Belt torus off");
                break;
            case "reload":
                config = BeltConfig.Load();
                RebuildRenderState();
                drawThisWorld = WorldMatches();
                Message($"Config reloaded — belt {(drawThisWorld && config.Enabled ? "active" : "inactive")} in this world");
                break;
            case "here":
                ToggleCurrentWorld();
                break;
            case "scale":
                if (args.Length > 2)
                {
                    float scale;
                    if (float.TryParse(args[2], out scale) && scale > 0f && scale < 1f)
                    {
                        config.ThicknessScale = scale;
                        config.Save();
                        Message($"Thickness scale = {scale}");
                    }
                    else
                        Message("Usage: /belt scale 0.0015  (meters of width per meter of distance)");
                }
                else
                    Message($"Thickness scale = {config.ThicknessScale} — set with /belt scale <value>");
                break;
            case "alpha":
                if (args.Length > 2)
                {
                    byte alpha;
                    if (byte.TryParse(args[2], out alpha))
                    {
                        config.ColorA = alpha;
                        config.Save();
                        RebuildRenderState();
                        Message($"Alpha = {alpha}");
                    }
                    else
                        Message("Usage: /belt alpha 0-255");
                }
                else
                    Message($"Alpha = {config.ColorA} — set with /belt alpha <0-255>");
                break;
            case "color":
                if (args.Length > 2)
                {
                    byte r, g, b, a;
                    bool hasAlpha;
                    if (TryParseHexColor(args[2], out r, out g, out b, out a, out hasAlpha))
                    {
                        config.ColorR = r;
                        config.ColorG = g;
                        config.ColorB = b;
                        if (hasAlpha)
                            config.ColorA = a;
                        config.Save();
                        RebuildRenderState();
                        Message(hasAlpha
                            ? $"Color = #{r:X2}{g:X2}{b:X2}, alpha = {a}"
                            : $"Color = #{r:X2}{g:X2}{b:X2}");
                    }
                    else
                        Message("Usage: /belt color #5EF10D  (6 hex digits, or 8 for #RRGGBBAA)");
                }
                else
                    Message($"Color = {CurrentColorHex()} — set with /belt color #RRGGBB");
                break;
            case "info":
                ShowInfo();
                break;
            default:
                Message("Commands: /belt (toggle), on, off, info, reload, here, color <#hex>, scale <v>, alpha <0-255>");
                break;
        }
    }

    private void ToggleCurrentWorld()
    {
        string world = MyAPIGateway.Session.Name ?? "";
        if (config.Worlds == null)
            config.Worlds = new List<string>();
        int existing = config.Worlds.FindIndex(w => string.Equals(w, world, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            config.Worlds.RemoveAt(existing);
            Message($"Removed '{world}' from belt worlds");
        }
        else
        {
            config.Worlds.Add(world);
            Message($"Added '{world}' to belt worlds");
        }
        config.Save();
        drawThisWorld = WorldMatches();
    }

    private void ShowInfo()
    {
        Vector3D center = new Vector3D(config.CenterX, config.CenterY, config.CenterZ);
        Vector3D normal = Vector3D.Normalize(new Vector3D(config.NormalX, config.NormalY, config.NormalZ));
        Vector3D camera = MyAPIGateway.Session.Camera != null
            ? MyAPIGateway.Session.Camera.WorldMatrix.Translation
            : Vector3D.Zero;

        Vector3D offset = camera - center;
        double height = Vector3D.Dot(offset, normal);
        Vector3D inPlane = offset - normal * height;
        double radial = inPlane.Length();
        double fromCenterline = Math.Sqrt((radial - config.MajorRadius) * (radial - config.MajorRadius) + height * height);
        bool inside = fromCenterline <= config.MinorRadius;

        Message($"Ring {config.MajorRadius / 1000:N0} km, tube {config.MinorRadius / 1000:N0} km, world draw: {(drawThisWorld && config.Enabled ? "on" : "off")}");
        Message($"You are {fromCenterline / 1000:N0} km from the Belt centerline ({(inside ? "inside" : "outside")} the tube)");
    }

    private string CurrentColorHex()
    {
        return $"#{config.ColorR:X2}{config.ColorG:X2}{config.ColorB:X2}";
    }

    // Parses "#RRGGBB" or "#RRGGBBAA" (the leading # is optional). Alpha is only reported
    // via hasAlpha when 8 digits are supplied, so a 6-digit value leaves alpha unchanged.
    private static bool TryParseHexColor(string text, out byte r, out byte g, out byte b, out byte a, out bool hasAlpha)
    {
        r = 0;
        g = 0;
        b = 0;
        a = 255;
        hasAlpha = false;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        string hex = text.Trim();
        if (hex.StartsWith("#"))
            hex = hex.Substring(1);
        if (hex.Length != 6 && hex.Length != 8)
            return false;

        for (int i = 0; i < hex.Length; i++)
        {
            char c = hex[i];
            bool isHexDigit = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!isHexDigit)
                return false;
        }

        try
        {
            r = Convert.ToByte(hex.Substring(0, 2), 16);
            g = Convert.ToByte(hex.Substring(2, 2), 16);
            b = Convert.ToByte(hex.Substring(4, 2), 16);
            if (hex.Length == 8)
            {
                a = Convert.ToByte(hex.Substring(6, 2), 16);
                hasAlpha = true;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void Message(string text)
    {
        if (MyAPIGateway.Utilities != null)
            MyAPIGateway.Utilities.ShowMessage("Belt", text);
    }

    public static void Log(string text)
    {
        try
        {
            if (MyLog.Default != null)
                MyLog.Default.WriteLineAndConsole($"{Name}: {text}");
        }
        catch
        {
        }
    }
}
