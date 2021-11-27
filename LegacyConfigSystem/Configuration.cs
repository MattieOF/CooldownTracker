using System;
using System.Collections.Generic;
using System.Text;

namespace CooldownTracker.Config
{
    public class Configuration
    {
        public Dictionary<string, ConfigValue> values;

        public Configuration(string path)
        {
            values = new Dictionary<string, ConfigValue>();
        }
    }
}
