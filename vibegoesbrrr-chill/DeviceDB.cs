using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ZLinq;

namespace CVRGoesBrrr
{
    class DeviceDB
    {
        public static readonly DeviceDB Instance = new();
        
        private static string Endpoint = "https://iostindex.com/devices.json";
        private static TimeSpan CacheLifetime = new TimeSpan(1, 0, 0, 0); // 1 day

        private static Task FetchTask;
        private static Dictionary<string, IoSTDevice> GiverDevices = new();
        private static Dictionary<string, IoSTDevice> TakerDevices = new();

        private string CachePath => Path.Combine(NativeMethods.TempPath, "devices.json");
        
        public static string[] GiverTypes = {
            "Cock Ring",
            "Onahole",
            "Strap-on",
            "Masturbator",
            "Silicone Masturbator",
        };

        public static string[] TakerTypes = {
            "Buttplug",
            "Insertable Vibrator",
            "Prostate Vibrator",
            "Clip Vibrator",
            "Kegel",
            "Love Egg",
            "Panty Vibrator",
            "Rabbit",
            "Ride-on Vibrator",
            "Butterfly",
            "Suction Vibrator",
            "Wand",
            "Nipple Clamps",
            "Anal Beads",
            "Bullet",
            "Fucking Machine",
            "Kegel Wedge",
        };

        public class IoSTDevice
        {
            public string FullName { get; private set; }
            
            public string Brand { get; set; }
            public string Device { get; set; }
            public string Detail { get; set; }
            public string Availability { get; set; }
            public string Connection { get; set; }
            public string Type { get; set; }
            
            public bool IsTaker { get; set; }
            public bool IsGiver { get; set; }

            public class IoSTDeviceButtplug
            {
                public bool IsButtplugSupported => ButtplugSupport > 0;
                
                public int ButtplugSupport { get; set; }
            }
            public IoSTDeviceButtplug Buttplug { get; set; }

            [OnDeserialized]
            internal void OnDeserializedMethod(StreamingContext context)
            {
                FullName = (Brand + " " + Device).ToLower();
            }
        }

        internal async Task Fetch()
        {
            // Force using cached copy if cache is fresh
            try
            {
                var cacheLastWrite = File.GetLastWriteTimeUtc(CachePath);
                if (DateTime.UtcNow.Subtract(cacheLastWrite).TotalHours < CacheLifetime.TotalHours)
                {
                    Util.Logger.Msg("Cached device list is new enough, loading devices...");
                    var json = File.ReadAllText(CachePath, Encoding.UTF8);
                    ProcessDeviceList(JsonConvert.DeserializeObject<List<IoSTDevice>>(json));
                    return;
                }
            }
            catch (Exception e)
            {
                Util.Logger.Error("Unable to parse device list! Will attempt to retrieve list from IoSTIndex.", e);
                
                //Nuke the cache file and reset giver/taker list
                GiverDevices.Clear();
                TakerDevices.Clear();
                
                if(File.Exists(CachePath))
                    File.Delete(CachePath);
            }

            
            try
            {
                HttpClient client = new HttpClient();
                HttpResponseMessage response = await client.GetAsync(Endpoint);

                if (response.IsSuccessStatusCode)
                {
                    Util.Logger.Msg("Successfully retrieved device list from IoSTIndex. Processing devices...");
                    
                    var json =  await response.Content.ReadAsStringAsync();
                    ProcessDeviceList(JsonConvert.DeserializeObject<List<IoSTDevice>>(json));
                    
                    // Cache DB to disk
                    await File.WriteAllTextAsync(CachePath, json);
                    return;
                }

                Util.Logger.Warning("Unable to reach IoSTIndex, using embedded device list. This may not have newer devices included!");
            }
            catch (Exception e)
            {
                Util.Logger.Error("An error occured while loading devices IoSTIndex, using embedded device list. This may not have newer devices included!", e);
            }

            // If all else fails, load from packed resources
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("devices.json"))
            {
                Util.Logger.Warning("Using embedded device list, this is likely outdated and some devices will not be identified as compatible!");
                
                var json = await new StreamReader(stream).ReadToEndAsync();
                ProcessDeviceList(JsonConvert.DeserializeObject<List<IoSTDevice>>(json));
            }
        }

        private void ProcessDeviceList(List<IoSTDevice> devices)
        {
            var unique = devices.AsValueEnumerable().DistinctBy(x => x.FullName);
            
            foreach (var device in unique)
            {
                if(!device.Buttplug.IsButtplugSupported) continue;

                if (GiverTypes.Contains(device.Type.Trim()))
                {
                    //Store result on device entry for easy access later
                    device.IsGiver = true;
                    GiverDevices.Add(device.FullName, device);
                    continue;
                }

                if (TakerTypes.Contains(device.Type.Trim()))
                {
                    //Store result on device entry for easy access later
                    device.IsTaker = true;
                    TakerDevices.Add(device.FullName, device);
                }
            }
            
            Util.Logger.Msg($"Processed device list, {GiverDevices.Count} giver devices and {TakerDevices.Count} taker devices are supported.");
        }

        public IoSTDevice FindDevice(string name)
        {
            if(GiverDevices.ContainsKey(name.ToLower()))
                return GiverDevices[name.ToLower()];
            if(TakerDevices.ContainsKey(name.ToLower()))
                return TakerDevices[name.ToLower()];
            
            return null;
        }
    }
}