#if BUILD1_CONFIG_FIREBASE_REMOTE_CONFIG_AVAILABLE && (UNITY_EDITOR || !UNITY_WEBGL)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Build1.UnityConfig.Utils;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using Newtonsoft.Json;
using UnityEngine;

namespace Build1.UnityConfig.Repositories
{
    internal static class ConfigRepositoryFirebase
    {
        private static readonly Regex BooleanTruePattern  = new("^(1|true|t|yes|y|on)$", RegexOptions.IgnoreCase);
        private static readonly Regex BooleanFalsePattern = new("^(0|false|f|no|n|off|)$", RegexOptions.IgnoreCase);

        public static void Load(ConfigSettings settings, Type configType, Action<ConfigNode> onComplete, Action<ConfigException> onError)
        {
            SetConfigSettings(settings, () => { FetchConfig(() => { GetJsonAndParse(settings, configType, onComplete, onError); }, onError); }, onError);
        }

        public static void Load<T>(ConfigSettings settings, Action<T> onComplete, Action<ConfigException> onError) where T : ConfigNode
        {
            SetConfigSettings(settings, () => { FetchConfig(() => { GetJsonAndParse(settings, onComplete, onError); }, onError); }, onError);
        }

        /*
         * Private.
         */

        private static void SetConfigSettings(ConfigSettings settings, Action onComplete, Action<ConfigException> onError)
        {
            try
            {
                var configSettings = new global::Firebase.RemoteConfig.ConfigSettings();

                if (settings is { FallbackEnabled: true, FallbackTimeout: > 0 })
                    configSettings.FetchTimeoutInMilliseconds = (ulong)settings.FallbackTimeout;

                if (Debug.isDebugBuild)
                    configSettings.MinimumFetchIntervalInMilliseconds = 0; // Refresh immediately when debugging.

                FirebaseRemoteConfig.DefaultInstance
                                    .SetConfigSettingsAsync(configSettings)
                                    .ContinueWithOnMainThread(settingsTask =>
                                     {
                                         if (settingsTask.IsFaulted)
                                         {
                                             onError?.Invoke(settingsTask.Exception.ToConfigException());
                                             return;
                                         }

                                         onComplete?.Invoke();
                                     });
            }
            catch (Exception exception)
            {
                onError?.Invoke(exception.ToConfigException());
            }
        }

        private static void FetchConfig(Action onComplete, Action<ConfigException> onError)
        {
            try
            {
                FirebaseRemoteConfig.DefaultInstance
                                    .FetchAndActivateAsync()
                                    .ContinueWithOnMainThread(task =>
                                     {
                                         if (task.IsFaulted)
                                         {
                                             onError?.Invoke(task.Exception.ToConfigException());
                                             return;
                                         }

                                         onComplete?.Invoke();
                                     });
            }
            catch (Exception exception)
            {
                onError?.Invoke(exception.ToConfigException());
            }
        }

