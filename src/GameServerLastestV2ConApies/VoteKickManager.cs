namespace Santana
{
    using System.Linq;
    using Network;
    using Network.Message.GameRule;

    internal class VoteKickManager
    {
        private Player Requester { get; set; }

        private Player Accused { get; set; }

        private uint TotalVotes { get; set; }

        private uint YesVotes { get; set; }

        private VoteKickReason KickReason { get; set; }

        public KickState State { get; internal set; }

        private Room HostRoom { get; set; }

        public VoteKickManager(Room room)
        {
            State = KickState.CanStart;
            HostRoom = room;
        }

        public void Start(Player sender, Player target, VoteKickReason reason)
        {
            if (State != KickState.CanStart)
                return;

            Requester = sender;
            Accused = target;
            KickReason = reason;

            TotalVotes++;
            YesVotes++;

            HostRoom.Broadcast(new GameKickOutStateAckMessage
            {
                DialogStyle = VoteKickDialogStyle.KickDialogWithSeconds,
                PlayerVoted = TotalVotes,
                YesCount = YesVotes,
                Reason = KickReason,
                Sender = Requester.Account.Id,
                Target = Accused.Account.Id
            });

            State = KickState.Execution;
        }

        public void Update()
        {
            var stillPresent = HostRoom.Players.FirstOrDefault(x => x.Value == Accused).Value;
            if (stillPresent != null)
                return;

            HostRoom.Broadcast(new GameKickOutStateAckMessage
            {
                DialogStyle = VoteKickDialogStyle.KickDialogCancelled,
                PlayerVoted = TotalVotes,
                YesCount = YesVotes,
                Reason = KickReason,
                Sender = Requester.Account.Id,
                Target = Accused.Account.Id
            });

            Clear();
        }

        public void UpdateResult(bool isYes)
        {
            TotalVotes += 1;
            YesVotes += isYes ? (uint)1 : 0;

            HostRoom.Broadcast(new GameKickOutStateAckMessage
            {
                DialogStyle = VoteKickDialogStyle.KickDialogWithoutSeconds,
                PlayerVoted = TotalVotes,
                YesCount = YesVotes,
                Reason = KickReason,
                Sender = Requester.Account.Id,
                Target = Accused.Account.Id
            });
        }

        public void Evaluate()
        {
            State = KickState.End;

            var needed = (HostRoom.Players.Count / 2) + 1;
            if (YesVotes >= needed)
            {
                HostRoom.Broadcast(new GameKickOutStateAckMessage
                {
                    DialogStyle = VoteKickDialogStyle.KickDialogPlayerKicked,
                    PlayerVoted = TotalVotes,
                    YesCount = YesVotes,
                    Reason = KickReason,
                    Sender = Requester.Account.Id,
                    Target = Accused.Account.Id
                });

                HostRoom.Leave(Accused, RoomLeaveReason.VoteKick);
            }
            else
            {
                HostRoom.Broadcast(new GameKickOutStateAckMessage
                {
                    DialogStyle = VoteKickDialogStyle.KickDialogNotKicked,
                    PlayerVoted = TotalVotes,
                    YesCount = YesVotes,
                    Reason = KickReason,
                    Sender = Requester.Account.Id,
                    Target = Accused.Account.Id
                });
            }

            Clear();
        }

        public void Clear()
        {
            Requester = null;
            Accused = null;
            KickReason = VoteKickReason.Etc;

            TotalVotes = 0;
            YesVotes = 0;
            State = KickState.CanStart;
        }

        internal enum KickState
        {
            CanStart,
            Execution,
            End
        }
    }
}
