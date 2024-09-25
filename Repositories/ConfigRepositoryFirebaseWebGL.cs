#if UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Build1.UnityConfig.Utils;
using Modules.Firebase.Adapter.Impl;
using Newtonsoft.Json;
using UnityEngine;
using Rc = MarksAssets.FirebaseWebGL.RemoteConfig.RemoteConfig;
using RemoteConfigSettings = MarksAssets.FirebaseWebGL.RemoteConfig.RemoteConfigSettings;

namespace Build1.UnityConfig.Repositories
{
    internal static class ConfigRepositoryFirebaseWebGL
    {
        private static readonly Regex BooleanTruePattern  = new("^(1|true|t|yes|y|on)$", RegexOptions.IgnoreCase);
        private static readonly Regex BooleanFalsePattern = new("^(0|false|f|no|n|off|)$", RegexOptions.IgnoreCase);

        private static Rc   _remoteConfig;
        private static bool _fetched;

        public static void Load<T>(ConfigSettings settings, Action<T> onComplete, Action<ConfigException> onError) where T : ConfigNode
        {
            Initialize(settings, () =>
            {
                FetchAndActivate(() =>
                {
                    GetJson<T>(settings, value =>
                    {
                        onComplete?.Invoke(value);
                    }, onError);
                }, onError);
            });
        }

        private static void Initialize(ConfigSettings settings, Action onComplete)
        {
            if (!FirebaseWebglAdapter.Initialized)
            {
                throw new Exception("FirebaseWebglAdapter isn't initialized.");
            }

            SetupRemoteConfig(settings, onComplete);
        }

        private static void SetupRemoteConfig(ConfigSettings settings, Action onComplete)
        {
            _remoteConfig = Rc.getRemoteConfig(FirebaseWebglAdapter.App);

            var fallbackTimeout = settings.FallbackEnabled && settings.FallbackTimeout > 0 ? settings.FallbackTimeout : 0;

            var minimumFetchIntervalMillis = Debug.isDebugBuild ? 0 : 300000;
            var fetchTimeoutMillis = fallbackTimeout > 0 ? fallbackTimeout : 60000;

            _remoteConfig.settings = new RemoteConfigSettings(fetchTimeoutMillis, minimumFetchIntervalMillis);

            onComplete?.Invoke();
        }

        private static async void FetchAndActivate(Action onComplete, Action<ConfigException> onError)
        {
            if (_fetched)
            {
                onComplete?.Invoke();
                return;
            }

            try
            {
                var isFetched = await Rc.fetchAndActivate(_remoteConfig);

                if (isFetched)
                {
                    _fetched = true;
                    onComplete?.Invoke();
                }
                else
                {
                    onComplete?.Invoke();
                }
            }
            catch (Exception error)
            {
                onError?.Invoke(new ConfigException(ConfigError.ConfigResourceNotFound, error.Message));
            }
        }

        private static void GetJson<T>(ConfigSettings settings, Action<T> onComplete, Action<ConfigException> onError)
        {
            if (settings.Mode == ConfigMode.Decomposed)
            {
                GetJsonDecomposed(settings, onComplete, onError);
                return;
            }

            try
            {
                var value = Rc.getValue(_remoteConfig, settings.ParameterName);
                var config = JsonConvert.DeserializeObject<T>(value.asString());

                onComplete?.Invoke(config);
            }
            catch (Exception error)
            {
                onError?.Invoke(new ConfigException(ConfigError.ParsingError, error.Message));
            }
        }

        private static void GetJsonDecomposed<T>(ConfigSettings settings, Action<T> onComplete, Action<ConfigException> onError)
        {
            try
            {
                var defaultConfig = Rc.getAll(_remoteConfig);

                if (defaultConfig == null)
                {
                    throw new ConfigException(ConfigError.ConfigFieldNotFound, "defaultConfig is null or not found.");
                }

                var parameters = new Dictionary<string, object>();

                foreach (var (key, value) in defaultConfig)
                {
                    var stringValue = value.asString();

                    if (stringValue.Length >= 24)
                    {
                        parameters.Add(key, TryDecompress(stringValue));
                    }
                    else if (stringValue.Length > 0)
                    {
                        if (BooleanTruePattern.IsMatch(stringValue))
                        {
                            parameters.Add(key, true);
                        }
                        else if (BooleanFalsePattern.IsMatch(stringValue))
                        {
                            parameters.Add(key, false);
                        }
                        else if (long.TryParse(stringValue, out var valueInt))
                        {
                            parameters.Add(key, valueInt);
                        }
                        else if (double.TryParse(stringValue, out var valueDouble))
                        {
                            parameters.Add(key, valueDouble);
                        }
                        else
                        {
                            parameters.Add(key, stringValue);
                        }
                    }
                }

                var instance = ParseDecomposed<T>(parameters);
                onComplete?.Invoke(instance);
            }
            catch (Exception ex)
            {
                onError?.Invoke(new ConfigException(ConfigError.ParsingError, ex.Message));
            }
        }

        private static object TryDecompress(string value)
        {
            if (!value.StartsWith("{") || !value.EndsWith("}"))
                value = value.Decompress();

            return value;
        }

        private static T ParseDecomposed<T>(IDictionary<string, object> values)
        {
            var instance = Activator.CreateInstance<T>();
            var properties = typeof(T).GetProperties();

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