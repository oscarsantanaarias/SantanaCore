using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SantanaLib.DotNetty.Handlers.MessageHandling;
using ExpressMapper.Extensions;
using Santana.Database.Game;
using Santana.Network.Data.Game;
using Santana.Network.Message.Game;
using Santana.Resource;
using ProudNetSrc.Handlers;
using Serilog;
using Serilog.Core;
namespace Santana.Network.Services
{
    internal class InventoryService : ProudMessageHandler
    {
        private static readonly ILogger Logger =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(InventoryService));
        [MessageHandler(typeof(ItemUseItemReqMessage))]
        public void UseItemHandler(GameSession session, ItemUseItemReqMessage message)
        {
            if (session?.Player == null || message == null)
            {
                return;
            }
            if (message.Action == UseItemAction.UnEquip && message.ItemId == 0)
            {
                session.SendAsync(new ItemUseItemAckMessage(message.Action, message.CharacterSlot, message.EquipSlot,
                    message.ItemId));
                return;
            }
            var owner = session.Player;
            var targetChar = owner.CharacterManager[message.CharacterSlot];
            var invItem = owner.Inventory[message.ItemId];
            if (targetChar == null || invItem == null || owner.Room != null && owner.RoomInfo.State != PlayerState.Lobby)
            {
                session.SendAsync(new ItemUseItemAckMessage(UseItemAction.NoAction, message.CharacterSlot,
                    message.EquipSlot,
                    message.ItemId));
                return;
            }
            try
            {
                if (message.Action == UseItemAction.Equip)
                    targetChar.Equip(invItem, message.EquipSlot);
                else if (message.Action == UseItemAction.UnEquip)
                    targetChar.UnEquip(invItem.ItemNumber.Category, message.EquipSlot, invItem.ItemNumber);
            }
            catch (CharacterException error)
            {
                Logger.ForAccount(session)
                    .Error(error.Message, "Equip/unequip rejected by the character manager");
                session.SendAsync(new ItemUseItemAckMessage(UseItemAction.NoAction, message.CharacterSlot,
                    message.EquipSlot,
                    message.ItemId));
                session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
            }
        }
        [MessageHandler(typeof(ItemRepairItemReqMessage))]
        public void RepairItemHandler(GameSession session, ItemRepairItemReqMessage message)
        {
            if (session?.Player == null || message?.Items == null || message.Items.Length == 0 || message.Items.Length > 128)
            {
                session?.SendAsync(new ItemRepairItemAckMessage { Result = ItemRepairResult.Error0 });
                return;
            }
            var shopTable = GameServer.Instance.ResourceCache.GetShop();
            foreach (var requestedId in message.Items)
            {
                var invItem = session.Player.Inventory[requestedId];
                if (invItem == null)
                {
                    Logger.ForAccount(session)
                        .Error("Repair failed: item {id} is not in the player's inventory", requestedId);
                    session.SendAsync(new ItemRepairItemAckMessage { Result = ItemRepairResult.Error0 });
                    return;
                }
                if (invItem.Durability == -1)
                {
                    Logger.ForAccount(session)
                        .Error("Repair failed: {item} has no durability to restore",
                            new { invItem.ItemNumber, invItem.PriceType, invItem.PeriodType, invItem.Period });
                    session.SendAsync(new ItemRepairItemAckMessage { Result = ItemRepairResult.Error1 });
                    return;
                }
                var repairCost = invItem.CalculateRepair();
                if (session.Player.PEN < repairCost)
                {
                    session.SendAsync(new ItemRepairItemAckMessage { Result = ItemRepairResult.NotEnoughMoney });
                    return;
                }
                var shopPrice = shopTable.GetPrice(invItem);
                if (shopPrice == null)
                {
                    Logger.ForAccount(session)
                        .Error("Repair failed: no price record for {item}",
                            new { invItem.ItemNumber, invItem.PriceType, invItem.PeriodType, invItem.Period });
                    session.SendAsync(new ItemRepairItemAckMessage { Result = ItemRepairResult.Error4 });
                    return;
                }
                if (invItem.Durability >= shopPrice.Durability)
                {
                    session.SendAsync(new ItemRepairItemAckMessage
                    {
                        Result = ItemRepairResult.OK,
                        ItemId = invItem.Id
                    });
                    continue;
                }
                invItem.Durability = shopPrice.Durability;
                session.Player.PEN -= repairCost;
                session.SendAsync(new ItemRepairItemAckMessage { Result = ItemRepairResult.OK, ItemId = invItem.Id });
                session.SendAsync(new MoneyRefreshCashInfoAckMessage { PEN = session.Player.PEN, AP = session.Player.AP });
            }
        }
        [MessageHandler(typeof(ItemRefundItemReqMessage))]
        public void RefundItemHandler(GameSession session, ItemRefundItemReqMessage message)
        {
            if (session?.Player == null || message == null)
            {
                return;
            }
            var shopTable = GameServer.Instance.ResourceCache.GetShop();
            var invItem = session.Player.Inventory[message.ItemId];
            if (invItem == null)
            {
                Logger.ForAccount(session)
                    .Error("Refund failed: item {itemId} is not in the player's inventory", message.ItemId);
                session.SendAsync(new ItemRefundItemAckMessage { Result = ItemRefundResult.Failed });
                return;
            }
            var shopPrice = shopTable.GetPrice(invItem);
            if (shopPrice == null)
            {
                Logger.ForAccount(session)
                    .Error("Refund failed: no price record for {item}",
                        new { invItem.ItemNumber, invItem.PriceType, invItem.PeriodType, invItem.Period });
                session.SendAsync(new ItemRefundItemAckMessage { Result = ItemRefundResult.Failed });
                return;
            }
            if (!shopPrice.CanRefund)
            {
                Logger.ForAccount(session)
                    .Error("Refund failed: {item} is flagged as non-refundable", new { invItem.ItemNumber, invItem.PriceType, invItem.PeriodType, invItem.Period });
                session.SendAsync(new ItemRefundItemAckMessage { Result = ItemRefundResult.Failed });
                return;
            }
            session.Player.PEN += invItem.CalculateRefund(shopPrice);
            session.Player.Inventory.Remove(invItem);
            session.SendAsync(new ItemRefundItemAckMessage { Result = ItemRefundResult.OK, ItemId = invItem.Id });
            session.SendAsync(new MoneyRefreshCashInfoAckMessage { PEN = session.Player.PEN, AP = session.Player.AP });
        }
        [MessageHandler(typeof(ItemDiscardItemReqMessage))]
        public void DiscardItemHandler(GameSession session, ItemDiscardItemReqMessage message)
        {
            if (session?.Player == null || message == null)
            {
                return;
            }
            var shopTable = GameServer.Instance.ResourceCache.GetShop();
            var invItem = session.Player.Inventory[message.ItemId];
            if (invItem == null)
            {
                Logger.ForAccount(session)
                    .Error("Discard failed: item {itemId} is not in the player's inventory", message.ItemId);
                session.SendAsync(new ItemDiscardItemAckMessage { Result = 2 });
                return;
            }
            var shopEntry = shopTable.GetItem(invItem.ItemNumber);
            if (shopEntry == null)
            {
                Logger.ForAccount(session)
                    .Error("Discard failed: no shop record for {item}",
                        new { invItem.ItemNumber, invItem.PriceType, invItem.PeriodType, invItem.Period });
                session.SendAsync(new ItemDiscardItemAckMessage { Result = 2 });
                return;
            }
            if (shopEntry.IsDestroyable == false)
            {
                Logger.ForAccount(session)
                    .Error("Discard failed: {item} is flagged as not destroyable",
                        new { invItem.ItemNumber, invItem.PriceType, invItem.PeriodType, invItem.Period });
                session.SendAsync(new ItemDiscardItemAckMessage { Result = 2 });
                return;
            }
            session.Player.Inventory.Remove(invItem);
            session.SendAsync(new ItemDiscardItemAckMessage { Result = 0, ItemId = invItem.Id });
        }
        [MessageHandler(typeof(ItemUseRecordResetReqMessage))]
        public async Task UserDeathmatchResetReq(GameSession session, ItemUseRecordResetReqMessage message)
        {
            try
            {
                if (session?.Player == null || message == null)
                {
                    return;
                }
                Player owner = session.Player;
                PlayerItem invItem = owner.Inventory[message.ItemId];
                if (invItem == null)
                {
                    await session.SendAsync(new ItemUseRecordResetAckMessage { Result = 1, Unk2 = 0 });
                    return;
                }
                using (var gameDb = GameDatabase.Open())
                {
                    if (invItem.ItemNumber == 4010000)
                    {
                        var playerRow = (await DbUtil.FindAsync<PlayerDto>(gameDb, statement => statement
                            .Where($"{nameof(PlayerDto.Id):C} = @Id")
                           .WithParameters(new { owner.Account.Id }))).FirstOrDefault();
                        if (owner.Account.Nickname.Contains("[") && owner.Account.Nickname.Contains("]"))
                        {
                            return;
                        }
                        return;
                    }
                    if (invItem.ItemNumber == 4010003)
                    {
                        if (!owner.Account.Nickname.Contains("[") && !owner.Account.Nickname.Contains("]"))
                        {
                            return;
                        }
                        owner.Inventory.Remove(invItem);
                        var currentNick = owner.Account.Nickname;
                        var openBracket = currentNick.IndexOf("[", StringComparison.Ordinal);
                        var closeBracket = currentNick.IndexOf("]", StringComparison.Ordinal);
                        if (openBracket < 0 || closeBracket <= openBracket)
                        {
                            return;
                        }
                        string bracketTag = currentNick.Substring(openBracket, closeBracket - openBracket);
                        currentNick = currentNick.Replace(bracketTag + "]", "");
                        var authRow = (await DbUtil.FindAsync<Database.Auth.AccountDto>(gameDb, statement => statement
                          .Where($"{nameof(Database.Auth.AccountDto.Id):C} = @Id")
                           .WithParameters(new { owner.Account.Id }))).FirstOrDefault();
                        if (authRow == null)
                        {
                            return;
                        }
                        authRow.Nickname = currentNick;
                        DbUtil.Update(gameDb, authRow);
                        await owner.Session.SendAsync(new ItemUseChangeNickAckMessage
                        {
                            Result = 0,
                            Unk2 = 0,
                            Unk3 = currentNick
                        });
                        return;
                    }
                    if (invItem.ItemNumber == 4010002)
                    {
                        var tdReset = new PlayerTouchDownDto
                        {
                            PlayerId = (int)owner.Account.Id,
                            Won = 0,
                            Loss = 0,
                            TD = 0,
                            TDAssist = 0,
                            Offense = 0,
                            OffenseAssist = 0,
                            Defense = 0,
                            DefenseAssist = 0,
                            Kill = 0,
                            KillAssist = 0,
                            OffenseRebound = 0,
                            Heal = 0
                        };
                        await DbUtil.UpdateAsync(gameDb, tdReset);
                        owner.Inventory.Remove(invItem);
                        await session.SendAsync(new ItemUseRecordResetAckMessage
                        {
                            Result = 0,
                            Unk2 = 0
                        });
                        await session.SendAsync(new PlayerAccountInfoAckMessage(owner.Map<Player, PlayerAccountInfoDto>()));
                        return;
                    }
                    var dmReset = new PlayerDeathMatchDto
                    {
                        PlayerId = (int)owner.Account.Id,
                        Won = 0,
                        Loss = 0,
                        KillAssists = 0,
                        Kills = 0,
                        Deaths = 0
                    };
                    await DbUtil.UpdateAsync(gameDb, dmReset);
                }
                owner.Inventory.Remove(invItem);
                await session.SendAsync(new ItemUseRecordResetAckMessage
                {
                    Result = 0,
                    Unk2 = 0
                });
                await session.SendAsync(new PlayerAccountInfoAckMessage(owner.Map<Player, PlayerAccountInfoDto>()));
            }
            catch (Exception error)
            {
                Logger.ForAccount(session).Error(error, "Could not clear the item usage record");
            }
        }
        [MessageHandler(typeof(UseInstantItemRemoveEffectReqMessage))]
        public void DeleteEnchant(GameSession session, UseInstantItemRemoveEffectReqMessage message)
        {
            var plr = session.Player;
            if (plr == null)
                return;
            var collectBookSelection = ShopService.ResolveCollectBookEffectSelection(
                plr,
                message.DelItemId,
                message.ItemId,
                message.EffectId,
                activeOnly: true);
            if (collectBookSelection.HasValue)
            {
                var selection = collectBookSelection.Value;
                if (!ShopService.DeactivatePlayerCollectBookReward(plr, selection.bookKey, selection.effectId))
                {
                    session.SendAsync(new ServerResultAckMessage(ServerResult.DBError));
                    return;
                }
                session.SendAsync(new UseInstantItemRemoveEffectAckMessage
                {
                    Unk = message.DelItemId,
                    Unk2 = message.ItemId,
                    Unk3 = 0,
                    Unk4 = message.EffectId
                });
                session.SendAsync(new CollectBook_ExpireBookReward_Ack { Unk = 0 });
                session.SendAsync(new CollectBook_BookUnRegist_Ack { Value = 0 });
                var collectBookInventory = ShopService.CreateCollectBookInventoryInfoAck(plr, true);
                var collectBookEffects = ShopService.LoadPlayerCollectBookEffects(plr);
                plr.CharacterManager.Boosts.PlayerNameTag();
                return;
            }
            var Del = session.Player.Inventory[message.DelItemId];
            var Item = session.Player.Inventory[message.ItemId];
            if (Item == null || Del == null || Item.PeriodType == ItemPeriodType.Units || Item.ItemNumber.Category > ItemCategory.Skill)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.DBError));
                return;
            }
            plr.Inventory.RemoveOrDecrease(Del);
            List<EffectNumber> effects = Item.Effects.ToList();
            effects.Remove(effects.Where(x => x == message.EffectId).FirstOrDefault());
            Item.Effects = effects.ToArray();
            Item.EnchantLvl -= 1;
            session.SendAsync(new UseInstantItemRemoveEffectAckMessage { Unk = message.DelItemId, Unk2 = message.ItemId, Unk3 = 0, Unk4 = message.EffectId });
            session.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update, Item.Map<PlayerItem, ItemDto>()));
        }
        [MessageHandler(typeof(ItemMPRefillReqMessage))]
        public void MPRefill(GameSession session, ItemMPRefillReqMessage message)
        {
            try
            {
                var plr = session.Player;
                if (plr == null)
                    return;
                var collectBookSelection = ShopService.ResolveCollectBookEffectSelection(
                    plr,
                    message.MPItemId,
                    message.ItemId,
                    0,
                    activeOnly: false);
                if (collectBookSelection.HasValue)
                {
                    var selection = collectBookSelection.Value;
                    if (!ShopService.ActivatePlayerCollectBookReward(plr, selection.bookKey, selection.effectId))
                    {
                        session.SendAsync(new ServerResultAckMessage(ServerResult.DBError));
                        return;
                    }
                    session.SendAsync(new ItemMPRefillAckMessage { Result = 0 });
                    plr.CharacterManager.Boosts.PlayerNameTag();
                    return;
                }
                var MP = session.Player.Inventory[message.MPItemId];
                var Item = session.Player.Inventory[message.ItemId];
                if (Item == null || MP == null || Item.ItemNumber.Category > ItemCategory.Skill)
                {
                    session.SendAsync(new ServerResultAckMessage(ServerResult.DBError));
                    return;
                }
                {
                }
                if (MP.ItemNumber == 4130000)
                {
                    if (session.Player.PEN < 1000)
                    {
                        session.SendAsync(new EnchantEnchantItemAckMessage(EnchantResult.NotEnoughMoney));
                        return;
                    }
                    List<EffectNumber> effects = new EffectNumber[0].ToList();
                    switch (Item.ItemNumber.Category)
                    {
                        case ItemCategory.Weapon:
                            effects.Add(1201300005);
                            effects.Add(1201301005);
                            effects.Add(1299600007);
                            effects.Add(1299602002);
                            break;
                        case ItemCategory.Costume:
                            if ((int)Item.ItemNumber.Id > 999999 && (int)Item.ItemNumber.Id < 1010000)
                            {
                                effects.Add(1100313007);
                                effects.Add(1100315007);
                                effects.Add(1100317007);
                                effects.Add(1999800001);
                            }
                            else if ((int)Item.ItemNumber.Id > 1010000 && (int)Item.ItemNumber.Id < 1019999)
                            {
                                effects.Add(1101301008);
                                effects.Add(1999800001);
                            }
                            else if ((int)Item.ItemNumber.Id > 1019999 && (int)Item.ItemNumber.Id < 1029999)
                            {
                                effects.Add(1102303008);
                                effects.Add(1999800001);
                            }
                            else if ((int)Item.ItemNumber.Id > 1029999 && (int)Item.ItemNumber.Id < 1040000)
                            {
                                effects.Add(1103302009);
                                effects.Add(1999800001);
                            }
                            else if ((int)Item.ItemNumber.Id > 1040000 && (int)Item.ItemNumber.Id < 1050000)
                            {
                                effects.Add(1104300008);
                                effects.Add(1999800001);
                            }
                            else if ((int)Item.ItemNumber.Id > 1050000 && (int)Item.ItemNumber.Id < 1059999)
                            {
                                effects.Add(1105300008);
                                effects.Add(1999800001);
                            }
                            else if ((int)Item.ItemNumber.Id > 1059999 && (int)Item.ItemNumber.Id < 1070000)
                            {
                                effects.Add(1106301008);
                                effects.Add(1999800001);
                            }
                            else if ((int)Item.ItemNumber.Id > 1070000 && (int)Item.ItemNumber.Id < 1999999)
                            {
                                effects.Add(1107301006);
                                effects.Add(1107302002);
                                effects.Add(1107307001);
                                effects.Add(1107800000);
                            }
                            break;
                        default:
                            break;
                    }
                    bool EffectExist = Item.Effects.Select(x => x)
                                  .Intersect(effects)
                                  .Any();
                    using (var db = GameDatabase.Open())
                    {
                        var plrDto = DbUtil.Find<PlayerItemDto>(db, statement => statement
                            .Where($"{nameof(PlayerItemDto.Id):C} = @{nameof(Item.Id)}")
                            .WithParameters(new { Item.Id }))
                           .FirstOrDefault();
                        var dtoEffects = "";
                        foreach (var eff in effects)
                            dtoEffects += eff + ",";
                        if (string.IsNullOrEmpty(dtoEffects) || !dtoEffects.EndsWith(","))
                        {
                            session.SendAsync(new ServerResultAckMessage(ServerResult.DBError));
                            return;
                        }
                        plrDto.Effects = dtoEffects.TrimEnd(',');
                        DbUtil.Update(db, plrDto);
                    }
                    session.Player.Inventory.RemoveOrDecrease(MP);
                    if (EffectExist)
                    {
                        session.SendAsync(new ItemMPRefillAckMessage { Result = 1 });
                        return;
                    }
                    else
                    {
                        Item.Effects = effects.ToArray();
                    }
                    session.Player.PEN -= 1000;
                    session.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update, Item.Map<PlayerItem, ItemDto>()));
                    session.SendAsync(new MoneyRefreshCashInfoAckMessage(session.Player.PEN, session.Player.AP));
                    session.SendAsync(new ItemMPRefillAckMessage { Result = 0 });
                }
                if (MP.ItemNumber == 4130001)
                {
                    var MPRequest = new uint[25] { 168, 168, 336, 336, 504, 504, 672, 672, 840,
                    840, 1176, 1176, 1512, 1680, 2016, 2688, 3360, 4032, 4872, 6048, 6048,
                    6048, 6048, 6048, 6048 };
                    var effects = new EffectNumber[0].ToList();
                    if (Item.EnchantMP >= MPRequest[Item.EnchantLvl])
                    {
                        session.SendAsync(new ItemMPRefillAckMessage { Result = 1 });
                        return;
                    }
                    plr.Inventory.RemoveOrDecrease(MP);
                    Item.EnchantMP += 168;
                    if (Item.EnchantMP >= MPRequest[Item.EnchantLvl])
                    {
                        Item.EnchantMP = MPRequest[Item.EnchantLvl];
                    }
                    session.SendAsync(new ItemMPRefillAckMessage { Result = 0 });
                    session.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update, Item.Map<PlayerItem, ItemDto>()));
                }
            }
            catch(Exception ex)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.DBError));
                Logger.Error(ex.ToString());
            }
        }
        [MessageHandler(typeof(UseEnchantChipReqMessage))]
        public void UseEnchantChipReq(GameSession session, UseEnchantChipReqMessage message)
        {
            var owner = session.Player;
            if (owner == null)
                return;
            var chipItem = session.Player.Inventory[message.ChipId];
            var baseItem = session.Player.Inventory[message.ItemId];
            if (baseItem == null || chipItem == null)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.DBError));
                return;
            }
            var carriedEffects = baseItem.Effects.Where(eff => eff >= 3100800001).ToArray();
            int carriedLevel = baseItem.EnchantLvl;
            List<EffectNumber> keptEffects = new List<EffectNumber>();
            keptEffects.AddRange(baseItem.Effects.Where(eff => eff <= 3100800001).ToArray());
            baseItem.Effects = keptEffects.ToArray();
            owner.Inventory.Update(baseItem.Id);
            session.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update, baseItem.Map<PlayerItem, ItemDto>()));
            if (baseItem.ItemNumber.Category == ItemCategory.Weapon)
            {
                switch (baseItem.ItemNumber.SubCategory)
                {
                    case (byte)WeaponCategory.Melee:
                        owner.Inventory.RemoveOrDecrease(chipItem);
                        owner.Inventory.Create(4190001, 1, 0, carriedEffects, 1, carriedLevel, true);
                        break;
                }
            }
        }
        [MessageHandler(typeof(MoveEnchantChipReqMessage))]
        public void MoveEnchantChipReq(GameSession session, MoveEnchantChipReqMessage message)
        {
            var owner = session.Player;
            if (owner == null)
                return;
            var chipItem = session.Player.Inventory[message.ChipId];
            var baseItem = session.Player.Inventory[message.ItemId];
            if (baseItem == null || chipItem == null)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.DBError));
                return;
            }
            if (baseItem.ItemNumber.Category == ItemCategory.Weapon)
            {
                switch (baseItem.ItemNumber.SubCategory)
                {
                    case (byte)WeaponCategory.Melee:
                        owner.Inventory.Remove(chipItem);
                        List<EffectNumber> keptEffects = new List<EffectNumber>();
                        keptEffects.AddRange(baseItem.Effects.Where(eff => eff <= 3100800001).ToArray());
                        baseItem.Effects = keptEffects.ToArray();
                        session.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update, baseItem.Map<PlayerItem, ItemDto>()));
                        break;
                }
            }
            session.SendAsync(new MoveEnchantChipAckMessage { Result = 1 });
        }
        [MessageHandler(typeof(ItemEnchanReqMessage))]
        public void ItemEnchanReq(GameSession session, ItemEnchanReqMessage message)
        {
            var owner = session.Player;
            if (owner == null)
                return;
            var baseItem = owner.Inventory[(ulong)message.ItemId];
            var boosterItemId = message.Unk2 == 0 ? message.Unk2 : 0;
            if (baseItem == null)
            {
                session.SendAsync(new EnchantEnchantItemAckMessage(EnchantResult.ErrorItemEnchant));
                return;
            }
            var random = new SecureRandom();
            var enchantTable = GameServer.Instance.ResourceCache.GetItemEnchant();
            var matchingEnchants = enchantTable.Where(x => x.Value.Category == baseItem.ItemNumber.Category && x.Value.SubCategory == baseItem.ItemNumber.SubCategory).ToList();
            var passedEnchants = matchingEnchants.Where(x => Enumerable.Range(x.Value.Chance, 101)
               .Count(i => random.Next(x.Value.Chance, 101) <= x.Value.Chance) >= 4).OrderByDescending(e => e.Value.Level).ToList();
            var chosenEffects = passedEnchants.Count() != 0 ? passedEnchants[random.Next(0, passedEnchants.Count())].Value.Effects : matchingEnchants[0].Value.Effects;
            var enchantPrice = new uint[] { 200, 200, 300, 300, 500, 500, 600, 600, 800, 800, 1100, 1100, 1300, 1500, 1800, 2300, 2900, 3500, 4200, 5200, 5200, 5200, 5200, 5200, 5200}[baseItem.EnchantLvl];
            if (owner.PEN < enchantPrice)
            {
                session.SendAsync(new EnchantEnchantItemAckMessage(EnchantResult.NotEnoughMoney));
                return;
            }
            var pickedEffectStr = chosenEffects.Split(',')[random.Next(0, chosenEffects.Split(',').Count())] ?? chosenEffects;
            var pickedEffect = Convert.ToUInt32(pickedEffectStr);
            List<EffectNumber> currentEffects = baseItem.Effects.ToList();
            var existingEffect = currentEffects.Where(x => x.ToString().Remove(x.ToString().Length - 1) == pickedEffectStr.Remove(pickedEffectStr.Length - 1)).FirstOrDefault();
            var jackpotBase = 10;
            var successBase = 0;
            if (baseItem.EnchantLvl >= 0 && baseItem.EnchantLvl < 3)
                successBase = 100;
            else if (baseItem.EnchantLvl >= 3 && baseItem.EnchantLvl <= 5)
                successBase = 80;
            else if (baseItem.EnchantLvl >= 6 && baseItem.EnchantLvl <= 8)
                successBase = 65;
            else if (baseItem.EnchantLvl >= 8 && baseItem.EnchantLvl <= 10)
                successBase = 50;
            else if (baseItem.EnchantLvl >= 10 && baseItem.EnchantLvl <= 12)
                successBase = 40;
            else if (baseItem.EnchantLvl >= 12 && baseItem.EnchantLvl <= 14)
                successBase = 30;
            else if (baseItem.EnchantLvl >= 15 && baseItem.EnchantLvl <= 17)
                successBase = 20;
            else if (baseItem.EnchantLvl >= 18 && baseItem.EnchantLvl <= 20)
                successBase = 15;
            if (boosterItemId > 0)
            {
                if (owner.Inventory[(ulong)boosterItemId].ItemNumber == 4080001)
                {
                    successBase += (successBase * 10) / 100;
                    owner.Inventory.RemoveOrDecrease(owner.Inventory[(ulong)boosterItemId]);
                }
                if (owner.Inventory[(ulong)boosterItemId].ItemNumber == 4080002)
                {
                    successBase += (successBase * 20) / 100;
                    owner.Inventory.RemoveOrDecrease(owner.Inventory[(ulong)boosterItemId]);
                }
                if (owner.Inventory[(ulong)boosterItemId].ItemNumber == 4080003)
                {
                    successBase = 100;
                    owner.Inventory.RemoveOrDecrease(owner.Inventory[(ulong)boosterItemId]);
                }
                if (owner.Inventory[(ulong)boosterItemId].ItemNumber == 4070001)
                {
                    jackpotBase = 20;
                    owner.Inventory.RemoveOrDecrease(owner.Inventory[(ulong)boosterItemId]);
                }
            }
            var successRoll = Enumerable.Range(successBase, 101)
               .Count(i => random.Next(successBase, 101) <= successBase);
            var jackpotRoll = Enumerable.Range(jackpotBase, 101)
               .Count(i => random.Next(jackpotBase, 101) <= jackpotBase);
            if (successRoll <= 3)
            {
                currentEffects.RemoveAll(x => Convert.ToInt32(x.Id.ToString().Remove(x.Id.ToString().Length - 9)) == 3);
                session.SendAsync(new EnchantEnchantItemAckMessage
                {
                    Result = EnchantResult.Reset,
                    ItemId = (ulong)message.ItemId,
                    Effect = 0
                });
                baseItem.Effects = currentEffects.ToArray();
                baseItem.EnchantLvl = 0;
                baseItem.EnchantMP = 0;
                owner.PEN -= enchantPrice;
                session.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update, baseItem.Map<PlayerItem, ItemDto>()));
                session.SendAsync(new MoneyRefreshCashInfoAckMessage(owner.PEN, owner.AP));
                return;
            }
            if (currentEffects.Contains(existingEffect))
            {
                var effStr = pickedEffect.ToString();
                currentEffects.Remove(existingEffect);
                int newTier = int.Parse(effStr.Last().ToString()) + 1;
                int oldTier = int.Parse(existingEffect.ToString().Last().ToString()) + 1;
                if (jackpotRoll >= 3)
                {
                    if (Convert.ToUInt32(effStr.Last()) >= 1 && Convert.ToUInt32(effStr.Last()) <= 5)
                    {
                        currentEffects.Add(Convert.ToUInt32(effStr.Remove(effStr.Length - 1) + (newTier + oldTier)));
                    }
                }
                else
                {
                    if (Convert.ToUInt32(effStr.Last()) >= 1 && Convert.ToUInt32(effStr.Last()) != 5)
                    {
                        currentEffects.Add(Convert.ToUInt32(effStr.Remove(effStr.Length - 1) + (newTier + oldTier)));
                    }
                    else
                        currentEffects.Add(pickedEffect);
                }
            }
            else
            {
                if (jackpotRoll >= 3)
                {
                    var effStr = pickedEffect.ToString();
                    if (Convert.ToUInt32(effStr.Last()) >= 1 && Convert.ToUInt32(effStr.Last()) <= 5)
                    {
                        currentEffects.Add(Convert.ToUInt32(effStr.Remove(effStr.Length - 1) + 1));
                    }
                }
                else
                    currentEffects.Add(pickedEffect);
            }
            baseItem.Effects = currentEffects.ToArray();
            baseItem.EnchantLvl++;
            baseItem.EnchantMP = 0;
            owner.PEN -= enchantPrice;
            session.SendAsync(new EnchantEnchantItemAckMessage
            {
                Result = EnchantResult.Success,
                ItemId = (ulong)message.ItemId,
                Effect = pickedEffect
            });
            session.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update, baseItem.Map<PlayerItem, ItemDto>()));
            session.SendAsync(new MoneyRefreshCashInfoAckMessage(owner.PEN, owner.AP));
        }
        [MessageHandler(typeof(EsperEnchantReqMessage))]
        public void EsperEnchant(GameSession session, EsperEnchantReqMessage message)
        {
            var owner = session.Player;
            session.SendAsync(new EsperEnchantAckMessage { Result = (int)message.EsperItemId, ItemId = message.ItemId });
        }
        [MessageHandler(typeof(EsperEnchantPercentUpReqMessage))]
        public void EsperEnchantPercentUp(GameSession session, EsperEnchantPercentUpReqMessage message)
        {
            var owner = session.Player;
            if (owner == null)
                return;
            var esperItem = owner.Inventory[message.EsperItemId];
            var esperTable = GameServer.Instance.ResourceCache.GetEsperEnchant();
            var esperData = esperTable.Where(x => x.Value.EsperId == esperItem.ItemNumber).FirstOrDefault().Value;
            var esperLevel = esperData.Level;
            var random = new SecureRandom();
            var rateRoll = Enumerable.Range(esperData.Rate, 101)
                    .Count(i => random.Next(esperData.Rate, 101) <= esperData.Rate);
            if (rateRoll >= 3 && esperLevel < 4)
            {
                esperLevel++;
                session.SendAsync(new EnchantEnchantItemAckMessage
                {
                    Result = EnchantResult.Success,
                    ItemId = message.EsperItemId,
                    Effect = esperData.Effect,
                });
            }
            else if (rateRoll >= 4 && esperLevel == 4)
            {
                esperLevel++;
                session.SendAsync(new EnchantEnchantItemAckMessage
                {
                    Result = EnchantResult.Success,
                    ItemId = message.EsperItemId,
                    Effect = esperData.Effect,
                });
            }
            else
            {
                session.SendAsync(new EsperEnchantAckMessage
                {
                    ItemId = message.EsperItemId,
                    Effect = esperData.Effect,
                });
            }
            session.Player.Inventory.Update(esperItem.Id);
        }
        [MessageHandler(typeof(ItemUseCapsuleReqMessage))]
        public async Task ItemUseCapsule(GameSession session, ItemUseCapsuleReqMessage message)
        {
            try
            {
                var rewardTable = GameServer.Instance.ResourceCache.GetItemRewards();
                var shopTable = GameServer.Instance.ResourceCache.GetShop();
                var owner = session.Player;
                var capsuleItem = owner.Inventory[message.ItemId];
                if (!rewardTable.ContainsKey(capsuleItem.ItemNumber))
                {
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.DBError));
                    return;
                }
                var capsuleReward = rewardTable[capsuleItem.ItemNumber];
                var rolledRewards = (from bag in capsuleReward.Bags
                               let picked = bag.Take()
                               select new CapsuleRewardDto
                               {
                                   RewardType = picked.Type,
                                   ItemNumber = picked.Item,
                                   PriceType = picked.PriceType,
                                   PeriodType = picked.PeriodType,
                                   Period = picked.Period,
                                   Color = picked.Color,
                                   PEN = picked.PEN,
                               }).ToArray();
                Dictionary<CapsuleRewardDto, EffectNumber[]> builtRewards = new Dictionary<CapsuleRewardDto, EffectNumber[]>();
                foreach (var rolled in rolledRewards)
                {
                    var itemInfo = shopTable.GetItemInfo(rolled.ItemNumber);
                    var effectList = new List<EffectNumber>();
                    if (itemInfo == null)
                    {
                        await session.SendAsync(new ItemUseCapsuleAckMessage(1));
                        return;
                    }
                    if (rolled.RewardType == CapsuleRewardType.Item)
                    {
                        foreach (var effect in itemInfo.EffectGroup.Effects)
                            effectList.Add(effect.Effect);
                    }
                    var rewardDto = new CapsuleRewardDto
                    {
                        RewardType = rolled.RewardType,
                        ItemNumber = rolled.ItemNumber,
                        PriceType = rolled.PriceType,
                        PeriodType = rolled.PeriodType,
                        Effect = rolled.RewardType == CapsuleRewardType.PEN ? 0 : itemInfo.EffectGroup.MainEffect,
                        Period = rolled.Period,
                        Color = (byte)rolled.Color,
                        PEN = rolled.PEN
                    };
                    builtRewards.Add(rewardDto, effectList.ToArray());
                }
                owner.Inventory.RemoveOrDecrease(capsuleItem);
                foreach (var entry in builtRewards)
                {
                    if (owner.CharacterManager.Boosts.GetUniqueBoosterRate() == 10)
                    {
                        if (entry.Key.PeriodType == ItemPeriodType.None)
                            owner.Inventory.Create(entry.Key.ItemNumber, 1, entry.Key.Color, entry.Value, 1);
                    }
                    else
                    {
                        if (entry.Key.RewardType == CapsuleRewardType.PEN)
                            owner.PEN += entry.Key.PEN;
                        else if (entry.Key.RewardType == CapsuleRewardType.AP)
                            owner.AP += entry.Key.PEN;
                        else if (entry.Key.RewardType == CapsuleRewardType.EXP)
                            owner.GainExp((int)entry.Key.PEN);
                        else
                        {
                            if (entry.Key.PeriodType == ItemPeriodType.None)
                                owner.Inventory.Create(entry.Key.ItemNumber, 1, entry.Key.Color, entry.Value, 1);
                            if (entry.Key.PeriodType == ItemPeriodType.Units)
                                owner.Inventory.CreateUnits(entry.Key.ItemNumber, entry.Key.Period);
                            if (entry.Key.PeriodType == ItemPeriodType.Days)
                                owner.Inventory.CreateDays(entry.Key.ItemNumber, (ushort)entry.Key.Period, entry.Key.Color);
                        }
                    }
                }
                await session.SendAsync(new ItemUseCapsuleAckMessage(builtRewards.Keys.ToArray(), 3));
                await session.SendAsync(new MoneyRefreshCashInfoAckMessage(owner.PEN, owner.AP));
            }
            catch (Exception error)
            {
                await session.SendAsync(new ServerResultAckMessage(ServerResult.DBError));
                Logger.Error(error.ToString());
            }
        }
    }
}
