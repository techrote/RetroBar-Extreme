using ManagedShell.Common.Helpers;
using ManagedShell.Common.Logging;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace RetroBar.Utilities
{
    public class SettingsManager<T> : INotifyPropertyChanged where T : IMigratableSettings
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private static readonly JsonSerializerOptions options = new()
        {
            IgnoreReadOnlyProperties = true,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        private readonly string _fileName;

        private T _settings;
        public T Settings
        {
            get => _settings;
            set
            {
                if (value is null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _settings = value;
                OnPropertyChanged();
                SaveToFile();
            }
        }

        public SettingsManager(string fileName, T defaultSettings)
        {
            if (defaultSettings is null)
            {
                throw new ArgumentNullException(nameof(defaultSettings));
            }

            _fileName = fileName;
            _settings = defaultSettings;

            if (!LoadFromFile())
            {
                ShellLogger.Info("SettingsManager: Using default settings");
            }
        }

        private bool LoadFromFile()
        {
            try
            {
                if (!ShellHelper.Exists(_fileName))
                {
                    return false;
                }

                string jsonString = File.ReadAllText(_fileName);
                T loaded = JsonSerializer.Deserialize<T>(jsonString);
                if (loaded is null)
                {
                    throw new JsonException("The settings file must contain a settings object, not null.");
                }

                // Do not replace the defaults until deserialization and migration inspection
                // both succeed. A rejected document must not leave partially loaded state.
                bool migrationPerformed = loaded.MigrationPerformed;
                _settings = loaded;

                if (migrationPerformed)
                {
                    // Save post-migration state so that we don't need to migrate every startup
                    SaveToFile();
                }

                return true;
            }
            catch (Exception ex)
            {
                ShellLogger.Error($"SettingsManager: Error loading settings file: {ex.Message}");
                return false;
            }
        }

        private void SaveToFile()
        {
            string temporary = null;
            try
            {
                string jsonString = JsonSerializer.Serialize(Settings, options);
                string path = Path.GetFullPath(_fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                // Write beside the destination so publication stays on the same volume.
                // Never truncate the last saved settings before the replacement is ready.
                temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
                    {
                        writer.Write(jsonString);
                    }
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(path))
                {
                    File.Replace(temporary, path, null);
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            catch (Exception ex)
            {
                ShellLogger.Error($"SettingsManager: Error saving settings file: {ex.Message}");
            }
            finally
            {
                if (temporary != null)
                {
                    try { File.Delete(temporary); }
                    catch (IOException ex) { ShellLogger.Warning($"SettingsManager: Could not remove temporary settings file: {ex.Message}"); }
                    catch (UnauthorizedAccessException ex) { ShellLogger.Warning($"SettingsManager: Could not remove temporary settings file: {ex.Message}"); }
                }
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public interface IMigratableSettings
    {
        public bool MigrationPerformed { get; }
    }
}
