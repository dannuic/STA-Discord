﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;
using Discord.WebSocket;
using Serilog;

namespace STA_Discord.Components
{
    class STARoller : IComponent
    {
        public static Regex diceregex = new Regex(@"^(?<numdice>\d*)(?<dsides>(?<separator>[d|D|o|O|u|U])(?<numsides>\d+))(?<modifier>(?<sign>[\+\-])(?<addend>\d*))?(?<floor>(?<floorseperator>[f|F])(?<floorlimit>(?<floorsign>[\+\-])?(?<floorlimiter>\d*)))?(?<ceiling>(?<ceilingseperator>[c|C])(?<ceilinglimit>(?<ceilingsign>[\+\-])?(?<ceilinglimiter>\d*)))?$");
        public static Regex sta_dice_regex = new Regex(@"^(?<numdice>\d*)(?<separator>(?i:player|char|sta|assist|eff|cur))\s*(?<comment>.*)$");

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

            if (separator == "player" && comment != string.Empty)
            {
                var discord_id = m.Author.ToString();

                try
                {
                    var player_id = (await Airtable.Get("Players", new List<string> {
                        "Player Name", "Discord"
                    })).Single(r => r.Get<string>("Player Name", "").ToLower().Contains(comment.ToLower()));

                    if (discord_id != player_id.Get<string>("Discord", ""))
                    {
                        var updated_id = await Airtable.Update("Players", player_id.Id(), new List<(string, string)> {
                            ("Discord", discord_id)
                        });

                        await m.Channel.SendMessageAsync($"Updated {updated_id.Get<string>("Player Name", "")} to use discord tag {updated_id.Get<string>("Discord", "")}");
                    }
                    else
                    {
                        await m.Channel.SendMessageAsync($"{player_id.Get<string>("Player Name", "")} already uses discord tag {discord_id}");
                    }
                }
                catch (InvalidOperationException e)
                {
                    Log.Warning($"Caught exception \"{e.Message}\" when trying to access player records");

                    await m.Channel.SendMessageAsync($"{m.Author.Mention} -- could not find exactly one player that matches \"{comment}\"");
                }
            }
            else if (separator == "player")
            {
                try
                {
                    var player = await current_player(m);
                    await m.Channel.SendMessageAsync($"{m.Author.Mention} is associated with player {player.Get<string>("Player Name", "No One")}");
                }
                catch (InvalidOperationException e)
                {
                    await m.Channel.SendMessageAsync($"{m.Author.Mention} -- is not associated with a player.");
                }
            }
            else if (separator == "char" && comment != String.Empty)
            {
                try
                {
                    var player = await current_player(m);
                    var character = (await Airtable.Get("Characters", new List<string> {
                        "Character Name", "Active Player"
                    })).Single(r => r.Get<string>("Character Name", "").ToLower().Contains(comment.ToLower()));

                    var current_character = player.Get<JArray>("Active Character", new JArray()).FirstOrDefault();

                    if (current_character == null || current_character.Value<string>() != character.Id())
                    {
                        var updated_player = await Airtable.Update("Players", player.Id(), new List<(string, string)> {
                            ("Active Character", character.Id())
                        });

                        var updated_character = await Airtable.Update("Characters", character.Id(), new List<(string, string)> {
                            ("Active Player", player.Id())
                        });

                        await m.Channel.SendMessageAsync($"Updated {updated_player.Get<string>("Player Name", "")} to use character {updated_character.Get<string>("Character Name", "")}");
                    }
                    else
                    {
                        await m.Channel.SendMessageAsync($"{player.Get<string>("Player Name", "")} already uses character {character.Get<string>("Character Name", "")}");
                    }

                    Log.Information($"Active Character: {player.Get("Active Character").GetType()}, Active Player: {character.Get("Active Player").GetType()}");
                }
                catch (InvalidOperationException e)
                {
                    Log.Warning($"Caught exception \"{e.Message}\" when trying to access player and records");

                    await m.Channel.SendMessageAsync($"{m.Author.Mention} -- could not find exactly one character that matches \"{comment}\" or player that matches discord \"{m.Author.ToString()}\"");
                }
            }
            else if (separator == "char")
            {
                try
                {
                    var player = await current_player(m);
                    var character = (await Airtable.Get("Characters", new List<string> {
                        "Character Name", "Active Player"
                    })).Single(r => is_character(r, player, comment));

                    await m.Channel.SendMessageAsync($"{m.Author.Mention} is associated with character {character.Get<string>("Character Name", "No One")}");
                }
                catch (InvalidOperationException e)
                {
                    await m.Channel.SendMessageAsync($"{m.Author.Mention} -- is not associated with a character.");
                }
            }
            else if (separator == "sta")
            {
                try
                {
                    var roll_targets = (await Airtable.Get("Roll Targets")).OrderByDescending(rec => rec.Get<long>("Index", 0)).First();
                    var complication_range = roll_targets.Get<long>("Complication Range", 20);
                    var difficulty = roll_targets.Get<long>("Difficulty", 1);
                    var focused = roll_targets.Get<bool>("Focused", false);
                    var attribute = roll_targets.Get<string>("Attribute", "");
                    var discipline = roll_targets.Get<string>("Discipline", "");
                    var assists = roll_targets.Get<long>("Assists", 0);

                    var player = await current_player(m);
                    var character_stats = (await Airtable.Get("Characters", new List<string> {
                        "Character Name", "Active Player",
                        "Control", "Fitness", "Presence", "Daring", "Insight", "Reason",
                        "Command", "Security", "Science", "Conn", "Engineering", "Medicine"
                    })).Single(r => is_character(r, player, comment));

                    var target_number = character_stats.Get<long>(attribute, 7) + character_stats.Get<long>(discipline, 1);

                    var rolls = Enumerable.Range(0, num_dice).Select(i => Dice.Roll(20)).OrderBy(r => r).ToList();

                    var successes = rolls.Aggregate(0, (agg, item) =>
                    {
                        if (item == 1 || (item <= target_number && (bool)focused)) return agg + 2;
                        else if (item <= target_number) return agg + 1;
                        else return agg;
                    }) + (int)assists;

                    var momentum = successes > difficulty ? successes - difficulty : 0;

                    var complications = rolls.Where(roll => roll >= complication_range).Count();

                    await Airtable.Update("Roll Targets", roll_targets.Id(), new List<(string, string)> { ("Assists", "0") });

                    await m.Channel.SendMessageAsync($"{m.Author.Mention} -- Rolls {rolls.Count()} d20's " +
                        $"using attribute **{attribute}** and discipline **{discipline}** " +
                        $"for character: **{character_stats.Get<string>("Character Name", "No One?")}** with a target of **{target_number}**\n\n" +
                        $"***{(successes >= difficulty ? "SUCCESS!" : "F A I L U R E ...")}***\n\n" +
                        $"  **{successes}** __*[{successes - assists}+{assists}]*__ successes out of difficulty of **{difficulty}**\n" +
                        $"  **{momentum}** momentum generated\n" +
                        $"  **{complications}** complications with a complication range of **{complication_range} - 20**\n" +
                        $"Results: `[ {string.Join(" ", rolls)} ]`");
                }
                catch (InvalidOperationException e)
                {
                    Log.Warning($"Caught exception \"{e.Message}\" when trying to access character stats");

                    await m.Channel.SendMessageAsync($"{m.Author.Mention} -- no character is associated with this discord or could not find exactly one character that matches \"{comment}\"");
                }
            }
            else if (separator == "assist")
            {
                try
                {
                    var roll_targets = (await Airtable.Get("Roll Targets")).OrderByDescending(rec => rec.Get<long>("Index", 0)).First();
                    var complication_range = roll_targets.Get<long>("Complication Range", 20);
                    var focused = roll_targets.Get<bool>("Focused", false);
                    var attribute = roll_targets.Get<string>("Attribute", "");
                    var discipline = roll_targets.Get<string>("Discipline", "");
                    var assists = roll_targets.Get<long>("Assists", 0);

                    var player = await current_player(m);
                    var character_stats = (await Airtable.Get("Characters", new List<string> {
                        "Character Name", "Active Player",
                        "Control", "Fitness", "Presence", "Daring", "Insight", "Reason",
                        "Command", "Security", "Science", "Conn", "Engineering", "Medicine"
                    })).Single(r => is_character(r, player, comment));

                    var target_number = character_stats.Get<long>(attribute, 7) + character_stats.Get<long>(discipline, 1);

                    var rolls = Enumerable.Range(0, num_dice).Select(i => Dice.Roll(20)).OrderBy(r => r).ToList();

                    var successes = rolls.Aggregate(0, (agg, item) =>
                    {
                        if (item == 1 || (item <= target_number && (bool)focused)) return agg + 2;
                        else if (item <= target_number) return agg + 1;
                        else return agg;
                    });

                    var complications = rolls.Where(roll => roll >= complication_range).Count();

                    await Airtable.Update("Roll Targets", roll_targets.Id(), new List<(string, string)> { ("Assists", (successes + assists).ToString()) });

                    await m.Channel.SendMessageAsync($"{m.Author.Mention} -- Rolls {rolls.Count()} d20's " +
                        $"using attribute **{attribute}** and discipline **{discipline}** " +
                        $"for character: **{character_stats.Get<string>("Character Name", "No One?")}** with a target of **{target_number}**\n\n" +
                        $"  **{successes}** automatic successes added to the next skill roll!\n" +
                        $"  **{successes + assists}** total assists so far for the next skill roll\n" +
                        $"  **{complications}** complications with a complication range of **{complication_range} - 20**\n" +
                        $"Results: `[ {string.Join(" ", rolls)} ]`");
                }
                catch (InvalidOperationException e)
                {
                    Log.Warning($"Caught exception \"{e.Message}\" when trying to access character stats");

                    await m.Channel.SendMessageAsync($"{m.Author.Mention} -- no character is associated with this discord or could not find exactly one character that matches \"{comment}\"");
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
            else if (separator == "cur")
            {
                var roll_targets = (await Airtable.Get("Roll Targets")).OrderByDescending(rec => rec.Get<long>("Index", 0)).First();
                var momentum = roll_targets.Get<long>("Momentum", 0);
                var threat = roll_targets.Get<long>("Threat", 0);

                await m.Channel.SendMessageAsync($"Momentum: **{momentum}** -- Threat: **{threat}**");

                try
                {
                    var player = await current_player(m);
                    var character = (await Airtable.Get("Characters", new List<string> {
                        "Character Name", "Active Player", "Determination"
                    })).Single(r => is_character(r, player, comment));

                    await m.Channel.SendMessageAsync($"Determination for {character.Get<string>("Character Name", "No One")}: **{character.Get<long>("Determination", 0)}**");
                }
                catch (InvalidOperationException e)
                {
                    Log.Warning($"Caught exception \"{e.Message}\" when trying to access character determination");

                    await m.Channel.SendMessageAsync($"{m.Author.Mention} -- could not determine determination for specified character");
                }
            }
        }

        private bool is_character(Airtable.Record character_record, Airtable.Record player_record, string comment)
        {
            string active_player(Airtable.Record record)
            {
                var current_player_id = record.Get<JArray>("Active Player", new JArray()).FirstOrDefault();
                if (current_player_id != null)
                    return current_player_id.Value<string>();
                else
                    return "";
            }

            return comment != string.Empty ? character_record.Get<string>("Character Name", "").ToLower().Contains(comment.ToLower()) :
                (player_record != null && active_player(character_record) == player_record.Id());
        }

        private async Task<Airtable.Record> current_player(SocketMessage m)
        {
            try
            {
                return (await Airtable.Get("Players", new List<string> {
                    "Player Name", "Discord", "Active Character"
                })).Single(r => r.Get<string>("Discord", "") == m.Author.ToString());
            }
            catch (InvalidOperationException e)
            {
                Log.Warning($"Caught exception \"{e.Message}\" when trying to access player from discord");
                return null;
            }
        }
    }
}
