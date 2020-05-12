using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Buttplug.Core.Messages;
using Newtonsoft.Json;

namespace Buttplug.Devices.Configuration
{
    public class DeviceConfiguration
    {
        // Can be null in default configuration
        [JsonProperty("identifier", NullValueHandling = NullValueHandling.Ignore)] public List<string> Identifiers;

        // Can be null when device is named via default
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)] public IDictionary<string, string> Names;

        // Can be null when all messages match default configuration and are not
        // included in configs block.
        [JsonProperty("messages", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, MessageAttributes> Attributes = new Dictionary<string, MessageAttributes>();

        public static IDictionary<TKey, TValue> Merge<TKey, TValue>(IDictionary<TKey, TValue> dictA, IDictionary<TKey, TValue> dictB)
            where TValue : class
        {
            return
                dictA == null ? dictB :
                dictB == null ? dictA :
                dictA.Keys.Union(dictB.Keys).ToDictionary(k => k, k => dictA.ContainsKey(k) ? dictA[k] : dictB[k]);
        }

        public void AddDefaults(DeviceConfiguration aDefaults)
        {
            Names = Merge(Names, aDefaults.Names);
            // Merge dictionaries. If a key already exists in our configuration,
            // ignore the version in the defaults.
            Attributes = Merge(Attributes, aDefaults.Attributes);
        }
    }

    public abstract class ProtocolConfiguration : IProtocolConfiguration
    {
        private List<DeviceConfiguration> _deviceConfig;

        public List<DeviceConfiguration> DeviceConfigs => _deviceConfig;

        public void SetDeviceConfig(List<DeviceConfiguration> aDeviceConfig)
        {
            _deviceConfig = aDeviceConfig;
        }

        public DeviceConfiguration GetDeviceConfig(string aIdentifier)
        {
            foreach (var conf in _deviceConfig)
            {
                if (conf.Identifiers.Contains(aIdentifier))
                {
                    return conf;
                }
            }

            return null;
        }

        public abstract bool Matches(IProtocolConfiguration aConfig);

        public abstract void Merge(IProtocolConfiguration aConfig);
    }
}