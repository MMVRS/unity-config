#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Build1.UnityConfig.Editor.Processors;
using Build1.UnityConfig.Repositories;
using Newtonsoft.Json;
using UnityEngine;

namespace Build1.UnityConfig.Editor.Config
{
    internal sealed class ConfigEditorModel
    {
        public ConfigSettingsEditor Settings { get; private set; }

        public List<string> Configs        { get; private set; }
        public bool         ConfigSelected => SelectedConfig != null;

        public string     SelectedConfigName         { get; private set; }
        public ConfigNode SelectedConfig             { get; private set; }
        public bool       SelectedConfigCanBeSaved   => SelectedConfigName != ConfigSettings.SourceFirebase;
        public bool       SelectedConfigCanBeDeleted => SelectedConfigName != ConfigSettings.SourceFirebase;
        public bool       SelectedConfigIsBaseline   => SelectedConfigName == Settings.BaselineSource;

        public List<string> SelectedConfigSections      { get; private set; }
        public ConfigNode   SelectedConfigSection       { get; private set; }
        public ConfigNode   SelectedConfigSectionBackup { get; private set; }
        public string       SelectedConfigSectionName   { get; private set; }
        public int          SelectedConfigSectionIndex  { get; private set; }

        public bool InProgress { get; private set; }

        public event Action OnReset;
        public event Action OnConfigRemoved;
        public event Action OnSectionChanged;
        public event Action OnSectionReverted;
        public event Action OnSectionSaved;

        public ConfigEditorModel()
        {
            ConfigAssetsPostProcessor.onAssetsPostProcessed += () =>
            {
                if (!InProgress)
                    Reset();
            };

            Reset();
        }

        public void Reset()
        {
            Settings = ConfigProcessor.GetSettings();
            Configs = ConfigProcessor.GetConfigs();

            SelectedConfigName = null;
            SelectedConfig = null;

            UnityConfig.CurrentEditorConfig = null;
            UnityConfig.CurrentEditorConfigSection = null;

            SelectedConfigSections = null;
            SelectedConfigSection = null;
            SelectedConfigSectionBackup = null;
            SelectedConfigSectionName = null;
            SelectedConfigSectionIndex = -1;

            OnReset?.Invoke();
        }

        /*
         * Settings.
         */

        public void TrySaveSettings()
        {
            ConfigProcessor.TrySaveSettings(Settings);
        }

        /*
         * Config.
         */

        public void SelectConfig(string configName, Action onComplete = null)
        {
            InProgress = true;

            var settings = ConfigSettings.FromEditorSettings(Settings);

            LoadConfig(configName, UnityConfig.Instance.ConfigType, settings, config =>
            {
                UnityConfig.CurrentEditorConfig = config;

                SelectedConfig = config;
                SelectedConfigName = configName;

                SelectedConfigSections = GetConfigSections(config);

                var lastSelectedSection = LoadLastSelectedSection();
                var lastSelectedSectionIndex = SelectedConfigSections.IndexOf(lastSelectedSection);
                SelectSection(lastSelectedSectionIndex);

                InProgress = false;

                onComplete?.Invoke();
            }, exception =>
            {
                Debug.LogException(exception);

                InProgress = false;
                onComplete?.Invoke();
            });
        }

        public bool CheckConfigNameValid(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && new Regex(@"^\w+$").IsMatch(name);
        }

        public bool CheckConfigExists(string name)
        {
            var configs = ConfigProcessor.GetConfigs().Select(n => n.ToLower()).ToList();
            return configs.Contains(name) || configs.Contains(name.ToLower());
        }

        public void AddConfig(string name, string sourceConfigName)
        {
            InProgress = true;

            // None.
            if (string.IsNullOrEmpty(sourceConfigName))
            {
                ConfigProcessor.AddConfig(name, "{}", OnConfigAdded);
                return;
            }

            var settings = ConfigSettings.FromEditorSettings(Settings);

            LoadConfig(sourceConfigName, UnityConfig.Instance.ConfigType, settings, config =>
            {
                var json = config.ToJson(true);
                ConfigProcessor.AddConfig(name, json, OnConfigAdded);
            }, OnConfigFail);
        }

        public void RemoveConfig(string name)
        {
            InProgress = true;
            ConfigProcessor.RemoveConfig(name, OnConfigRemovedHandler, OnConfigFail);
        }

        private void OnConfigAdded(string name)
        {
            Reset();
            InProgress = false;
        }

        private void OnConfigRemovedHandler(string name)
        {
            Reset();
            InProgress = false;

            OnConfigRemoved?.Invoke();
        }

        private void OnConfigFail(Exception exception)
        {
            Debug.LogException(exception);

            Reset();
            InProgress = false;
        }

        private static List<string> GetConfigSections(ConfigNode config)
        {
            var type = config.GetType();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            return properties.Select(p => p.Name).Where(n => n != "Metadata").ToList();
        }

        /*
         * Config Loading.
         */

