using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace STA_Discord
{
    class DiscordMessage
    {
        public string Message;
        public Embed Data;

        public DiscordMessage()
        {
            // stub the default ctor
        }

        public DiscordMessage(string message)
        {
            // this is the ctor if we just have a string to send
            Message = message;
        }

        public DiscordMessage(Embed data)
        {
            // this is the ctor if we have embedded discord data to send
            Data = data;
        }

        public async Task SendToChannel(ISocketMessageChannel channel)
        {
            if (Data is null)
            {
                await channel.SendMessageAsync(Message);
            }
            else
            {
                await channel.SendMessageAsync("", false, Data);
            }
        }
    }
}
