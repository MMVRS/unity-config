using System;
using System.Runtime.Serialization;
using Build1.UnityConfig.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace Build1.UnityConfig
{
    public sealed class ConfigNodeMetadata
    {
        [JsonProperty("a")] public string AppVersion          { get; private set; }
        [JsonProperty("n")] public string Note                { get; private set; }
        [JsonProperty("c")] public string LastChangedBy       { get; private set; }
        [JsonProperty("t")] public long   LastChangeTimestamp { get; private set; }
        [JsonProperty("b")] public bool   AbTestEnabled       { get; private set; }
        [JsonProperty("m")] public string AbTestName          { get; private set; }
        [JsonProperty("g")] public string AbTestGroup         { get; private set; }

        [JsonIgnore] public DateTime LastChangeDate { get; private set; }

        internal ConfigNodeMetadata() { }

        /*
         * Public.
         */

        internal void Update()
        {
            AppVersion = Application.version;
            LastChangedBy = Environment.UserName;
            LastChangeDate = DateTime.UtcNow;
            LastChangeTimestamp = LastChangeDate.ToUnixTimestamp();
        }

        /*
         * Serialization.
         */

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            LastChangeDate = LastChangeTimestamp.FromUnixTimestamp();
        }
    }
}