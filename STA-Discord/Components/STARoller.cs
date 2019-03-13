using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using Discord.WebSocket;
using Serilog;

namespace STA_Discord.Components
{
    class STARoller : IComponent
    {
        public static Regex diceregex = new Regex(@"^(?<numdice>\d*)(?<dsides>(?<separator>[d|D|o|O|u|U])(?<numsides>\d+))(?<modifier>(?<sign>[\+\-])(?<addend>\d*))?(?<floor>(?<floorseperator>[f|F])(?<floorlimit>(?<floorsign>[\+\-])?(?<floorlimiter>\d*)))?(?<ceiling>(?<ceilingseperator>[c|C])(?<ceilinglimit>(?<ceilingsign>[\+\-])?(?<ceilinglimiter>\d*)))?$");
        public static Regex sta_dice_regex = new Regex(@"^(?<numdice>\d*)(?<separator>(?i:sta|eff))\s*(?<comment>.*)$");

        public string Name => "Star Trek Adventures Dice";
        public List<string> Commands => new List<string> { "sta", "eff" }; // this could be in a config...

        public Config Config { get; set; }
        public DiscordSocketClient DiscordClient { get; set; }
        public Airtable Airtable { get; set; }

        public STARoller()
        {
        }

        public Task Command(string command, string message, SocketMessage m)
        {
            m.Author.ToString(); // this will key the db entry for the default character -- can specify ID afterwards
            return Task.CompletedTask;
        }

        public async Task Message(string message, SocketMessage m)
        {
            var sta_match = sta_dice_regex.Match(message);
            int num_dice = 1; // default is 1
            string comment = "";

            var separator = sta_match.Groups["separator"].Value.ToLower();

            if (!string.IsNullOrWhiteSpace(sta_match.Groups["numdice"].Value))
            {
                num_dice = int.Parse(sta_match.Groups["numdice"].Value);
            }

            if (!string.IsNullOrEmpty(sta_match.Groups["comment"].Value))
            {
                comment = sta_match.Groups["comment"].Value.Trim();
            }

            if (separator == "sta" && comment == string.Empty)
            {
                await m.Channel.SendMessageAsync($"{m.Author.Mention} -- Please enter a character name.");
            }
            else if (separator == "sta")
            {
                var roll_targets = (await Airtable.Get("Roll Targets")).OrderByDescending(rec => rec.Get<long>("Index", 0)).First();
                var complication_range = roll_targets.Get<long>("Complication Range", 20);
                var difficulty = roll_targets.Get<long>("Difficulty", 1);
                var focused = roll_targets.Get<bool>("Focused", false);
                var attribute = roll_targets.Get<string>("Attribute", "");
                var discipline = roll_targets.Get<string>("Discipline", "");

                try
                {
                    var character_stats = (await Airtable.Get("Characters", new List<string> {
                        "Character Name",
                        "Control", "Fitness", "Presence", "Daring", "Insight", "Reason",
                        "Command", "Security", "Science", "Conn", "Engineering", "Medicine"
                    })).Where(r => r.Get<string>("Character Name", "").ToLower().Contains(comment.ToLower())).First();

                    var target_number = character_stats.Get<long>(attribute, 7) + character_stats.Get<long>(discipline, 1);

                    var rolls = Enumerable.Range(0, num_dice).Select(i => Dice.Roll(20)).OrderBy(r => r).ToList();

                    var successes = rolls.Aggregate(0, (agg, item) =>
                    {
                        if (item == 1 || (item <= target_number && (bool)focused)) return agg + 2;
                        else if (item <= target_number) return agg + 1;
                        else return agg;
                    });

                    var momentum = successes > difficulty ? successes - difficulty : 0;

                    var complications = rolls.Where(roll => roll >= complication_range).Count();

                    await m.Channel.SendMessageAsync($"{m.Author.Mention} -- Rolls {rolls.Count()} d20's " +
                        $"using attribute **{attribute}** and discipline **{discipline}** " +
                        $"for character: **{character_stats.Get<string>("Character Name", "No One?")}**\n" +
                        $"**{successes}** successes with a target of **{target_number}** and a difficulty of **{difficulty}**\n" +
                        $"**{momentum}** momentum generated\n" +
                        $"**{complications}** complications with a complication range of **{complication_range} - 20**\n" +
                        $"Results: `[ {string.Join(" ", rolls)} ]`");
                }
                catch (InvalidOperationException e)
                {
                    Log.Warning($"Caught exception \"{e.Message}\" when trying to access character stats");

                    await m.Channel.SendMessageAsync($"{m.Author.Mention} -- could not find character that matches \"{comment}\"");
                }
            }
            else if (separator == "eff")
            {
                var rolls = Enumerable.Range(0, num_dice).Select(i => Dice.Roll(new List<string> { "1", "2", "X", "X", "E", "E" })).ToList();

                var count = rolls.Aggregate(0, (agg, item) =>
                {
                    if (item == "1" || item == "E") return agg + 1;
                    else if (item == "2") return agg + 2;
                    else return agg;
                });

                var effects = rolls.Where(r => r == "E").Count();

                await m.Channel.SendMessageAsync($"{m.Author.Mention} -- Rolls {rolls.Count()} challenge dice\n" +
                    $"**{count}** is the result\n" +
                    $"**{effects}** effects are generated\n" +
                    $"Results: `[ {string.Join(" ", rolls)} ]`");
            }
        }
    }
}
