using System;
using Verse;
using XmlExtensions;
using XmlExtensions.Action;

namespace CustomInfestations; 

public static class Extensions {
    public static T? ParseAs<T>(this string str) where T : struct {
        return GenericHelper<T>.TryParse(str, out var value) ? value : default(T?);
    }
    public static T ParseAs<T>(this string str, T defaultValue) {
        return GenericHelper<T>.TryParse(str, out var value) ? value : defaultValue;
    }

    private static class GenericHelper<T> {
        public delegate bool TryParseFunc(string str, out T result);

        static TryParseFunc _tryParse;
        public static TryParseFunc TryParse {
            get {
                return _tryParse ??= Delegate.CreateDelegate(
                    typeof(TryParseFunc),
                    typeof(T),
                    "TryParse"
                ) as TryParseFunc;
            }
        }
    }
}

[StaticConstructorOnStartup]
static class Settings {

    const string ModIdentifier = "whitespacekilla.infestations";

    delegate bool TryParseHandler<T>(string value, out T result);
    
    static string GetDefaultSetting(string key) {
        try
        {
            return SettingsManager.GetDefaultValue(ModIdentifier, key);
        }
        catch
        {
            return (string) null;
        }
    }
    
    static T SafeGetSetting<T>(string settingName) where T : struct {
        var attemptedValue = SettingsManager.GetSetting(ModIdentifier, settingName);
        var attemptedDefaultValue = GetDefaultSetting(settingName);
        var defaultValue = attemptedDefaultValue.ParseAs<T>() ?? new T();
        var value = attemptedValue.ParseAs<T>(defaultValue);
        return value;
    }

    public static bool ForbidOnBuildings { get; private set; }
    public static int MinimumDistanceToColonyBuilding { get; private set; }
    public static int SmallestClosedArea { get; private set; }
    public static float MaximumLightAttenuation { get; private set; }
    public static float BrightestLightClamp { get; private set; }
    public static float PossibleTemperatureRangeMin { get; private set; }
    public static float PossibleTemperatureRangeMax { get; private set; }
    public static float SafeTemperatureRangeMin { get; private set; }
    public static float SafeTemperatureRangeMax { get; private set; }
    public static float MinimumScore { get; private set; }

    public static void Initialize() {
        ForbidOnBuildings = SafeGetSetting<bool>("ForbidOnBuildings");
        MinimumDistanceToColonyBuilding = SafeGetSetting<int>("MinimumDistanceToColonyBuilding");
        SmallestClosedArea = SafeGetSetting<int>("SmallestClosedArea");
        MaximumLightAttenuation = SafeGetSetting<float>("MaximumLightAttenuation");
        BrightestLightClamp = SafeGetSetting<float>("BrightestLightClamp");
        PossibleTemperatureRangeMin = SafeGetSetting<float>("PossibleTemperatureRangeMin");
        PossibleTemperatureRangeMax = SafeGetSetting<float>("PossibleTemperatureRangeMax");
        SafeTemperatureRangeMin = SafeGetSetting<float>("SafeTemperatureRangeMin");
        SafeTemperatureRangeMax = SafeGetSetting<float>("SafeTemperatureRangeMax");
        MinimumScore = SafeGetSetting<float>("MinimumScore");
    }

    static Settings() {
        Initialize();
    }
}

public class ResetSettings : ActionContainer {
    protected override bool ApplyAction() {
        Settings.Initialize();
        return base.ApplyAction();
    }
}
