using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Build1.UnityConfig
{
    internal sealed class ConfigSettingsEditor
    {
        [JsonProperty("source")]               public string     Source                       { get; private set; }
        [JsonProperty("mode")]                 public ConfigMode Mode                         { get; private set; }
        [JsonProperty("param")]                public string     ParameterName                { get; private set; }
        [JsonProperty("source_reset")]         public bool       ResetSourceForPlatformBuilds { get; private set; }
        [JsonProperty("fallback_enabled")]     public bool       FallbackEnabled              { get; private set; }
        [JsonProperty("fallback_source")]      public string     FallbackSource               { get; private set; }
        [JsonProperty("fallback_timeout")]     public int        FallbackTimeout              { get; private set; }
        [JsonProperty("cache_enabled")]        public bool       CacheEnabled                 { get; private set; }
        [JsonProperty("fast_loading_enabled")] public bool       FastLoadingEnabled           { get; private set; }
        [JsonProperty("baseline_source")]      public string     BaselineSource               { get; private set; }

        [JsonIgnore] public bool IsDirty { get; private set; }

        private ConfigSettingsEditor() { }

        /*
         * Public.
         */

        public void SetSource(string value)
        {
            if (Source == value)
                return;

            Source = value;
            SetDirty();

            if (Source == ConfigSettings.SourceFirebase && !ResetSourceForPlatformBuilds && FallbackEnabled)
                SetFallbackEnabled(false);
        }

        public void SetMode(ConfigMode mode)
        {
            if (Mode == mode)
                return;

            Mode = mode;
            SetDirty();

            switch (Mode)
            {
                case ConfigMode.Default:
                    SetParameterName("config");
                    break;
                case ConfigMode.Decomposed:
                    SetParameterName(null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void SetParameterName(string param)
        {
            if (ParameterName == param)
                return;

            ParameterName = param == "config" ? null : param;
            SetDirty();
        }

        public void SetResetForPlatformBuilds(bool value)
        {
            if (ResetSourceForPlatformBuilds == value)
                return;

            ResetSourceForPlatformBuilds = value;
            SetDirty();

            if (Source == ConfigSettings.SourceFirebase && !ResetSourceForPlatformBuilds && FallbackEnabled)
                SetFallbackEnabled(false);
        }

        public void SetFallbackEnabled(bool value)
        {
            if (FallbackEnabled == value)
                return;

            FallbackEnabled = value;
            SetDirty();

            if (FallbackEnabled && FallbackTimeout == 0)
                SetFallbackTimeout(3000);
        }

        public void SetFallbackSource(string value)
        {
            if (FallbackSource == value)
                return;

            FallbackSource = value;
            SetDirty();
        }

        public void SetFallbackTimeout(int value)
        {
            if (FallbackTimeout == value)
                return;

            FallbackTimeout = value;
            SetDirty();
        }

        public void SetCacheEnabled(bool value)
        {
            if (CacheEnabled == value)
                return;

            CacheEnabled = value;

            if (!CacheEnabled && FastLoadingEnabled)
                SetFastLoadingEnabled(false);

            SetDirty();
        }

        public void SetFastLoadingEnabled(bool value)
        {
            if (FastLoadingEnabled == value)
                return;

            FastLoadingEnabled = value;
            SetDirty();
        }

        public void SetBaselineSource(string value)
        {
            if (BaselineSource == value)
                return;

            BaselineSource = value;
            SetDirty();
        }

        /*
         * Dirty.
         */

        public  void ResetDirty() { IsDirty = false; }
        private void SetDirty()   { IsDirty = true; }

        /*
         * Serialization.
         */

        [OnSerializing]
        private void OnSerializing(StreamingContext context)
        {
            if (ParameterName == "config")
                ParameterName = null;
        }

        [OnSerialized]
        private void OnSerialized(StreamingContext context)
        {
            if (string.IsNullOrWhiteSpace(ParameterName))
                ParameterName = "config";
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (Mode == ConfigMode.Default && string.IsNullOrWhiteSpace(ParameterName))
                ParameterName = "config";
        }

        /*
         * Static.
         */

        public static ConfigSettingsEditor New(string source)
        {
            return new ConfigSettingsEditor
            {
                Source = source,
                Mode = ConfigMode.Default,
                ParameterName = "config",
                ResetSourceForPlatformBuilds = true,
                FallbackEnabled = false,
                CacheEnabled = false,
                FastLoadingEnabled = false
            };
        }
    }
}