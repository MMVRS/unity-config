#if UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Build1.UnityConfig.Repositories.WebGL;
using Build1.UnityConfig.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace Build1.UnityConfig.Repositories
{
    internal sealed class ConfigRepositoryFirebaseWebGL
    {
        private readonly Regex BooleanTruePattern  = new("^(1|true|t|yes|y|on)$", RegexOptions.IgnoreCase);
        private readonly Regex BooleanFalsePattern = new("^(0|false|f|no|n|off|)$", RegexOptions.IgnoreCase);
        private readonly FirebaseWebglPrivateRemoteConfigBridge _remoteConfigBridge = new();

        private bool _fetched;

        public void Load<T>(ConfigSettings settings, Action<T> onComplete, Action<ConfigException> onError) where T : ConfigNode
        {
            LoadInternal(settings, onComplete, onError);
        }

        private async void LoadInternal<T>(ConfigSettings settings, Action<T> onComplete, Action<ConfigException> onError) where T : ConfigNode
        {
            try
            {
                await SetupRemoteConfig(settings);
                await FetchAndActivate();
                var config = await GetJson<T>(settings);
                onComplete?.Invoke(config);
            }
            catch (ConfigException exception)
            {
                onError?.Invoke(exception);
            }
            catch (Exception exception)
            {
                onError?.Invoke(new ConfigException(ConfigError.Unknown, "Unknown", exception));
            }
        }

        private async System.Threading.Tasks.Task SetupRemoteConfig(ConfigSettings settings)
        {
            var fallbackTimeout = settings.FallbackEnabled && settings.FallbackTimeout > 0 ? settings.FallbackTimeout : 0;

            var minimumFetchIntervalMillis = Debug.isDebugBuild ? 0 : 300000;
            var fetchTimeoutMillis = fallbackTimeout > 0 ? fallbackTimeout : 60000;

            await _remoteConfigBridge.Configure(fetchTimeoutMillis, minimumFetchIntervalMillis);
        }

        private async System.Threading.Tasks.Task FetchAndActivate()
        {
            if (_fetched)
                return;

            var isFetched = await _remoteConfigBridge.FetchAndActivate();

            if (isFetched)
                _fetched = true;
        }

        private async System.Threading.Tasks.Task<T> GetJson<T>(ConfigSettings settings) where T : ConfigNode
        {
            if (settings.Mode == ConfigMode.Decomposed)
            {
                return await GetJsonDecomposed<T>();
            }

            try
            {
                var json = await _remoteConfigBridge.GetValue(settings.ParameterName);
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception error)
            {
                throw new ConfigException(ConfigError.ParsingError, "Get Json error", error);
            }
        }

        private async System.Threading.Tasks.Task<T> GetJsonDecomposed<T>() where T : ConfigNode
        {
            try
            {
                var defaultConfig = await _remoteConfigBridge.GetAll();

                if (defaultConfig == null)
                {
                    throw new ConfigException(ConfigError.FieldNotFound, "defaultConfig is null or not found.", null);
                }

                var parameters = new Dictionary<string, object>();

                foreach (var (key, value) in defaultConfig)
                {
                    var stringValue = value;

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
                return instance;
            }
            catch (Exception ex)
            {
                throw new ConfigException(ConfigError.ParsingError, "Json decompose error", ex);
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
