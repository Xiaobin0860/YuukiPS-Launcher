﻿using DiscordRPC;
using DiscordRPC.Logging;
using Button = DiscordRPC.Button;

namespace YuukiPS_Launcher.Extra
{
    public class Discord
    {
        public DiscordRpcClient client;

        public void Ready(string appid = "1023479009335582830")
        {
            if (client == null)
            {
                client = new DiscordRpcClient(appid);
                client.Logger = new ConsoleLogger() { Level = LogLevel.Warning };
                client.RegisterUriScheme();

                //Subscribe to events
                client.OnReady += (sender, e) =>
                {
                    Console.WriteLine("Received Ready from user {0}", e.User.Username);
                    UpdateStatus("Getting ready", "Wait");
                };
                client.OnPresenceUpdate += (sender, e) =>
                {
                    Console.WriteLine("Received Update! {0}", e.Presence);
                };

                //Connect to the RPC
                client.Initialize();
            }

        }

        public void UpdateStatus(string details, string state, string iconkey = "")
        {
            if (client != null)
            {
                var Editor = new RichPresence()
                {
                    Details = details,
                    State = state
                };
                if (!string.IsNullOrEmpty(iconkey))
                {
                    Editor.Assets = new Assets()
                    {
                        LargeImageKey = iconkey,
                        LargeImageText = state
                    };
                }
                else
                {
                    Editor.Assets = new Assets()
                    {
                        LargeImageKey = "yuuki",
                        LargeImageText = "YuukiPS"
                    };
                }
                if (state.Contains("In Game"))
                {
                    Editor.Buttons = new Button[]
                     {
                       new Button() { Label = "Join", Url = "https://ps.yuuki.me/" }
                     };
                }
                client.SetPresence(Editor);
            }

        }

        public void Stop()
        {
            if (client != null)
            {
                client.Dispose();
                client = null;
            }
        }
    }
}