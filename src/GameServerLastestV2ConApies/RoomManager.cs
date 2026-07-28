using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SantanaLib.Collections.Concurrent;
using SantanaLib.Threading.Tasks;
using Santana;
using Santana.Network;
using Santana.Network.Message.Game;
using Santana.Game;
using ProudNetSrc;
using Serilog;
using Serilog.Core;

namespace Santana
{
    internal class RoomManager : IReadOnlyCollection<Room>
    {
        public static readonly ILogger Logger = Log.ForContext(Constants.SourceContextPropertyName, "RoomManager");
        public readonly ConcurrentDictionary<uint, Room> _rooms = new ConcurrentDictionary<uint, Room>();

        public RoomManager(Channel channel)
        {
            Channel = channel;
            GameRuleFactory = new GameRuleFactory();
        }

        public Channel Channel { get; }

        public GameRuleFactory GameRuleFactory { get; }

        public void Update(TimeSpan delta)
        {
            Parallel.ForEach(_rooms.Values, (current) =>
            {
                try
                {
                    if (current == null)
                        return;

                    if (current.Id == 0 || !current.Players.Any())
                    {
                        Remove(current);
                        return;
                    }

                    current?.Update(delta);
                }
                catch (Exception failure)
                {
                    Logger.Error(failure.ToString());
                }
            });
        }

        public Room Get(uint id)
        {
            _rooms.TryGetValue(id, out var found);
            return found;
        }

        public Room Create_2(RoomCreationOptions options)
        {
            try
            {
                uint nextId = 1;
                while (true)
                {
                    if (!_rooms.ContainsKey(nextId))
                        break;
                    nextId++;
                }

                var created = new Room(this, nextId, options, options.Creator);

                var info = created.GetRoomInfo();
                info.Password =
                    !string.IsNullOrWhiteSpace(created.Options.Password) ||
                    !string.IsNullOrEmpty(created.Options.Password)
                        ? "nice try :)"
                        : "";
                Channel.Broadcast(new RoomDeployAck2Message(info));

                return created;
            }
            catch (Exception failure)
            {
                Console.WriteLine(failure.ToString());
                return null;
            }
        }

        public Room Create(RoomCreationOptions options)
        {
            try
            {
                uint nextId = 1;
                while (true)
                {
                    if (!_rooms.ContainsKey(nextId))
                        break;
                    nextId++;
                }

                var created = new Room(this, nextId, options, options.Creator);
                var info = created.GetRoomInfo();
                info.Password =
                    !string.IsNullOrWhiteSpace(created.Options.Password) ||
                    !string.IsNullOrEmpty(created.Options.Password)
                        ? "nice try :)"
                        : "";
                Channel.Broadcast(new RoomDeployAckMessage(info));

                return created;
            }
            catch (Exception failure)
            {
                Console.WriteLine(failure.ToString());
                return null;
            }
        }

        public void Remove(Room room)
        {
            if (room == null || room.Disposed || !_rooms.ContainsKey(room.Id) || room.Players.Count() > 0)
                return;

            _rooms.Remove(room.Id);
            Channel.Broadcast(new RoomDisposeAckMessage(room.Id));
            room.Dispose();
        }

        #region Events

        #endregion

        #region IReadOnlyCollection

        public int Count => _rooms.Count;

        public Room this[uint id] => Get(id);

        public IEnumerator<Room> GetEnumerator()
        {
            return _rooms.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
