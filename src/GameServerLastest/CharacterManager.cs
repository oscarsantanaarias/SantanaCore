namespace Santana
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Text;
    using SantanaLib.Threading.Tasks;
    using Dapper.FastCrud;
    using ExpressMapper.Extensions;
    using Santana.Database.Game;
    using Santana.Network;
    using Santana.Network.Data.Game;
    using Santana.Network.Message.Game;
    using Santana.Resource;
    using Serilog;
    using Serilog.Core;
    internal class CharacterManager : IReadOnlyCollection<Character>
    {
        private static readonly ILogger Logger =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(CharacterManager));
        public readonly ConcurrentDictionary<byte, Character>
            _characters = new ConcurrentDictionary<byte, Character>();
        private readonly ConcurrentStack<Character> _pendingDelete = new ConcurrentStack<Character>();
        private readonly AsyncLock _gate = new AsyncLock();
        internal CharacterManager(Player plr, PlayerDto dto)
        {
            Player = plr;
            CurrentSlot = dto.CurrentCharacterSlot;
            Boosts = new BoostManager(plr);
            foreach (var stored in dto.Characters)
            {
                var loaded = new Character(this, stored);
                if (!_characters.TryAdd(loaded.Slot, loaded))
                    { }
            }
        }
        public Player Player { get; }
        public Character CurrentCharacter => GetCharacter(CurrentSlot);
        public byte CurrentSlot { get; private set; }
        public BoostManager Boosts { get; }
        public Character this[byte slot] => GetCharacter(slot);
        public int Count => _characters.Count;
        public void DecreaseDurability(int loss)
        {
            try
            {
                var current = CurrentCharacter;
                if (current == null)
                    return;
                var worn = new List<ItemDurabilityInfoDto>();
                foreach (var gear in current.Weapons.GetItems())
                {
                    if (gear == null || gear.Durability == -1)
                        continue;
                    worn.Add(gear.LoseDurability(loss));
                }
                foreach (var gear in current.Costumes.GetItems())
                {
                    if (gear == null || gear.Durability == -1)
                        continue;
                    worn.Add(gear.LoseDurability(loss));
                }
                foreach (var gear in current.Skills.GetItems())
                {
                    if (gear == null || gear.Durability == -1)
                        continue;
                    worn.Add(gear.LoseDurability(loss));
                }
                Player?.SendAsync(new ItemDurabilityItemAckMessage(worn.ToArray()));
            }
            catch (Exception)
            {
            }
        }
        public IEnumerator<Character> GetEnumerator()
        {
            return _characters.Values.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public Character GetCharacter(byte slot)
        {
            return _characters?.GetValueOrDefault(slot);
        }
        public Character Create(byte slot, CharacterGender gender)
        {
            if (Count >= 3)
            {
                throw new CharacterException("Character limit reached");
            }
            if (_characters.ContainsKey(slot))
            {
                throw new CharacterException($"Slot {slot} is already in use");
            }
            var fresh = new Character(this, slot, gender);
            var inserted = _characters.TryAdd(slot, fresh);
            var style = new CharacterStyle(fresh.Gender, fresh.Slot);
            Player.Session.SendAsync(new CSuccessCreateCharacterAckMessage(fresh.Slot, style));
            return fresh;
        }
        public Character CreateFirst(byte slot, CharacterGender gender)
        {
            if (Count >= 3)
            {
                throw new CharacterException("Character limit reached");
            }
            if (_characters.ContainsKey(slot))
            {
                throw new CharacterException($"Slot {slot} is already in use");
            }
            var fresh = new Character(this, slot, gender);
            var inserted = _characters.TryAdd(slot, fresh);
            return fresh;
        }
        public bool Select(byte slot, bool silent = false)
        {
            if (!Contains(slot))
                return false;
            if (CurrentSlot != slot)
                Player.NeedsToSave = true;
            CurrentSlot = slot;
            if (!silent)
                Player.Session.SendAsync(new CharacterSelectAckMessage(CurrentSlot));
            return true;
        }
        public bool CheckChars()
        {
            for (var i = 0; i <= 3; i++)
            {
                if (Contains((byte)i))
                    return true;
            }
            return false;
        }
        public void Remove(Character @char)
        {
            Remove(@char.Slot);
        }
        public void Remove(byte slot)
        {
            if (Count == 1)
                throw new ArgumentException($"Slot {slot} is the last char", nameof(slot));
            var doomed = GetCharacter(slot);
            if (doomed == null)
                throw new ArgumentException($"Slot {slot} does not exist", nameof(slot));
            _characters.TryRemove(slot, out _);
            if (doomed.ExistsInDatabase)
                _pendingDelete.Push(doomed);
            Player.Session.SendAsync(new CharacterDeleteAckMessage(slot));
        }
        private void SetDtoItems(Character @char, PlayerCharacterDto charDto)
        {
            PlayerItem equipped;
            for (var weaponSlot = WeaponSlot.Weapon1; weaponSlot <= WeaponSlot.Weapon3; weaponSlot++)
            {
                equipped = @char.Weapons.GetItem(weaponSlot);
                var refId = equipped != null ? (int?)equipped.Id : null;
                switch (weaponSlot)
                {
                    case WeaponSlot.Weapon1:
                        charDto.Weapon1Id = refId;
                        break;
                    case WeaponSlot.Weapon2:
                        charDto.Weapon2Id = refId;
                        break;
                    case WeaponSlot.Weapon3:
                        charDto.Weapon3Id = refId;
                        break;
                }
            }
            equipped = @char.Skills.GetItem(SkillSlot.Skill);
            charDto.SkillId = equipped != null ? (int?)equipped.Id : null;
            for (var costumeSlot = CostumeSlot.Hair; costumeSlot <= CostumeSlot.Pet; costumeSlot++)
            {
                equipped = @char.Costumes.GetItem(costumeSlot);
                var refId = equipped != null ? (int?)equipped.Id : null;
                switch (costumeSlot)
                {
                    case CostumeSlot.Hair:
                        charDto.HairId = refId;
                        break;
                    case CostumeSlot.Face:
                        charDto.FaceId = refId;
                        break;
                    case CostumeSlot.Shirt:
                        charDto.ShirtId = refId;
                        break;
                    case CostumeSlot.Pants:
                        charDto.PantsId = refId;
                        break;
                    case CostumeSlot.Gloves:
                        charDto.GlovesId = refId;
                        break;
                    case CostumeSlot.Shoes:
                        charDto.ShoesId = refId;
                        break;
                    case CostumeSlot.Accessory:
                        charDto.AccessoryId = refId;
                        break;
                    case CostumeSlot.Pet:
                        charDto.PetId = refId;
                        break;
                }
            }
        }
        internal void Save(IDbConnection db)
        {
            if (!_pendingDelete.IsEmpty)
            {
                var deletedIds = new StringBuilder();
                var first = true;
                while (_pendingDelete.TryPop(out var gone))
                {
                    if (first)
                        first = false;
                    else
                        deletedIds.Append(',');
                    deletedIds.Append(gone.Id);
                }
                DbUtil.BulkDelete<PlayerCharacterDto>(db, statement => statement
                    .Where($"{nameof(PlayerCharacterDto.Id):C} IN ({deletedIds})"));
            }
            foreach (var entry in _characters.Values)
            {
                if (!entry.ExistsInDatabase)
                {
                    var newRow = new PlayerCharacterDto
                    {
                        PlayerId = (int)Player.Account.Id,
                        Slot = entry.Slot,
                        Gender = (byte)entry.Gender,
                    };
                    SetDtoItems(entry, newRow);
                    try
                    {
                        DbUtil.Insert(db, newRow);
                        entry.ExistsInDatabase = true;
                    }
                    catch (Exception ex)
                    {
                        throw;
                    }
                    continue;
                }
                if (!entry.NeedsToSave)
                    continue;
                var updateRow = new PlayerCharacterDto
                {
                    Id = entry.Id,
                    PlayerId = (int)Player.Account.Id,
                    Slot = entry.Slot,
                    Gender = (byte)entry.Gender,
                };
                SetDtoItems(entry, updateRow);
                DbUtil.Update(db, updateRow);
                entry.NeedsToSave = false;
            }
        }
        public bool Contains(byte slot)
        {
            return _characters.ContainsKey(slot);
        }
    }
}