        private static void GetJsonAndParse(ConfigSettings settings, Type configType, Action<ConfigNode> onComplete, Action<ConfigException> onError)
        {
            switch (settings.Mode)
            {
                case ConfigMode.Default:
                    GetJsonDefault(settings, json =>
                    {
                        var config = (ConfigNode)JsonConvert.DeserializeObject(json, configType);
                        onComplete?.Invoke(config);
                    }, onError);
                    break;
                case ConfigMode.Decomposed:
                    GetDecomposed(settings, values =>
                    {
                        var instance = ParseDecomposed(values, configType);
                        onComplete?.Invoke(instance);
                    }, onError);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void GetJsonAndParse<T>(ConfigSettings settings, Action<T> onComplete, Action<ConfigException> onError) where T : ConfigNode
        {
            switch (settings.Mode)
            {
                case ConfigMode.Default:
                    GetJsonDefault(settings, json =>
                    {
                        var config = JsonConvert.DeserializeObject<T>(json);
                        onComplete?.Invoke(config);
                    }, onError);
                    break;
                case ConfigMode.Decomposed:
                    GetDecomposed(settings, values =>
                    {
                        var instance = ParseDecomposed<T>(values);
                        onComplete?.Invoke(instance);
                    }, onError);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void GetJsonDefault(ConfigSettings settings, Action<string> onComplete, Action<ConfigException> onError)
        {
            try
            {
                var field = settings.ParameterName;

                if (!FirebaseRemoteConfig.DefaultInstance.AllValues.ContainsKey(field))
                {
                    onError?.Invoke(new ConfigException(ConfigError.ConfigFieldNotFound, $"Parameter \"{field}\" not found in Firebase Remote Config."));
                    return;
                }

                var json = FirebaseRemoteConfig.DefaultInstance.AllValues[field].StringValue;

                try
                {
                    if (!json.StartsWith("{") || !json.EndsWith("}"))
                        json = json.Decompress();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);

                    onError?.Invoke(exception.ToConfigException());
                    return;
                }

                onComplete?.Invoke(json);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                onError?.Invoke(exception.ToConfigException());
            }
        }

        private static void GetDecomposed(ConfigSettings settings, Action<Dictionary<string, object>> onComplete, Action<ConfigException> onError)
        {
            try
            {
                var parameters = new Dictionary<string, object>(FirebaseRemoteConfig.DefaultInstance.AllValues.Count);

                foreach (var pair in FirebaseRemoteConfig.DefaultInstance.AllValues)
                {
                    var value = pair.Value.StringValue;
                    if (value.Length >= 24)
                    {
                        parameters.Add(pair.Key, TryDecompress(value));
                    }
                    else if (value.Length > 0)
                    {
                        if (BooleanTruePattern.IsMatch(value))
                        {
                            parameters.Add(pair.Key, true);
                        }
                        else if (BooleanFalsePattern.IsMatch(value))
                        {
                            parameters.Add(pair.Key, false);
                        }
                        else if (long.TryParse(value, out var valueInt))
                        {
                            parameters.Add(pair.Key, valueInt);
                        }
                        else if (double.TryParse(value, out var valueDouble))
                        {
                            parameters.Add(pair.Key, valueDouble);
                        }
                        else
                        {
                            parameters.Add(pair.Key, TryDecompress(value));
                        }
                    }
                }

                onComplete?.Invoke(parameters);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                onError?.Invoke(exception.ToConfigException());
            }
        }

        private static object TryDecompress(string value)
        {
            if (!value.StartsWith("{") || !value.EndsWith("}"))
                value = value.Decompress();
            return value;
        }

        private static ConfigNode ParseDecomposed(IDictionary<string, object> values, Type configType)
        {
            var instance   = (ConfigNode)Activator.CreateInstance(configType);
            var properties = instance.GetType().GetProperties();

            foreach (var property in properties)
            {
                var jsonPropertyAttribute = property.GetCustomAttribute<JsonPropertyAttribute>();
                if (jsonPropertyAttribute?.PropertyName == null)
                    continue;

                if (!values.TryGetValue(jsonPropertyAttribute.PropertyName, out var value))
                    continue;

                if (value is string json)
                {
                    var propertyInstance = JsonConvert.DeserializeObject(json, property.PropertyType);
                    property.SetValue(instance, propertyInstance);
                }
                else
                {
                    property.SetValue(instance, value);
                }
            }
            
            var method = instance.GetType().GetMethod("OnDeserialized", BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
            {
                var methods = instance.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
                method = methods.FirstOrDefault(m => m.GetCustomAttribute<OnDeserializedAttribute>() != null);
            }

            method?.Invoke(instance, new object[] { null });

            return instance;
        }

        private static T ParseDecomposed<T>(IDictionary<string, object> values)
        {
            var instance   = Activator.CreateInstance<T>();
            var properties = instance.GetType().GetProperties();

            foreach (var property in properties)
            {
                var jsonPropertyAttribute = property.GetCustomAttribute<JsonPropertyAttribute>();
                if (jsonPropertyAttribute?.PropertyName == null)
                    continue;

                if (!values.TryGetValue(jsonPropertyAttribute.PropertyName, out var value))
                    continue;

                if (value is string json)
                {
                    var propertyInstance = JsonConvert.DeserializeObject(json, property.PropertyType);
                    property.SetValue(instance, propertyInstance);
                }
                else
                {
                    property.SetValue(instance, value);
                }
            }
            
            var method = instance.GetType().GetMethod("OnDeserialized", BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
            {
                var methods = instance.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
                method = methods.FirstOrDefault(m => m.GetCustomAttribute<OnDeserializedAttribute>() != null);
            }
            
            method?.Invoke(instance, new object[] { null });

            return instance;
        }
    }
}

#endif