        internal static void LoadConfig(string configName, Type configType, ConfigSettings settings, Action<ConfigNode> onComplete, Action<ConfigException> onError)
        {
            // Firebase.
            if (configName == ConfigSettings.SourceFirebase)
                LoadFirebaseConfig(configType, settings, onComplete, onError);
            else
                LoadLocalConfig(configName, onComplete, onError);
        }

        private static void LoadFirebaseConfig(Type configType, ConfigSettings settings, Action<ConfigNode> onComplete, Action<ConfigException> onError)
        {
            #if BUILD1_CONFIG_FIREBASE_REMOTE_CONFIG_AVAILABLE

            ConfigRepositoryFirebase.LoadInEditor(settings, configType, onComplete, onError);

            #else
            Debug.LogError("Remote Config loading from Firebase is unavailable. Probably you need to add Firebase Remote Config package into the project.");
            onError?.Invoke(new ConfigException(ConfigError.FirebaseRemoteConfigUnavailable));

            #endif
        }

        private static void LoadLocalConfig(string configName, Action<ConfigNode> onComplete, Action<ConfigException> onError)
        {
            var path = ConfigProcessor.GetEditorConfigFilePath(configName);

            string json;

            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                onError?.Invoke(new ConfigException(ConfigError.ResourceNotFound, $"Path: {path}", exception));
                return;
            }

            ConfigNode config;

            try
            {
                config = (ConfigNode)JsonConvert.DeserializeObject(json, UnityConfig.Instance.ConfigType);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                onError?.Invoke(new ConfigException(ConfigError.ParsingError, $"JSON: {json}", exception));
                return;
            }

            onComplete?.Invoke(config);
        }

        /*
         * Section.
         */

        public void SelectSection(int index)
        {
            index = Math.Max(index, 0);

            SelectedConfigSectionBackup = GetSection(SelectedConfig, index, out var sectionName);
            SelectedConfigSection = CloneSection(SelectedConfigSectionBackup);

            UnityConfig.CurrentEditorConfigSection = SelectedConfigSection;

            SelectedConfigSectionName = sectionName;
            SelectedConfigSectionIndex = index;

            SaveLastSelectedSection(sectionName);

            OnSectionChanged?.Invoke();
        }

        public void SaveSection()
        {
            InProgress = true;

            var config = SelectedConfig;
            var property = config.GetType().GetProperty(SelectedConfigSectionName);
            if (property == null)
            {
                Debug.LogError("Property not found: " + SelectedConfigSectionName);
                InProgress = false;
                return;
            }

            switch (Settings.Mode)
            {
                case ConfigMode.Default:

                    config.UpdateMetadata();

                    // When saving in default mode we need to get rid of metadatas from all sections.
                    foreach (var section in SelectedConfigSections)
                        GetSection(config, section).ClearMetadata();

                    break;

                case ConfigMode.Decomposed:
                    config.ClearMetadata();
                    SelectedConfigSection.UpdateMetadata();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            property.SetValue(config, SelectedConfigSection);

            ConfigProcessor.OnSaving(config);

            SelectedConfigSectionBackup = CloneSection(SelectedConfigSection);

            ConfigProcessor.SaveConfig(SelectedConfigName, config.ToJson(true));

            InProgress = false;

            OnSectionSaved?.Invoke();

            ConfigProcessor.OnSaved(config);
        }

        public void RevertSection()
        {
            SelectedConfigSection = CloneSection(SelectedConfigSectionBackup);

            UnityConfig.CurrentEditorConfigSection = SelectedConfigSection;

            OnSectionReverted?.Invoke();
        }

        public bool CheckSelectedSectionModified()
        {
            return SelectedConfigSection != null &&
                   SelectedConfigSection.ToJson(false) != SelectedConfigSectionBackup.ToJson(false);
        }

        public static ConfigNode GetSection(ConfigNode config, string name)
        {
            var type = config.GetType();
            var property = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).First(p => p.Name == name);
            var sectionRaw = property.GetValue(config) ?? Activator.CreateInstance(property.PropertyType);
            return (ConfigNode)sectionRaw;
        }

        public static ConfigNode GetSection(ConfigNode config, int index, out string name)
        {
            var type = config.GetType();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            name = properties[index].Name;

            var property = properties[index];
            var sectionRaw = property.GetValue(config) ?? Activator.CreateInstance(property.PropertyType);
            return (ConfigNode)sectionRaw;
        }

        private ConfigNode CloneSection(ConfigNode node)
        {
            return node == null ? null : ConfigNode.FromJson(node.ToJson(false), node.GetType());
        }

        private static string LoadLastSelectedSection()
        {
            return PlayerPrefs.GetString($"{Application.identifier}_LastSelectedConfigSection");
        }

        private static void SaveLastSelectedSection(string section)
        {
            PlayerPrefs.SetString($"{Application.identifier}_LastSelectedConfigSection", section);
        }
    }
}

#endif