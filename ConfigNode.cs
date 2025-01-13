using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Build1.UnityConfig
{
    public abstract class ConfigNode
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        [JsonProperty("_m")] public ConfigNodeMetadata Metadata { get; private set; }

        internal void UpdateMetadata()
        {
            Metadata ??= new ConfigNodeMetadata();
            Metadata.Update();
        }

        internal void ClearMetadata()
        {
            Metadata = null;
        }

        public IEnumerable<ConfigNodeInfo> GetNodesInfo()
        {
            var properties = GetType()
                            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.PropertyType.IsSubclassOf(typeof(ConfigNode)));
            return properties.Select(property => new ConfigNodeInfo(property.Name, (ConfigNode)property.GetValue(this)));
        }

        public string ToJson(bool indented)
        {
            if (indented)
                return JsonConvert.SerializeObject(this, Formatting.Indented, Settings);
            return JsonConvert.SerializeObject(this, Settings);
        }
        
        /*
         * Private.
         */
        
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Metadata ??= new ConfigNodeMetadata();
        }

        /*
         * Static.
         */

        public static ConfigNode FromJson(string json, Type type)
        {
            return (ConfigNode)JsonConvert.DeserializeObject(json, type);
        }
    }
}