namespace Santana
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Data;
    using Dapper.FastCrud;
    using Database.Game;

    internal class PlayerSettingManager
    {
        private static readonly IDictionary<string, IPlayerSettingConverter> ConverterRegistry =
            new ConcurrentDictionary<string, IPlayerSettingConverter>();

        private readonly IDictionary<string, Setting> _entries = new ConcurrentDictionary<string, Setting>();

        static PlayerSettingManager()
        {
            var communityConverter = new CommunitySettingConverter();
            RegisterConverter(PlayerSetting.AllowCombiInvite, communityConverter);
            RegisterConverter(PlayerSetting.AllowFriendRequest, communityConverter);
            RegisterConverter(PlayerSetting.AllowRoomInvite, communityConverter);
            RegisterConverter(PlayerSetting.AllowInfoRequest, communityConverter);
        }

        public PlayerSettingManager(Player player, PlayerDto dto)
        {
            Player = player;

            foreach (var stored in dto.Settings)
            {
                _entries[stored.Setting] =
                    new Setting(GetObject(stored.Setting, stored.Value)) { ExistsInDatabase = true };
            }

            if (_entries.Count <= 0)
            {
                AddOrUpdate("AllowCombiInvite", 0);
                AddOrUpdate("AllowRoomInvite", 0);
                AddOrUpdate("AllowInfoRequest", 0);
                AddOrUpdate("AllowFriendRequest", 0);
            }
        }

        public Player Player { get; }

        public bool Contains(string name)
        {
            return _entries.ContainsKey(name);
        }

        public CommunitySetting GetSetting(string name)
        {
            if (!_entries.TryGetValue(name, out var entry))
                throw new Exception($"Setting {name} not found");
            Enum.TryParse(entry.Data.ToString(), out CommunitySetting parsed);
            return parsed;
        }

        public string Get(string name)
        {
            if (!_entries.TryGetValue(name, out var entry))
                throw new Exception($"Setting {name} not found");

            return (string)entry.Data;
        }

        public void AddOrUpdate(string name, string value)
        {
            if (_entries.TryGetValue(name, out var entry))
                entry.Data = value;
            else
                _entries[name] = new Setting(value);
        }

        public void AddOrUpdate<T>(string name, T value)
        {
            if (_entries.TryGetValue(name, out var entry))
                entry.Data = value;
            else
                _entries[name] = new Setting(value);
        }

        internal void Save(IDbConnection db)
        {
            foreach (var pair in _entries)
            {
                var name = pair.Key;
                var entry = pair.Value;
                if (!entry.ExistsInDatabase)
                {
                    DbUtil.Insert(db, new PlayerSettingDto
                    {
                        PlayerId = (int)Player.Account.Id,
                        Setting = name,
                        Value = GetString(name, entry.Data)
                    });
                    entry.ExistsInDatabase = true;
                }
                else
                {
                    if (!entry.NeedsToSave)
                        continue;

                    var row = new PlayerSettingDto
                    {
                        PlayerId = (int)Player.Account.Id,
                        Setting = name,
                        Value = GetString(name, entry.Data)
                    };

                    DbUtil.BulkUpdate(db,
                        row,
                        statement => statement
                            .Where(
                                $"{nameof(PlayerSettingDto.PlayerId):C} = @PlayerId AND {nameof(PlayerSettingDto.Setting):C} = @Setting")
                            .WithParameters(new { PlayerId = (int)Player.Account.Id, Setting = name }));
                    entry.NeedsToSave = false;
                }
            }
        }

        private class Setting
        {
            private object _value;

            public Setting(object data)
            {
                _value = data;
            }

            public bool ExistsInDatabase { get; set; }
            public bool NeedsToSave { get; set; }

            public object Data
            {
                get => _value;
                set
                {
                    if (_value == value)
                        return;
                    _value = value;
                    NeedsToSave = true;
                }
            }
        }

        #region Converter

        public static void RegisterConverter(string name, IPlayerSettingConverter converter)
        {
            if (!ConverterRegistry.TryAdd(name, converter))
                throw new Exception($"Converter for {name} already registered");
        }

        public static void RegisterConverter(PlayerSetting name, IPlayerSettingConverter converter)
        {
            RegisterConverter(name.ToString(), converter);
        }

        private static IPlayerSettingConverter GetConverter(string name)
        {
            ConverterRegistry.TryGetValue(name, out var converter);
            return converter;
        }

        private static object GetObject(string name, string value)
        {
            var converter = GetConverter(name);
            return converter != null ? converter.GetObject(value) : value;
        }

        private static string GetString(string name, object value)
        {
            var converter = GetConverter(name);
            return converter != null ? converter.GetString(value) : (string)value;
        }

        #endregion
    }

    internal interface IPlayerSettingConverter
    {
        object GetObject(string value);
        string GetString(object value);
    }

    internal class CommunitySettingConverter : IPlayerSettingConverter
    {
        public object GetObject(string value)
        {
            if (!Enum.TryParse(value, out CommunitySetting setting))
                throw new Exception($"CommunitySetting {value} not found");
            return setting;
        }

        public string GetString(object value)
        {
            return value.ToString();
        }
    }
}
