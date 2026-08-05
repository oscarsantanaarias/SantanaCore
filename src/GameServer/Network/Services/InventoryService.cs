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
                targetChar.Costumes.RefreshEsperSkill();
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
                if (MP.ItemNumber == 4130006)
                {
                    if (Item.EsperChip == 0)
                    {
                        session.SendAsync(new ItemMPRefillAckMessage { Result = 1 });
                        return;
                    }
                    var extracted = Item.EsperChip;
                    var chipEffects = GetEsperChipEffects(extracted);
                    var remaining = Item.Effects.Where(x => x.Id != 0).ToList();
                    foreach (var effect in chipEffects)
                        remaining.Remove(effect);
                    Item.Effects = remaining.ToArray();
                    Item.EsperChip = 0;
                    Item.NeedsToSave = true;
                    plr.Inventory.RemoveOrDecrease(MP);
                    plr.Inventory.CreateUnits(extracted, 1);
                    session.SendAsync(new ItemMPRefillAckMessage { Result = 0 });
                    session.SendAsync(new ItemInventroyDeleteAckMessage(Item.Id));
                    session.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Add,
                        Item.Map<PlayerItem, ItemDto>()));
                    RestoreEquipSlot(session, Item);
                    return;
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
            if (IsEsperChip(chipItem))
            {
                if (!MatchesEsperChipSlot(chipItem, baseItem))
                {
                    session.SendAsync(new ServerResultAckMessage(ServerResult.DBError));
                    return;
                }
                var keptEsperEffects = baseItem.Effects
                    .Where(x => x.Id != 0 && (x.Id < 2100000000 || x.Id > 2299999999))
                    .ToList();
                keptEsperEffects.AddRange(chipItem.Effects.Where(x => x.Id != 0));
                baseItem.Effects = keptEsperEffects.ToArray();
                baseItem.EsperChip = chipItem.ItemNumber.Id;
                baseItem.NeedsToSave = true;
                owner.Inventory.RemoveOrDecreaseCount(chipItem, 1);
                session.SendAsync(new UseEnchantChipAckMessage());
                session.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update,
                    baseItem.Map<PlayerItem, ItemDto>()));
                return;
            }
            var extractedNumber = GetExtractedChipNumber(baseItem.ItemNumber);
            if (extractedNumber == 0)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.DBError));
                return;
            }
            var carriedEffects = baseItem.Effects.Where(eff => eff >= 3100800001).ToArray();
            if (carriedEffects.Length == 0)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.DBError));
                return;
            }
            int carriedLevel = baseItem.EnchantLvl;
            List<EffectNumber> keptEffects = new List<EffectNumber>();
            keptEffects.AddRange(baseItem.Effects.Where(eff => eff < 3100800001).ToArray());
            baseItem.Effects = keptEffects.ToArray();
            owner.Inventory.Update(baseItem.Id);
            session.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update, baseItem.Map<PlayerItem, ItemDto>()));
            owner.Inventory.RemoveOrDecrease(chipItem);
            owner.Inventory.Create(extractedNumber, 1, 0, carriedEffects, 1, carriedLevel, true);
        }
        private static uint GetExtractedChipNumber(ItemNumber item)
        {
            if (item.Category == ItemCategory.Weapon)
            {
                switch ((WeaponCategory)item.SubCategory)
                {
                    case WeaponCategory.Melee: return 4190001;
                    case WeaponCategory.RifleGun: return 4200001;
                    case WeaponCategory.HeavyGun: return 4220001;
                    case WeaponCategory.Sniper: return 4230001;
                    case WeaponCategory.Sentry: return 4240001;
                    case WeaponCategory.Bomb: return 4250001;
                    case WeaponCategory.Mind: return 4350001;
                    default: return 0;
                }
            }
            if (item.Category == ItemCategory.Costume)
            {
                switch ((CostumeSlot)item.SubCategory)
                {
                    case CostumeSlot.Hair: return 4260001;
                    case CostumeSlot.Face: return 4270001;
                    case CostumeSlot.Shirt: return 4280001;
                    case CostumeSlot.Pants: return 4290001;
                    case CostumeSlot.Gloves: return 4300001;
                    case CostumeSlot.Shoes: return 4310001;
                    case CostumeSlot.Accessory: return 4320001;
                    case CostumeSlot.Pet: return 4330001;
                    default: return 0;
                }
            }
            return 0;
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
            if (GetExtractedChipNumber(baseItem.ItemNumber) != chipItem.ItemNumber.Id)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.DBError));
                return;
            }
            var gainedEffects = chipItem.Effects.Where(eff => eff >= 3100800001).ToArray();
            if (gainedEffects.Length == 0)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.DBError));
                return;
            }
            var removedEffects = baseItem.Effects.Where(eff => eff >= 3100800001).ToArray();
            var mergedEffects = baseItem.Effects.Where(eff => eff < 3100800001).ToList();
            mergedEffects.AddRange(gainedEffects);
            baseItem.Effects = mergedEffects.ToArray();
            baseItem.EnchantLvl = chipItem.EnchantLvl;
            baseItem.NeedsToSave = true;
            owner.Inventory.Remove(chipItem);
            session.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update, baseItem.Map<PlayerItem, ItemDto>()));
            session.SendAsync(new MoveEnchantChipAckMessage
            {
                Unk1 = 0,
                Removed = removedEffects.Select(x => (int)x.Id).ToArray(),
                Gained = gainedEffects.Select(x => (int)x.Id).ToArray(),
                Result = 1
            });
        }
        [MessageHandler(typeof(ItemEnchanReqMessage))]
        public void ItemEnchanReq(GameSession session, ItemEnchanReqMessage message)
        {
            var owner = session.Player;
            if (owner == null)
                return;
            var baseItem = owner.Inventory[(ulong)message.ItemId];
            var boosterItemId = message.Unk2;
            if (baseItem == null)
            {
                session.SendAsync(new EnchantEnchantItemAckMessage(EnchantResult.ErrorItemEnchant));
                return;
            }
            var random = new SecureRandom();
            var enchantTable = GameServer.Instance.ResourceCache.GetItemEnchant();
            var matchingEnchants = enchantTable.Where(x => x.Value.Category == baseItem.ItemNumber.Category && x.Value.SubCategory == baseItem.ItemNumber.SubCategory).ToList();
            if (matchingEnchants.Count == 0)
            {
                session.SendAsync(new EnchantEnchantItemAckMessage(EnchantResult.ErrorItemEnchant));
                return;
            }
            var passedEnchants = matchingEnchants.Where(x => Enumerable.Range(x.Value.Chance, 101)
               .Count(i => random.Next(x.Value.Chance, 101) <= x.Value.Chance) >= 4).OrderByDescending(e => e.Value.Level).ToList();
            var chosenEffects = passedEnchants.Count() != 0 ? passedEnchants[random.Next(0, passedEnchants.Count())].Value.Effects : matchingEnchants[0].Value.Effects;
            var priceTable = new uint[] { 200, 200, 300, 300, 500, 500, 600, 600, 800, 800, 1100, 1100, 1300, 1500, 1800, 2300, 2900, 3500, 4200, 5200, 5200, 5200, 5200, 5200, 5200};
            var enchantPrice = priceTable[Math.Min(baseItem.EnchantLvl, priceTable.Length - 1)];
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
                var booster = owner.Inventory[(ulong)boosterItemId];
                if (booster != null)
                {
                    var boosterUsed = true;
                    if (booster.ItemNumber == 4080001)
                        successBase += (successBase * 10) / 100;
                    else if (booster.ItemNumber == 4080002)
                        successBase += (successBase * 20) / 100;
                    else if (booster.ItemNumber == 4080003)
                        successBase = 100;
                    else if (booster.ItemNumber == 4070001)
                        jackpotBase = 20;
                    else
                        boosterUsed = false;
                    if (boosterUsed)
                        owner.Inventory.RemoveOrDecrease(booster);
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
        [MessageHandler(typeof(ItemUseEsperChipReqMessage))]
        public void ItemUseEsperChip(GameSession session, ItemUseEsperChipReqMessage message)
        {
            var owner = session.Player;
            if (owner == null)
                return;
            var first = owner.Inventory[message.Unk1];
            var second = owner.Inventory[message.Unk2];
            Logger.Information("[ESPERCHIP] Unk1={U1} ({I1}) Unk2={U2} ({I2})",
                message.Unk1, first?.ItemNumber.Id ?? 0, message.Unk2, second?.ItemNumber.Id ?? 0);
            var chip = IsEsperChip(first) ? first : IsEsperChip(second) ? second : null;
            var target = chip == first ? second : first;
            if (chip == null || target == null)
            {
                session.SendAsync(new ItemUseEsperChipItemAckMessage { Unk1 = 1 });
                return;
            }
            if (target.EsperChip != 0)
            {
                session.SendAsync(new ItemUseEsperChipItemAckMessage { Unk1 = 2 });
                return;
            }
            var effects = target.Effects.Where(x => x.Id != 0).ToList();
            foreach (var previous in GetEsperChipEffects(target.EsperChip))
                effects.Remove(previous);
            foreach (var gained in GetEsperChipEffects(chip.ItemNumber.Id))
                effects.Add(gained);
            target.Effects = effects.ToArray();
            target.EsperChip = chip.ItemNumber.Id;
            target.NeedsToSave = true;
            owner.Inventory.RemoveOrDecreaseCount(chip, 1);
            session.SendAsync(new ItemUseEsperChipItemAckMessage
            {
                Unk1 = 3,
                Unk2 = (long)target.Id,
                Unk3 = (int)chip.ItemNumber.Id
            });
            session.SendAsync(new ItemInventroyDeleteAckMessage(target.Id));
            session.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Add,
                target.Map<PlayerItem, ItemDto>()));
        }
        private static void RestoreEquipSlot(GameSession session, PlayerItem item)
        {
            foreach (var character in session.Player.CharacterManager)
            {
                var worn = character.Costumes.GetItems();
                for (var slot = 0; slot < worn.Count; slot++)
                {
                    if (worn[slot]?.Id != item.Id)
                        continue;
                    session.SendAsync(new ItemUseItemAckMessage
                    {
                        CharacterSlot = character.Slot,
                        ItemId = item.Id,
                        Action = UseItemAction.Equip,
                        EquipSlot = (byte)slot
                    });
                }
            }
        }
        private static EffectNumber[] GetEsperChipEffects(uint chipNumber)
        {
            if (chipNumber == 0)
                return Array.Empty<EffectNumber>();
            var shop = GameServer.Instance.ResourceCache.GetShop();
            var info = shop.GetFirstItemInfo(chipNumber);
            if (info?.EffectGroup == null)
                return Array.Empty<EffectNumber>();
            return info.EffectGroup.Effects.Select(x => (EffectNumber)x.Effect).ToArray();
        }
        private static bool MatchesEsperChipSlot(PlayerItem chip, PlayerItem target)
        {
            var slot = chip.ItemNumber.Id / 1000;
            if (target.ItemNumber.Category != ItemCategory.Costume)
                return false;
            var costume = (CostumeSlot)target.ItemNumber.SubCategory;
            if (slot == 7011)
                return costume == CostumeSlot.Face;
            if (slot == 7012)
                return costume == CostumeSlot.Shirt;
            if (slot == 7013)
                return costume == CostumeSlot.Pants;
            if (slot == 7014)
                return costume == CostumeSlot.Gloves;
            if (slot == 7015)
                return costume == CostumeSlot.Shoes;
            if (slot == 7016)
                return costume == CostumeSlot.Accessory;
            if (slot == 7017)
                return costume == CostumeSlot.Pet;
            return false;
        }
        private static bool IsEsperChip(PlayerItem item)
        {
            return item != null && item.ItemNumber.Id >= 7000000 && item.ItemNumber.Id <= 7099999;
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
            if (esperItem == null)
            {
                session.SendAsync(new EnchantEnchantItemAckMessage { Result = EnchantResult.ErrorItemEnchant });
                return;
            }
            var chipId = esperItem.ItemNumber.Id;
            var level = (int)(chipId % 10);
            if (level < 1 || level >= 5)
            {
                session.SendAsync(new EnchantEnchantItemAckMessage { Result = EnchantResult.ErrorItemEnchant });
                return;
            }
            var prices = new[] { 300, 500, 800, 3200 };
            var successProb = new[] { 80, 60, 30, 5 };
            var failKeep = new[] { 100, 70, 40, 45 };
            var failDown = new[] { 0, 25, 40, 50 };
            var price = prices[level - 1];
            if (owner.PEN < price)
            {
                session.SendAsync(new EnchantEnchantItemAckMessage { Result = EnchantResult.NotEnoughMoney });
                return;
            }
            if (message.PlusChips != 0)
                Logger.Information("[ESPERCHIP] PercentUp con PlusChips={Plus} sobre chip {Chip}", message.PlusChips, chipId);
            owner.PEN -= (uint)price;
            var random = new SecureRandom();
            if (random.Next(1, 101) <= successProb[level - 1])
            {
                Logger.Information("[ESPERCHIP] upgrade {From} -> {To}", chipId, chipId + 1u);
                owner.Inventory.CreateUnits(chipId + 1u, 1);
                owner.Inventory.RemoveOrDecreaseCount(esperItem, 1);
                var upgraded = owner.Inventory.FirstOrDefault(x => x.ItemNumber == chipId + 1u);
                var upgradedEffect = upgraded?.Effects?.FirstOrDefault(x => x.Id != 0).Id ?? 0u;
                session.SendAsync(new ItemInventoryInfoAckMessage
                {
                    Items = owner.Inventory.Select(i => i.Map<PlayerItem, ItemDto>()).ToArray()
                });
                session.SendAsync(new EsperEnchantAckMessage
                {
                    Result = 0,
                    ItemId = upgraded?.Id ?? message.EsperItemId,
                    Effect = upgradedEffect
                });
            }
            else
            {
                var failRoll = random.Next(1, 101);
                if (failRoll > failKeep[level - 1] + failDown[level - 1])
                {
                    owner.Inventory.RemoveOrDecreaseCount(esperItem, 1);
                    session.SendAsync(new EsperEnchantAckMessage
                    {
                        Result = 3,
                        ItemId = message.EsperItemId,
                        Effect = 0
                    });
                }
                else if (failRoll > failKeep[level - 1] && level > 1)
                {
                    owner.Inventory.CreateUnits(chipId - 1u, 1);
                    owner.Inventory.RemoveOrDecreaseCount(esperItem, 1);
                    var downgraded = owner.Inventory.FirstOrDefault(x => x.ItemNumber == chipId - 1u);
                        session.SendAsync(new ItemInventoryInfoAckMessage
                    {
                        Items = owner.Inventory.Select(i => i.Map<PlayerItem, ItemDto>()).ToArray()
                    });
                    session.SendAsync(new EsperEnchantAckMessage
                    {
                        Result = 2,
                        ItemId = downgraded?.Id ?? message.EsperItemId,
                        Effect = chipId - 1u
                    });
                }
                else
                {
                    session.SendAsync(new EsperEnchantAckMessage
                    {
                        Result = 1,
                        ItemId = message.EsperItemId,
                        Effect = chipId
                    });
                }
            }
            session.SendAsync(new MoneyRefreshCashInfoAckMessage { PEN = owner.PEN, AP = owner.AP });
            session.SendAsync(new ItemInventoryInfoAckMessage
            {
                Items = owner.Inventory.Select(i => i.Map<PlayerItem, ItemDto>()).ToArray()
            });
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
