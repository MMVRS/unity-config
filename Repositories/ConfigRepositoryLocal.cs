using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Build1.UnityConfig.Repositories
{
    internal static class ConfigRepositoryLocal
    {
        internal static ulong GetCacheSizeBytes()
        {
            var path = GetCachePath();
            return File.Exists(path) ? (ulong)new FileInfo(path).Length : 0;
        }

        internal static void LoadFromResources<T>(string fileName, Action<T> onComplete, Action<ConfigException> onError) where T : ConfigNode
        {
            string json;
            T config;
            
            try
            {
                json = Resources.Load<TextAsset>(fileName).text;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                onError?.Invoke(new ConfigException(ConfigError.ResourceNotFound, $"FileName: {fileName}", exception));
                return;
            }
            
            try
            {
                config = JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                onError?.Invoke(new ConfigException(ConfigError.ParsingError, $"JSON: {json}", exception));
                return;
            }

            onComplete?.Invoke(config);
        }

        internal static void LoadFromCache<T>(Action<T> onComplete, Action<ConfigException> onError) where T : ConfigNode
        {
            var path = GetCachePath();
            
            string json;
            
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception exception)
            {
                // No error logging needed as when updating from one version of the app to another backup config will not be found.
                onError?.Invoke(new ConfigException(ConfigError.ResourceNotFound, $"Path: {path}", exception));
                return;
            }
            
            T config;
            
            try
            {
                config = JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                onError?.Invoke(new ConfigException(ConfigError.ParsingError, $"JSON: {json}", exception));
                return;
            }
            
            onComplete.Invoke(config);
        }

        internal static void SaveToCache<T>(T config) where T : ConfigNode
        {
            try
            {
                var path = GetCachePath();
                var json = config.ToJson(false);
            
                File.WriteAllText(path, json);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        internal static void CleanCache()
        {
            var path = GetCachePath();
            if (File.Exists(path))
                File.Delete(path);
        }

        private static string GetCachePath()
        {
            // Version in the file name prevents issues while updating the app.
            return $"{Application.persistentDataPath}/config_backup_{Application.version}.json";
        }
    }
}
