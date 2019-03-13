using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Discord.WebSocket;
using Serilog;

namespace STA_Discord
{
    class ComponentManager
    {
        private readonly List<IComponent> _components;
        private readonly string _prefix;

        public Config Config { get; set; }
        public DiscordSocketClient DiscordClient { get; set; }
        public Airtable Airtable { get; set; }

        public ComponentManager(Config config, DiscordSocketClient client, Airtable airtable)
        {
            Config = config;
            DiscordClient = client;
            Airtable = airtable;

            _prefix = config.Prefix;

            var assembly = System.Reflection.Assembly.GetEntryAssembly();

            _components = assembly
                .DefinedTypes
                .Where(ti => ti.ImplementedInterfaces.Contains(typeof(IComponent)))
                .Select(ti =>
                {
                    var component = (IComponent)assembly.CreateInstance(ti.FullName);
                    component.Config = config;
                    component.DiscordClient = client;
                    component.Airtable = airtable;
                    return component;
                })
                .ToList();

            Log.Information("Loaded {components}", _components.Select(comp => comp.Name).ToList());
        }

        public async Task Dispatch(SocketMessage m)
        {
            foreach (IComponent comp in _components)
            {
                await comp.Message(m.Content, m);
            }

            if (m.Content.StartsWith(_prefix))
            {
                var command = m.Content.Split(' ').First().TrimStart(_prefix.ToCharArray()).ToLower();
                foreach (IComponent comp in _components)
                {
                    if (comp.Commands.Contains(command))
                    {
                        await comp.Command(command, m.Content, m);
                    }
                }
            }
        }
    }
}
