using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.IO;
using System.Linq;
using SocketIOClient;
using SocketIOClient.ConnectInterval;
using Newtonsoft.Json.Linq;
using System.Globalization;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Net;

namespace CooldownTracker
{
    internal class Redemption
    {
        public string   name;
        public int      cost;
        public string   iconUrl;
        public DateTime cooldownExpiresAt;
        public bool     ready = false;

        public string SpacelessName
        {
            get { return name.Replace(' ', '-'); }
        }
    }

    internal class ConnectInterval : IConnectInterval
    {
        private int delay = 1000;

        public int GetDelay()
        {
            return delay;
        }

        public double NextDealy() // This typo is hard coded into the interface...
        {
            return delay += 1000;
        }
    }

    class Program
    {
        private static List<Redemption> redemptions    = new List<Redemption>();
        private static List<string>     exemptions     = new List<string>();
        private static WebClient        client         = new WebClient();
        private static CultureInfo      culture        = CultureInfo.CreateSpecificCulture("en-US");
        private static bool             enumerating    = false;
        private static bool             logRedemptions = true;

        private static Config           config;

        static async Task Main(string[] args)
        {
            Directory.CreateDirectory("Images");
            Directory.CreateDirectory("Config");

            Console.ForegroundColor = ConsoleColor.White;
            Console.Title = "Twitch Cooldown Tracker";

            config = ConfigSerialiser.LoadConfig();
            Console.WriteLine($"Streamer: {config.streamerName}");

            LoadExemptions();

            // Connect to socket
            Console.WriteLine($"Connecting to socket {config.apiURL}...");
            var uri = new Uri(config.apiURL);
            var socket = new SocketIO(uri, new SocketIOOptions
            {
                Reconnection = true,
                EIO = 4,
                Query = new Dictionary<string, string>
                {
                    {"token", "v3"}
                }
            })
            {
                GetConnectInterval = () => new ConnectInterval()
            };

            socket.OnConnected += (sender, e) =>
            {
                Console.WriteLine("Socket connected!");
                Console.WriteLine("Commands: cooldowns, clearimages, toggleimage, togglelogging, reloadexemptions, clearexemptions, exemptions, addexemption, removeexemption, mincost, setmincost, clear, exit");
                Console.WriteLine("Exemptions are redemption names that will show up even if their cost is below the minimum cost.");
            };

            socket.On(config.streamerName, response =>
            {
                Redemption redemption = new Redemption();
                JArray redemptionValues = JArray.Parse(response.ToString());

                if (redemptionValues[0]["type"].ToString() != "custom-reward-updated") return;

                redemption.name              = redemptionValues[0]["data"]["updated_reward"]["title"].Value<string>();
                redemption.cost              = redemptionValues[0]["data"]["updated_reward"]["cost"].Value<int>();
                redemption.iconUrl           = redemptionValues[0]["data"]["updated_reward"]["image"]["url_4x"].Value<string>();
                redemption.ready             = false;
                redemption.cooldownExpiresAt = DateTime.Parse(redemptionValues[0]["data"]["updated_reward"]["cooldown_expires_at"].Value<string>(),
                                               culture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal);

                string imagePath = $"Images/{redemption.SpacelessName}.png";
                if (!File.Exists(imagePath))
                    client.DownloadFile(new Uri(redemption.iconUrl), imagePath);

                if (logRedemptions)
                    Console.WriteLine($"Redemption of {redemption.name} received!");

                // We're in the check redemptions foreach loop, so wait until that's done 
                // by waiting for 1ms until we're not as to avoid an exception
                while (enumerating)
                    Thread.Sleep(1);

                redemptions.RemoveAll(r => r.name == redemption.name);
                redemptions.Add(redemption);
            });

            await socket.ConnectAsync();

            // Set up a timer to call CheckRedemptions every second (1000ms)
            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Interval            = 1000;
            timer.Elapsed             += CheckRedemptions;
            timer.Enabled             = true;

            string input;
            do
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("> ");
                Console.ForegroundColor = ConsoleColor.White;
                input = Console.ReadLine();
                ProcessInput(input);
            }
            while (!input.Equals("exit", StringComparison.CurrentCultureIgnoreCase));

            ToastNotificationManagerCompat.Uninstall();
        }

