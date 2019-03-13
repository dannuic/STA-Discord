using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using Discord;
using Discord.WebSocket;
using Serilog;

namespace STA_Discord
{
    class Program
    {
        static readonly string baseId = "app8AtpRpb5RZKWwE";
        static readonly string appKey = "key5Qx1z4wMFOzibF";

        private Config _config;
        private DiscordSocketClient _client;
        private Airtable _airtable;

        private ComponentManager _componentManager;

        // just defer to the async main
        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            // first, load the config
            LoadConfig();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose() // change this for live -- put level in the config
                .WriteTo.Console()
                .CreateLogger();

            // setup the discord client
            _client = new DiscordSocketClient();
            _client.Log += LogClientMessage; // add our local logger before we set anything up

            try
            {
                await _client.LoginAsync(TokenType.Bot, _config.DiscordToken);
                await _client.StartAsync();
            } catch (Exception e)
            {
                Log.Fatal("Could not log into Discord.net with token {token}", _config.DiscordToken);
                Log.Fatal("Message: {message}", e.Message);
                return; // we don't need to handle the exceptions because we're just going to exit. We've already logged all we can.
            }

            _client.MessageReceived += MessageReceived; // here is the discord message handler

            _airtable = new Airtable(_config);

            // now load the components
            _componentManager = new ComponentManager(_config, _client, _airtable);

            await Task.Delay(-1);
        }

        private void LoadConfig()
        {
            if (File.Exists(Config.FullPath))
            {
                _config = Config.Read();
            }
            else
            {
                Log.Fatal("Could not load config {0}, writing out default config.", Config.FullPath);
                _config = new Config();
                _config.Write();
            }
        }

        private Task MessageReceived(SocketMessage m)
        {
            // don't trigger on own messages
            if (m.Author.Id != _client.CurrentUser.Id)
            {
                Task.Run(() => _componentManager.Dispatch(m));
            }

            return Task.CompletedTask;
        }

        private Task LogClientMessage(LogMessage m)
        {
            if (m.Exception is null)
            {
                switch (m.Severity)
                {
                    case LogSeverity.Critical:
                        Log.Warning("Discord.net {Source}: {Message}", m.Source, m.Message);
                        break;
                    case LogSeverity.Debug:
                        Log.Debug("Discord.net {Source}: {Message}", m.Source, m.Message);
                        break;
                    case LogSeverity.Error:
                        Log.Error("Discord.net {Source}: {Message}", m.Source, m.Message);
                        break;
                    case LogSeverity.Info:
                        Log.Information("Discord.net {Source}: {Message}", m.Source, m.Message);
                        break;
                    case LogSeverity.Verbose:
                        Log.Verbose("Discord.net {Source}: {Message}", m.Source, m.Message);
                        break;
                    case LogSeverity.Warning:
                        Log.Warning("Discord.net {Source}: {Message}", m.Source, m.Message);
                        break;
                    default:
                        Log.Debug("Discord.net {Source}: {Message}", m.Source, m.Message);
                        break;
                }
            }
            else
            {
                Log.Fatal(m.Exception, "Exception caught by Discord.net");
            }

            return Task.CompletedTask;
        }
    }
}
