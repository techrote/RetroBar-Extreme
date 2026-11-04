using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using RetroBar.Utilities;

internal static partial class Program
{
    public sealed class StoredSettings : IMigratableSettings
    {
        public string Value { get; set; } = "default";
        public bool Migrate { get; set; }
        [JsonIgnore] public bool MigrationPerformed => Migrate;
    }

    public sealed class FailingMigrationSettings : IMigratableSettings
    {
        public bool Fail { get; set; }
        public bool MigrationPerformed => Fail ? throw new InvalidOperationException("Rejected migration") : false;
    }

    private static void SettingsPersistence()
    {
        string folder = Path.Combine(Path.GetTempPath(), "retrobar-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "settings.json");
            var defaults = new StoredSettings();
            var manager = new SettingsManager<StoredSettings>(path, defaults);
            Check(ReferenceEquals(manager.Settings, defaults) && !File.Exists(path), "missing settings retain defaults without writing");

            foreach (string invalid in new[] { "null", "{", "[]", "{\"Value\":42}" })
            {
                File.WriteAllText(path, invalid);
                manager = new SettingsManager<StoredSettings>(path, defaults);
                Check(ReferenceEquals(manager.Settings, defaults), "rejected settings retain defaults: " + invalid);
                Check(File.ReadAllText(path) == invalid, "loading rejected settings preserves the original file");
            }

            string migrationPath = Path.Combine(folder, "migration.json");
            File.WriteAllText(migrationPath, "{\"Fail\":true}");
            var migrationDefaults = new FailingMigrationSettings();
            var failedMigration = new SettingsManager<FailingMigrationSettings>(migrationPath, migrationDefaults);
            Check(ReferenceEquals(failedMigration.Settings, migrationDefaults), "failed migration inspection retains defaults");
            Check(File.ReadAllText(migrationPath) == "{\"Fail\":true}", "failed migration does not rewrite its source");

            File.WriteAllText(path, "{\"Value\":\"loaded\",\"Migrate\":true,\"obsolete\":1}");
            manager = new SettingsManager<StoredSettings>(path, defaults);
            Check(manager.Settings.Value == "loaded", "valid settings load");
            Check(!File.ReadAllText(path).Contains("obsolete"), "successful migration publishes the migrated document");

            manager.Settings = new StoredSettings { Value = "saved £ 字" };
            var saved = new SettingsManager<StoredSettings>(path, defaults);
            Check(saved.Settings.Value == "saved £ 字", "saved settings round trip");
            Check(!File.ReadAllBytes(path).Take(3).SequenceEqual(new byte[] { 0xef, 0xbb, 0xbf }), "settings retain UTF-8 without a BOM");

            // Deny replacement after the new file has been written. The last good file
            // must survive and the unique staging file must be cleaned up.
            string previous = File.ReadAllText(path);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                manager.Settings = new StoredSettings { Value = "blocked save" };
            }
            Check(File.ReadAllText(path) == previous, "failed replacement preserves last saved contents");
            Check(Directory.GetFiles(folder, "*.tmp").Length == 0, "failed replacement cleans temporary files");
            manager.Settings = new StoredSettings { Value = "retry" };
            Check(new SettingsManager<StoredSettings>(path, defaults).Settings.Value == "retry", "save succeeds after the sharing lock is released");

            string firstSave = Path.Combine(folder, "nested", "settings.json");
            var fresh = new SettingsManager<StoredSettings>(firstSave, defaults);
            fresh.Settings = new StoredSettings { Value = "first save" };
            Check(new SettingsManager<StoredSettings>(firstSave, defaults).Settings.Value == "first save", "first save creates the directory and complete document");

            bool rejected = false;
            try { manager.Settings = null; } catch (ArgumentNullException) { rejected = true; }
            Check(rejected && manager.Settings.Value == "retry", "null assignment cannot erase current settings");
            Check(Directory.GetFiles(folder, "*.tmp", SearchOption.AllDirectories).Length == 0, "successful saves leave no temporary files");
        }
        finally { Directory.Delete(folder, true); }
    }

    // All fixture mutations stay in memory, including JSON setters. Accessing Instance
    // first allows its normal startup to complete, then autosave is suppressed for the scope.
    private static void WithSettingsAutosaveSuppressed(Action<Type, object> action)
    {
        Type type = typeof(SettingsManager<>).Assembly.GetType("RetroBar.Utilities.Settings", throwOnError: true);
        object settings = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        FieldInfo initializing = type.GetField("_isInitializing", BindingFlags.NonPublic | BindingFlags.Static);
        object previous = initializing.GetValue(null);
        initializing.SetValue(null, true);
        try { action(type, settings); }
        finally { initializing.SetValue(null, previous); }
    }

    private static void NullSettings()
    {
        WithSettingsAutosaveSuppressed((type, _) =>
        {
            // Exercise the real JSON setters without replacing the process-wide singleton.
            const string json = "{\"Theme\":null,\"Language\":null,\"QuickLaunchPath\":null,\"ClockFormat\":null,\"AMDesignator\":null,\"PMDesignator\":null,\"InvertNotifyIcons\":null,\"NotifyIconBehaviors\":null,\"NotifyIconOrder\":null,\"QuickLaunchOrder\":null,\"PinnedNotifyIcons\":null}";
            object defaults = Activator.CreateInstance(type);
            object loaded = JsonSerializer.Deserialize(json, type);
            foreach (string name in new[] { "Theme", "Language", "QuickLaunchPath", "ClockFormat", "AMDesignator", "PMDesignator" })
            {
                PropertyInfo property = type.GetProperty(name);
                Check(Equals(property.GetValue(loaded), property.GetValue(defaults)), name + " keeps its default for JSON null");
                property.SetValue(loaded, "valid");
                Check(Equals(property.GetValue(loaded), "valid"), name + " remains editable after JSON null");
            }
            foreach (string name in new[] { "InvertNotifyIcons", "NotifyIconBehaviors", "NotifyIconOrder", "QuickLaunchOrder" })
                Check(type.GetProperty(name).GetValue(loaded) != null, name + " remains a usable collection");

            foreach (var (name, valid) in new[]
            {
                ("InvertNotifyIcons", "\"keep\""),
                ("NotifyIconOrder", "\"keep\""),
                ("QuickLaunchOrder", "\"keep\""),
                ("QuickLaunchIcons", "{}"),
                ("TrayIconReplacements", "{}"),
                ("TaskRowRules", "{\"Row\":1}"),
                ("TaskButtonAppRules", "{}"),
            })
            {
                object withEntries = JsonSerializer.Deserialize("{\"" + name + "\":[null," + valid + ",null]}", type);
                var entries = (System.Collections.IList)type.GetProperty(name).GetValue(withEntries);
                Check(entries.Count == 1 && entries[0] != null, name + " discards null entries while retaining its valid entry");
            }
            bool rejectedRecord = false;
            try { JsonSerializer.Deserialize("{\"NotifyIconBehaviors\":[null]}", type); }
            catch (JsonException) { rejectedRecord = true; }
            Check(rejectedRecord, "null value-type icon records reject the document instead of entering the live list");
            object legacy = JsonSerializer.Deserialize("{\"PinnedNotifyIcons\":[null,\"keep\"]}", type);
            var migrated = (System.Collections.IList)type.GetProperty("NotifyIconBehaviors").GetValue(legacy);
            Check(migrated.Count == 1, "legacy pinned icons skip null entries during migration");
        });
        string profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RetroBar", "settings.json");
        Check(!File.Exists(profile), "null settings fixture never writes the user profile");
    }
}