        public static void ProcessInput(string input)
        {
            if (input.Equals("clearimages", StringComparison.CurrentCultureIgnoreCase))
            {
                ClearImages();
                return;
            }

            if (input.Equals("clear", StringComparison.CurrentCultureIgnoreCase))
            {
                Console.Clear();
                return;
            }

            if (input.Equals("togglelogging", StringComparison.CurrentCultureIgnoreCase)
                || input.Equals("togglelog", StringComparison.CurrentCultureIgnoreCase)
                || input.Equals("togglelogs", StringComparison.CurrentCultureIgnoreCase))
            {
                logRedemptions = !logRedemptions;
                if (logRedemptions)
                    Console.WriteLine($"Redemption logging enabled");
                else
                    Console.WriteLine($"Redemption logging disabled");
                return;
            }

            if (input.Equals("toggleimage", StringComparison.CurrentCultureIgnoreCase))
            {
                config.showImageInNotification = !config.showImageInNotification;
                if (config.showImageInNotification)
                    Console.WriteLine("Enabled image in notification.");
                else
                    Console.WriteLine("Disabled image in notification.");
                ConfigSerialiser.SaveConfig(config);
                return;
            }

            if (input.Equals("reloadexemptions", StringComparison.CurrentCultureIgnoreCase))
            {
                LoadExemptions();
                return;
            }

            if (input.Equals("clearexemptions", StringComparison.CurrentCultureIgnoreCase))
            {
                ClearExemptions();
                return;
            }
            
            if (input.Equals("cooldowns", StringComparison.CurrentCultureIgnoreCase))
            {
                PrintCurrentCooldowns();
                return;
            }
            
            if (input.Equals("mincost", StringComparison.CurrentCultureIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("Minimum cost is currently ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(config.minCost);
                return;
            }

            if (input.Equals("exemptions", StringComparison.CurrentCultureIgnoreCase)
                || input.Equals("viewexemptions", StringComparison.CurrentCultureIgnoreCase)
                || input.Equals("showexemptions", StringComparison.CurrentCultureIgnoreCase))
            {
                PrintExemptions();
                return;
            }

            string[] tokens = input.Split(' ', 2);

            if (tokens[0].Equals("addexemption", StringComparison.CurrentCultureIgnoreCase))
            {
                AddExemptions(tokens[1]);
                return;
            }

            if (tokens[0].Equals("removeexemption", StringComparison.CurrentCultureIgnoreCase))
            {
                if (tokens.Length <= 1)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Provide an exemption to remove as an argument");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                RemoveExemption(tokens[1]);
                return;
            }

            if (input.Equals("setmincost", StringComparison.CurrentCultureIgnoreCase))
            {
                if (tokens.Length == 1 || !uint.TryParse(tokens[1], out config.minCost))
                {
                    string minCostInput;
                    do
                    {
                        Console.Write("Enter minimum spawn cost to show notification: ");
                        minCostInput = Console.ReadLine();
                    }
                    while (!uint.TryParse(minCostInput, out config.minCost));
                }

                ConfigSerialiser.SaveConfig(config);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Set minimum cost to {config.minCost}");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        public static void LoadExemptions(string path = "Config/exemptions.txt")
        {
            if (!File.Exists(path))
            {
                File.Create(path).Close();
                return;
            }

            exemptions.Clear();
            StreamReader reader = new StreamReader(path);
            string line = reader.ReadLine();
            while (!string.IsNullOrEmpty(line))
            {
                exemptions.Add(line);
                line = reader.ReadLine();
            }
            reader.Close();

            if (exemptions.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Loaded exemptions: {string.Join(", ", exemptions)}");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        public static void SaveExemptions(string path = "Config/exemptions.txt")
        {
            StreamWriter writer = new StreamWriter(path);
            foreach (string exemption in exemptions)
                writer.WriteLine(exemption);
            writer.Flush();
            writer.Close();
        }

        public static void AddExemptions(string exemption)
        {
            exemptions.Add(exemption);
            SaveExemptions();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Added exemption: {exemption}");
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static void RemoveExemption(string exemption)
        {
            if (!exemptions.Contains(exemption))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Couldn't find exemption {exemption} in the list. The command is case sensitive.");
                Console.ForegroundColor = ConsoleColor.White;
            } else
            {
                exemptions.Remove(exemption);
                SaveExemptions();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Removed exemption {exemption}");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        public static void ClearExemptions()
        {
            // Save the old exemptions and get the count
            SaveExemptions("Config/exemptions.old.txt");
            int oldCount = exemptions.Count;
            
            // Clear the exemptions and save it (pretty much, reset the file)
            exemptions.Clear();
            SaveExemptions();
            
            // Feedback to user
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Cleared {oldCount} exemptions!");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"Old exemptions have been saved to Config/exemptions.old.txt");
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static void PrintExemptions()
        {
            if (exemptions.Count > 0)
                Console.WriteLine($"Current exemptions: {string.Join(", ", exemptions)}");
            else
                Console.WriteLine("The exemptions list is currently empty. Add exemptions with the 'addexemption' command.");
        }

        public static void ClearImages()
        {
            int count = 0;
            foreach (string file in Directory.GetFiles("Images"))
            {
                // Only delete if its an image file 
                if (Path.GetExtension(file) == ".png")
                {
                    File.Delete(file);
                    count++;
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Cleared {count} images");
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static void PrintCurrentCooldowns()
        {
            Console.WriteLine("Redemptions currently on cooldown are:");
            
            Console.ForegroundColor = ConsoleColor.DarkGray;
            foreach (Redemption r in redemptions)
            {
                if (!r.ready)
                {
                    // It's on cooldown
                    var cooldown = r.cooldownExpiresAt - DateTime.Now;
                    Console.WriteLine($"{r.name}: {cooldown.Hours:D2}:{cooldown.Minutes:D2}:{cooldown.Seconds:D2}");
                }
            }
            Console.ForegroundColor = ConsoleColor.White;
        }
        
        /// <summary>
        /// Enumerate through all redemptions in the list and check if they are ready yet.
        /// The parameters are not needed.
        /// </summary>
        public static void CheckRedemptions(object sender, ElapsedEventArgs e)
        {
            // The 'enumerating' bool is a bodged solution to a collection 
            // modified exception if a redemption is received during the foreach
            enumerating = true;
            foreach (Redemption r in redemptions)
            {
                if (!r.ready)
                {
                    if (r.cooldownExpiresAt < e.SignalTime)
                    {
                        r.ready = true;

                        Console.ForegroundColor = ConsoleColor.Green;
                        if (logRedemptions)
                            Console.WriteLine($"Redemption ready: {r.name}");
                        Console.ForegroundColor = ConsoleColor.White;

                        if (r.cost < config.minCost && !exemptions.Contains(r.name, StringComparer.CurrentCultureIgnoreCase)) continue;

                        // Send notification
                        if (config.showImageInNotification && File.Exists($"Images/{r.SpacelessName}.png"))
                        {
                            new ToastContentBuilder()
                                .AddText("Redemption ready")
                                .AddText(r.name)
                                // No alt text for the image as the text describes the notification sufficiently
                                .AddInlineImage(new Uri(Path.GetFullPath($"Images/{r.SpacelessName}.png"))) 
                                .Show(toast =>
                                {
                                    // For an app like this, the notification is only useful for a short period 
                                    // and also needs to be seen very quickly if it's going to be useful, so it
                                    // is therefore set to expire quickly and also set to be high priority.
                                    toast.ExpirationTime = DateTime.Now.AddMinutes(3);
                                    toast.ExpiresOnReboot = true;
                                    toast.Group = "OffCooldownNotification";
                                    toast.Priority = Windows.UI.Notifications.ToastNotificationPriority.High;
                                });
                        } else
                        {
                            // Same as above but without the image as it doesn't exist. Maybe it's download didn't finish
                            // in time, or it's been deleted since the rewards last redemption. It'll get redownloaded.
                            new ToastContentBuilder()
                                .AddText("Redemption ready")
                                .AddText(r.name)
                                .Show(toast =>
                                {
                                    toast.ExpirationTime = DateTime.Now.AddMinutes(3);
                                    toast.ExpiresOnReboot = true;
                                    toast.Group = "OffCooldownNotification";
                                    toast.Priority = Windows.UI.Notifications.ToastNotificationPriority.High;
                                });
                        }
                    }
                }
            }
            enumerating = false;
        }
    }
}
