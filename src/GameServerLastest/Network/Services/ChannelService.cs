using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SantanaLib;
using SantanaLib.DotNetty.Handlers.MessageHandling;
using ExpressMapper.Extensions;
using Santana.Network.Data.Chat;
using Santana.Network.Data.Game;
using Santana.Network.Message.Chat;
using Santana.Network.Message.Game;
using ProudNetSrc;
using ProudNetSrc.Handlers;
using Serilog;
using Serilog.Core;
namespace Santana.Network.Services
{
    internal class ChannelService : ProudMessageHandler
    {
        private static readonly ILogger Logger =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(ChannelService));
        [MessageHandler(typeof(ChannelInfoReqMessage))]
        public void ChannelInfoReq(GameSession session, ChannelInfoReqMessage message)
        {
            var player = session.Player;
            if (player.Room != null)
                return;
            if (player.Channel == null)
            {
                try
                {
                    GameServer.Instance.ChannelManager[0].Join(player);
                }
                catch (Exception)
                {
                }
            }
            try
            {
                switch (message.Request)
                {
                    case ChannelInfoRequest.ChannelList:
                        var dtoList = GameServer.Instance.ChannelManager
                            .Select(ch => ch.Map<Channel, ChannelInfoDto>())
                            .Skip(1)
                            .ToArray();
                        foreach (var dto in dtoList)
                        {
                            if (dto.Name.Contains("Clan"))
                                dto.IsClanChannel = true;
                        }
                        session.SendAsync(new ChannelListInfoAckMessage(dtoList));
                        break;
                    case ChannelInfoRequest.RoomList:
                    case ChannelInfoRequest.RoomList2:
                        if (player?.Channel == null)
                            return;
                        var rooms = new List<RoomDto>();
                        foreach (var room in player.Channel.RoomManager)
                        {
                            if (room == null || room.Disposed)
                                continue;
                            if (!room.TeamManager.Players.Any())
                                continue;
                            var roomDto = room.GetRoomInfo();
                            var hasPassword =
                                !string.IsNullOrWhiteSpace(room.Options.Password) ||
                                !string.IsNullOrEmpty(room.Options.Password);
                            roomDto.Password = hasPassword ? "..." : string.Empty;
                            rooms.Add(roomDto);
                        }
                        session.SendAsync(new RoomListInfoAck2Message(rooms.ToArray()),
                            SendOptions.ReliableSecureCompress);
                        break;
                    default:
                        Logger.ForAccount(session)
                            .Error("Room listing asked for mode {request}, which maps to no known branch", message.Request);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }
        [MessageHandler(typeof(ChannelEnterReqMessage))]
        public void ChannelEnterReq(GameSession session, ChannelEnterReqMessage message)
        {
            var player = session.Player;
            if (player.Room != null)
                return;
            var target = GameServer.Instance.ChannelManager[message.Channel];
            if (target == null)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.NonExistingChannel));
                return;
            }
            var isClanChannel = target.Name?.Contains("Clan") ?? false;
            if (isClanChannel)
            {
                var clubId = player?.Club?.Id ?? 0;
                if (clubId <= 0)
                {
                    session.SendAsync(new ServerResultAckMessage(ServerResult.JoinChannelFailed));
                    return;
                }
                session.SendAsync(new NoticeAdminMessageAckMessage("ACTIVATE_MATCH"));
            }
            player.Channel?.Leave(player);
            try
            {
                if (target.Id == 5)
                {
                    if (player.IsRankReg)
                        target.Join(player);
                    return;
                }
                target.Join(player);
            }
            catch (ChannelLimitReachedException)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.ChannelLeave));
                session.SendAsync(new ServerResultAckMessage(ServerResult.ChannelLimitReached));
            }
            catch (ChannelLevelLimitException)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.ChannelLeave));
                session.SendAsync(new ServerResultAckMessage(ServerResult.JoinChannelFailed));
            }
            catch (ChannelException)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.ChannelLeave));
                session.SendAsync(new ServerResultAckMessage(ServerResult.JoinChannelFailed));
            }
        }
        [MessageHandler(typeof(ChannelLeaveReqMessage))]
        public void ChannelLeaveReq(GameSession session)
        {
            var player = session.Player;
            if (player?.Room != null)
                return;
            player?.Channel?.Leave(player);
            GameServer.Instance.ChannelManager[0].Join(player);
        }
        [MessageHandler(typeof(MessageChatReqMessage))]
        public void MessageChatReq(ChatSession session, MessageChatReqMessage message)
        {
            var player = session.Player;
            switch (message.ChatType)
            {
                case ChatType.Channel:
                    player?.Channel?.SendChatMessage(player, message.Message);
                    break;
                case ChatType.Club:
                    if (player?.Club?.Id > 0)
                    {
                        foreach (var clanMate in GameServer.Instance.PlayerManager
                            .Where(p => p.Club == player.Club))
                        {
                            clanMate.SendAsync(new MessageChatAckMessage(ChatType.Club,
                                player.Account.Id,
                                player.Account.Nickname, message.Message));
                        }
                    }
                    break;
                default:
                    break;
            }
        }
        [MessageHandler(typeof(MessageWhisperChatReqMessage))]
        public async Task MessageWhisperChatReq(ChatSession session, MessageWhisperChatReqMessage message)
        {
            var sender = session.Player;
            var recipient = GameServer.Instance.PlayerManager.Get(message.ToNickname);
            if (string.Equals(message.ToNickname, "game", StringComparison.CurrentCultureIgnoreCase) &&
                sender.Account.SecurityLevel >= SecurityLevel.GameSage)
            {
                try
                {
                    var parts = message.Message.GetArgs();
                    var flag = parts[0];
                    var chipKind = parts[1].ToLower();
                    if (flag == "/f")
                    {
                        if (chipKind == "strong")
                            await sender?.SendAsync(new EspherChipLv5Message(0));
                        else if (chipKind == "solid")
                            await sender?.SendAsync(new EspherChipLv5Message(1));
                        else if (chipKind == "special")
                            await sender?.SendAsync(new EspherChipLv5Message(2));
                        else if (chipKind == "shiny")
                            await sender?.SendAsync(new EspherChipLv5Message(3));
                        else if (chipKind == "style")
                            await sender?.SendAsync(new EspherChipLv5Message(4));
                    }
                }
                catch { }
                return;
            }
            if (string.Equals(message.ToNickname, "server", StringComparison.CurrentCultureIgnoreCase) &&
                sender.Account.SecurityLevel >= SecurityLevel.GameSage)
            {
                var serverArgs = message.Message.GetArgs();
                if (!await GameServer.Instance.CommandManager.Execute(sender, serverArgs))
                {
                    await sender.ChatSession.SendAsync(new MessageChatAckMessage(ChatType.Channel,
                        sender.Account.Id, "System",
                        "Unknown command! Try to contact the server administrators"));
                }
            }
            else if (string.Equals(message.ToNickname, "clan", StringComparison.CurrentCultureIgnoreCase))
            {
                var clanArgs = new List<string> { "/clan" };
                clanArgs.AddRange(message.Message.GetArgs());
                if (!await GameServer.Instance.CommandManager.Execute(sender, clanArgs.ToArray()))
                {
                    await sender.ChatSession.SendAsync(new MessageChatAckMessage(ChatType.Channel,
                        sender.Account.Id, "Clan", "An error occoured"));
                }
            }
            else
            {
                if (recipient == null)
                {
                    await sender.SendAsync(new MessageWhisperChatAckMessage(3, message.ToNickname,
                        sender.Account.Id, sender.Account.Nickname, message.Message));
                    return;
                }
                if (sender.DenyManager.Contains(recipient.Account.Id))
                {
                    await sender.SendAsync(new MessageWhisperChatAckMessage(3, message.ToNickname,
                        sender.Account.Id, sender.Account.Nickname, message.Message));
                    return;
                }
                if (recipient.DenyManager.Contains(sender.Account.Id))
                {
                    await sender.SendAsync(new MessageWhisperChatAckMessage(3, message.ToNickname,
                        sender.Account.Id, sender.Account.Nickname, message.Message));
                    return;
                }
                await recipient.SendAsync(new MessageWhisperChatAckMessage(0, recipient.Account.Nickname,
                    sender.Account.Id, sender.Account.Nickname, message.Message));
            }
        }
        [MessageHandler(typeof(RoomQuickStartReqMessage))]
        public void RoomQuickStartReq(GameSession session, RoomQuickStartReqMessage message)
        {
            session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
        }
        [MessageHandler(typeof(TaskReguestReqMessage))]
        public void TaskReguestReq(GameSession session, TaskReguestReqMessage message)
        {
            session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
        }
        [MessageHandler(typeof(ChannellistReqMessage))]
        public void Channellistreq(ChatSession session, ChannellistReqMessage message)
        {
            session.Player?.Channel.SendPlayerlist(session.Player);
        }
    }
}
