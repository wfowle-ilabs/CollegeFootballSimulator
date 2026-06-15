using Godot;

namespace CollegeFootballSimGoDot;

/// <summary>Lightweight persisted UI preferences, stored in <c>user://settings.cfg</c>.</summary>
public static class AppSettings
{
    private const string Path = "user://settings.cfg";
    private const string Section = "ui";

    /// <summary>Whether to confirm before simming a week (the "Don't ask again" toggle clears this).</summary>
    public static bool ConfirmSimWeek
    {
        get => GetBool("confirm_sim_week", true);
        set => SetBool("confirm_sim_week", value);
    }

    private static bool GetBool(string key, bool fallback)
    {
        var cfg = new ConfigFile();
        cfg.Load(Path); // missing file → defaults below
        return cfg.GetValue(Section, key, fallback).AsBool();
    }

    private static void SetBool(string key, bool value)
    {
        var cfg = new ConfigFile();
        cfg.Load(Path);
        cfg.SetValue(Section, key, value);
        cfg.Save(Path);
    }
}
