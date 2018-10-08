using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Streams;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.DataStructures;
using TerrariaApi.Server;
using TShockAPI;

namespace Iframes {
    [ApiVersion (2, 1)]
    public class IFrames : TerrariaPlugin {
        Config config = new Config();
        public static long[] iframeTime = new long[256];

        public override string Name => "IFrames";
        public override string Author => "Johuan";
        public override string Description => "Custom iFrames for pvp";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        public IFrames(Main game) : base(game) {
        }

        public override void Initialize() {
            string path = Path.Combine(TShock.SavePath, "iframes.json");
            config = Config.Read(path);
            if (!File.Exists(path))
                config.Write(path);

            ServerApi.Hooks.NetGetData.Register(this, GetData);

            Commands.ChatCommands.Add(new Command("iframetime", ToggleIframes, "iframetime") { HelpText = "Usage: /iframetime <seconds>" });
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                ServerApi.Hooks.NetGetData.Deregister(this, GetData);

                string path = Path.Combine(TShock.SavePath, "pvpfixes.json");
                config.Write(path);
            }
            base.Dispose(disposing);
        }

        private void ToggleIframes(CommandArgs args) {
            double iframe = 0;
            if (args.Parameters.Count == 1 && Double.TryParse(args.Parameters[0], out iframe)) {
                config.iframeTime = (long)(iframe * 10000000);
                args.Player.SendSuccessMessage("Iframe time has been set to " + iframe + " seconds.");
            } else {
                args.Player.SendErrorMessage("Incorrect Syntax. Type /iframetime <seconds>");
            }
        }

        private void GetData(GetDataEventArgs args) {
            if (args.MsgID == PacketTypes.PlayerHurtV2) {
                //Collects all necessary data to alter pvp
                //The person who would be sending data is the attacking player
                var attackingplayer = TShock.Players[args.Msg.whoAmI];
                var data = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length);
                var damagedplayer = TShock.Players[data.ReadByte()];
                PlayerDeathReason playerHitReason = PlayerDeathReason.FromReader(new BinaryReader(data));

                if (attackingplayer == null || !attackingplayer.ConnectionAlive || !attackingplayer.Active) return;
                if (damagedplayer == null || !damagedplayer.ConnectionAlive || !damagedplayer.Active) return;
                if (!attackingplayer.TPlayer.hostile || !damagedplayer.TPlayer.hostile) return;
                if (playerHitReason.SourcePlayerIndex == -1) return;

                int damage = data.ReadInt16();
                int knockback = data.ReadByte() - 1;

                //Cancels client damage handling and makes it so the server does all the damage handling
                args.Handled = true;

                //Send the damage packet as long as they're not immune themselves
                if (!attackingplayer.TPlayer.immune) {
                    if (Math.Abs(iframeTime[damagedplayer.Index] - DateTime.Now.ToFileTimeUtc()) >= config.iframeTime) {
                        NetMessage.SendPlayerHurt(damagedplayer.Index, playerHitReason,
                            damage, knockback, false, true, 5);

                        iframeTime[damagedplayer.Index] = DateTime.Now.ToFileTimeUtc();
                    }
                }
            }
        }
    }
}
