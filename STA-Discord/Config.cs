using System.IO;
using Newtonsoft.Json;

namespace STA_Discord
{
    public class Config
    {
        public static string FileName = "config.json"; // let's use json because it's easy
        public static string FullPath => Directory.GetCurrentDirectory() + "/" + FileName;

        // the things we can configure, with defaults
        public string Prefix = "!";
        public string DiscordToken = "this-token-is-wrong-please-replace";
        public string AirtableToken = "this-token-is-wrong-please-replace";
        public string AirtableBaseId = "this-token-is-wrong-please-replace";

        public void Write()
        {
            File.WriteAllText(
                FullPath,
                JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public static Config Read()
        {
            return JsonConvert.DeserializeObject<Config>(
                File.ReadAllText(FullPath));
        }
    }
}
