using Tomlet;
using System.IO;
using System;

namespace CooldownTracker
{
    public class Config
    {
        public string streamerName, apiURL;
        public uint minCost;
        public bool showImageInNotification;

        public Config()
        {
            streamerName = "usteppin";
            apiURL = "http://rewards-relay.herokuapp.com/";
            minCost = 200;
            showImageInNotification = true;
        }
    }

    public static class ConfigSerialiser
    {
        public static Config LoadConfig(string path = "Config/config.cfg")
        {
            if (!File.Exists(path))
                return CreateDefaultConfig();

            string serialisedConfig = File.ReadAllText(path);

            try
            {
                Config config = TomletMain.To<Config>(serialisedConfig);
                return config;
            } 
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error parsing config! Using a default config.");
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                Console.ForegroundColor = ConsoleColor.White;
                return new Config();
            }
        }

        public static void SaveConfig(Config config, string path = "Config/config.cfg")
        {
            string serialisedConfig = TomletMain.TomlStringFrom(config);
            File.WriteAllText(path, serialisedConfig);
        }

        public static Config CreateDefaultConfig(string path = "Config/config.cfg")
        {
            Config config = new Config();
            SaveConfig(config, path);
            return config;
        }
    }
}
