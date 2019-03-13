using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace STA_Discord
{
    public interface IComponent
    {
        string Name { get; }
        List<string> Commands { get; }

        Config Config { get; set; }
        DiscordSocketClient DiscordClient { get; set; }
        Airtable Airtable { get; set; }

        Task Command(string command, string message, SocketMessage m);
        Task Message(string message, SocketMessage m);
    }
}
