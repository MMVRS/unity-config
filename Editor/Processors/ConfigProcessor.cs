#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Build1.UnityConfig.Editor.Processors
{
    [InitializeOnLoad]
    internal static class ConfigProcessor
    {
        private static readonly JsonSerializerSettings JsonSerializerSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };
        
        private static readonly string editorSettingsFilePath = Application.dataPath + "/Build1/build1-config.json";
        private static readonly string clientSettingsFilePath = Application.dataPath + "/Resources/build1-config-settings.json";
        private static readonly string resourcesFolderPath    = Application.dataPath + "/Resources/";

        public static event Action<ConfigNode> OnConfigSaving;
        public static event Action<ConfigNode> OnConfigSaved;

        static ConfigProcessor()
        {
            CheckRequiredResources();
        }

        /*
         * Resources.
         */

        public static void CheckRequiredResources()
        {
            CheckConfigFolder();
        }

        private static void CheckConfigFolder()
        {
            var configsRootPath = GetEditorConfigsRootFolderPath();
            if (Directory.Exists(configsRootPath))
                return;

            Directory.CreateDirectory(configsRootPath);
            Log("Configs folder created");
        }

        /*
         * Settings.
         */

        public static ConfigSettingsEditor GetSettings()
        {
            if (!File.Exists(editorSettingsFilePath))
                return ConfigSettingsEditor.New(ConfigSettings.SourceDefault);

            var json = File.ReadAllText(editorSettingsFilePath);
            var settings = JsonConvert.DeserializeObject<ConfigSettingsEditor>(json);
            return settings;
        }

        public static void TrySaveSettings(ConfigSettingsEditor settings)
        {
            if (!settings.IsDirty)
                return;
            
            var json = JsonConvert.SerializeObject(settings, JsonSerializerSettings);
            File.WriteAllText(editorSettingsFilePath, json);

            var clientSettings = ConfigSettings.FromEditorSettings(settings);
            json = JsonConvert.SerializeObject(clientSettings, JsonSerializerSettings);
            File.WriteAllText(clientSettingsFilePath, json);
            
            UpdateConfigFiles(settings);
            
            settings.ResetDirty();
        }

        private static void UpdateConfigFiles(ConfigSettingsEditor settings)
        {
            var configResourcePath = resourcesFolderPath + "config.json";
            if (File.Exists(configResourcePath))
                File.Delete(configResourcePath);
            
            var configResourcePathMeta = resourcesFolderPath + "config.json.meta";
            if (File.Exists(configResourcePathMeta))
                File.Delete(configResourcePathMeta);
                
            // Firebase is remote, so we don't need to embed anything into the build.
            if (settings.Source != ConfigSettings.SourceFirebase)
                File.Copy(GetEditorConfigFilePath(settings.Source), configResourcePath);
            
            var configFallbackResourcePath = resourcesFolderPath + "config_fallback.json";
            if (File.Exists(configFallbackResourcePath))
                File.Delete(configFallbackResourcePath);
            
            var configFallbackResourcePathMeta = resourcesFolderPath + "config_fallback.json.meta";
            if (File.Exists(configFallbackResourcePathMeta))
                File.Delete(configFallbackResourcePathMeta);
            
            if (settings.FallbackEnabled)
                File.Copy(GetEditorConfigFilePath(settings.FallbackSource), configFallbackResourcePath);

            AssetDatabase.Refresh(ImportAssetOptions.Default);
        }

        /*
         * Configs.
         */

        public static List<string> GetConfigs()
        {
            var configsRootPath = GetEditorConfigsRootFolderPath();
            var directories = Directory.GetDirectories(configsRootPath);
            var res = new List<string> { ConfigSettings.SourceFirebase };
            res.AddRange(directories.Select(directory => directory.Replace(configsRootPath, "")));
            return res;
        }

        public static void AddConfig(string configName, string content, Action<string> onComplete)
        {
            var folderPath = GetEditorConfigFolderPath(configName);
            Directory.CreateDirectory(folderPath);

            var filePath = GetEditorConfigFilePath(configName);
            File.WriteAllText(filePath, content);

            AssetDatabase.Refresh(ImportAssetOptions.Default);

            onComplete?.Invoke(configName);
        }

        public static void RemoveConfig(string configName, Action<string> onComplete, Action<Exception> onError)
        {
            if (configName == ConfigSettings.SourceFirebase)
            {
                onError?.Invoke(new Exception("Firebase config can't be deleted."));
                return;
            }

            var folderPath = GetEditorConfigFolderPath(configName);
            Directory.Delete(folderPath, true);
            File.Delete(folderPath + ".meta");

            var settings = GetSettings();
            var source = settings.Source;
            if (source == configName)
            {
                var configResourcePath = resourcesFolderPath + "config.json"; 
                if (File.Exists(configResourcePath))
                    File.Delete(configResourcePath);

                var configFallbackResourcePath = resourcesFolderPath + "config_fallback.json";
                if (File.Exists(configFallbackResourcePath))
                    File.Delete(configFallbackResourcePath);
                
                settings.SetSource(ConfigSettings.SourceDefault);
            }

            if (settings.BaselineSource == configName)
                settings.SetBaselineSource(null);
            
            TrySaveSettings(settings);

            AssetDatabase.Refresh(ImportAssetOptions.Default);

            onComplete?.Invoke(configName);
        }

        public static void SaveConfig(string configName, string content)
        {
            var filePath = GetEditorConfigFilePath(configName);
            File.WriteAllText(filePath, content);

            var settings = GetSettings();
            var source = settings.Source;
            if (source == configName && source != ConfigSettings.SourceFirebase)
                File.WriteAllText(resourcesFolderPath + "config.json", content);

            if (settings.FallbackEnabled && settings.FallbackSource == configName)
                File.WriteAllText(resourcesFolderPath + "config_fallback.json", content);
            
            AssetDatabase.Refresh(ImportAssetOptions.Default);
        }

        public static void OnSaving(ConfigNode config) { OnConfigSaving?.Invoke(config); }
        public static void OnSaved(ConfigNode config)  { OnConfigSaved?.Invoke(config); }

        /*
         * Paths.
         */

        public static string GetEditorConfigsRootFolderPath()
        {
            return Application.dataPath + "/Config/";
        }

        public static string GetEditorConfigFolderPath(string configName)
        {
            return $"{Application.dataPath}/Config/{configName}";
        }

        public static string GetEditorConfigFilePath(string configName)
        {
            return $"{Application.dataPath}/Config/{configName}/config.json";
        }

        /*
         * Logging.
         */

        private static void Log(string message)
        {
            Debug.Log($"Config: {message}");
        }
    }
}

#endif