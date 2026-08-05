using Santana.Database.Game;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Santana.Database.Game
{
    [Table("players")]
    public class PlayerDto
    {
        [Key] public int Id { get; set; }
        public string PlayTime { get; set; }
        public byte TutorialState { get; set; }
        public byte Level { get; set; }
        public uint TotalExperience { get; set; }
        public int PEN { get; set; }
        public int AP { get; set; }
        public int Coins1 { get; set; }
        public int Coins2 { get; set; }
        public byte CurrentCharacterSlot { get; set; }
        public int TotalMatches { get; set; }
        public int TotalWins { get; set; }
        public int TotalLosses { get; set; }
        public uint TagId { get; set; }
        public IList<PlayerCharacterDto> Characters { get; set; } = new List<PlayerCharacterDto>();
        public IList<PlayerDenyDto> Ignores { get; set; } = new List<PlayerDenyDto>();
        public IList<PlayerItemDto> Items { get; set; } = new List<PlayerItemDto>();
        public IList<PlayerMailDto> Inbox { get; set; } = new List<PlayerMailDto>();
        public IList<PlayerSettingDto> Settings { get; set; } = new List<PlayerSettingDto>();
        public IList<PlayerFriendDto> Friends { get; set; } = new List<PlayerFriendDto>();
        public IList<PlayerDeathMatchDto> DeathMatchInfo { get; set; } = new List<PlayerDeathMatchDto>();
        public IList<PlayerTouchDownDto> TouchDownInfo { get; set; } = new List<PlayerTouchDownDto>();
        public IList<PlayerChaserDto> ChaserInfo { get; set; } = new List<PlayerChaserDto>();
        public IList<PlayerBattleRoyalDto> BattleRoyalInfo { get; set; } = new List<PlayerBattleRoyalDto>();
        public IList<PlayerCaptainDto> CaptainInfo { get; set; } = new List<PlayerCaptainDto>();
        public IList<PlayerSiegeDto> SiegeInfo { get; set; } = new List<PlayerSiegeDto>();
        public IList<PlayerArenaDto> ArenaInfo { get; set; } = new List<PlayerArenaDto>();
        public IList<PlayerArcadeDto> ArcadeInfo { get; set; } = new List<PlayerArcadeDto>();
    }
    [Table("player_friends")]
    public class PlayerFriendDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(Player))] public int PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public int FriendId { get; set; }
        public int PlayerState { get; set; }
        public int FriendState { get; set; }
    }
    [Table("player_info_deathmatch")]
    public class PlayerDeathMatchDto
    {
        [Key] [ForeignKey(nameof(Player))] public int PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public ulong Won { get; set; }
        public ulong Loss { get; set; }
        public ulong Kills { get; set; }
        public ulong KillAssists { get; set; }
        public ulong Deaths { get; set; }
        public ulong Heal { get; set; }
    }
    [Table("player_info_touchdown")]
    public class PlayerTouchDownDto
    {
        [Key] [ForeignKey(nameof(Player))] public int PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public ulong Won { get; set; }
        public ulong Loss { get; set; }
        public ulong TD { get; set; }
        public ulong TDAssist { get; set; }
        public ulong Offense { get; set; }
        public ulong OffenseAssist { get; set; }
        public ulong Defense { get; set; }
        public ulong DefenseAssist { get; set; }
        public ulong Kill { get; set; }
        public ulong KillAssist { get; set; }
        public ulong OffenseRebound { get; set; }
        public ulong Heal { get; set; }
    }
    [Table("player_info_chaser")]
    public class PlayerChaserDto
    {
        [Key] [ForeignKey(nameof(Player))] public int PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public ulong ChasedWon { get; set; }
        public ulong ChasedRounds { get; set; }
        public ulong ChaserWon { get; set; }
        public ulong ChaserRounds { get; set; }
        public ulong Kills { get; set; }
    }
    [Table("player_info_battleroyal")]
    public class PlayerBattleRoyalDto
    {
        [Key] [ForeignKey(nameof(Player))] public int PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public ulong Won { get; set; }
        public ulong Loss { get; set; }
        public ulong Kills { get; set; }
        public ulong KillAssists { get; set; }
        public ulong FirstKilled { get; set; }
        public ulong FirstPlace { get; set; }
    }
    [Table("player_info_captain")]
    public class PlayerCaptainDto
    {
        [Key] [ForeignKey(nameof(Player))] public int PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public ulong Won { get; set; }
        public ulong Loss { get; set; }
        public ulong CPTKilled { get; set; }
        public ulong CPTCount { get; set; }
    }
    [Table("player_info_siege")]
    public class PlayerSiegeDto
    {
        [Key]
        [ForeignKey(nameof(Player))]
        public int PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public ulong Won { get; set; }
        public ulong Loss { get; set; }
        public ulong CaptureScore { get; set; }
        public ulong BattleScore { get; set; }
        public ulong MainCoreCaptureScore { get; set; }
        public ulong ItemObtainScore { get; set; }
    }
    [Table("player_info_arena")]
    public class PlayerArenaDto
    {
        [Key]
        [ForeignKey(nameof(Player))]
        public int PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public ulong Won { get; set; }
        public ulong Loss { get; set; }
        public ulong Kills { get; set; }
        public ulong Deaths { get; set; }
        public ulong DoubleKills { get; set; }
        public ulong TripleKills { get; set; }
        public ulong ShortestKillTime { get; set; }
        public ulong LeaderSelected { get; set; }
        public ulong LeaderKills { get; set; }
        public ulong TotalScore { get; set; }
    }
    [Table("player_info_arcade")]
    public class PlayerArcadeDto
    {
        [Key]
        [ForeignKey(nameof(Player))]
        public int PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        [Key]
        public byte ClearedStages { get; set; }
        [Key]
        public byte Difficulty { get; set; }
    }
    [Table("player_characters")]
    public class PlayerCharacterDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(Player))] public int PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public byte Slot { get; set; }
        public byte Gender { get; set; }
        public int? Weapon1Id { get; set; }
        public int? Weapon2Id { get; set; }
        public int? Weapon3Id { get; set; }
        public int? SkillId { get; set; }
        public int? HairId { get; set; }
        public int? FaceId { get; set; }
        public int? ShirtId { get; set; }
        public int? PantsId { get; set; }
        public int? GlovesId { get; set; }
        public int? ShoesId { get; set; }
        public int? AccessoryId { get; set; }
        public int? PetId { get; set; }
    }
    [Table("player_deny")]
    public class PlayerDenyDto
    {
        [Key] public int Id { get; set; }
        [ForeignKey(nameof(Player))] public int PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public int DenyPlayerId { get; set; }
    }
    [Table("shoppingbasket_items")]
    public class ShoppingBasketItemDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(Player))]
        public int PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public int ItemId { get; set; }
        public uint Effects { get; set; }
        public byte Color { get; set; }
        public ushort Period { get; set; }
        public int PeriodType { get; set; }
        public int PriceType { get; set; }
    }
    [Table("player_items")]
    public class PlayerItemDto
    {
        [Key] public int Id { get; set; }
        [ForeignKey(nameof(Player))] public int PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public int ShopItemInfoId { get; set; }
        public int ShopPriceId { get; set; }
        public int Period { get; set; }
        public int DaysLeft { get; set; }
        public string Effects { get; set; }
        public byte Color { get; set; }
        public long PurchaseDate { get; set; }
        public int Durability { get; set; }
        public int Count { get; set; }
        public uint EnchantMP { get; set; }
        public int EnchantLvl { get; set; }
        public uint EsperChip { get; set; }
        public PlayerItemDto() { }
    }
    [Table("player_mails")]
    public class PlayerMailDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(Player))] public int PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public int SenderPlayerId { get; set; }
        public long SentDate { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public bool IsMailNew { get; set; }
        public bool IsMailDeleted { get; set; }
        public bool IsClubMail { get; set; }
    }
    [Table("player_settings")]
    public class PlayerSettingDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(Player))] public int PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public string Setting { get; set; }
        public string Value { get; set; }
    }
    [Table("shop_effect_groups")]
    public class ShopEffectGroupDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Name { get; set; }
        public uint Effect { get; set; }
        public IList<ShopEffectDto> ShopEffects { get; set; } = new List<ShopEffectDto>();
    }
    [Table("shop_effects")]
    public class ShopEffectDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(EffectGroup))] public int EffectGroupId { get; set; }
        public ShopEffectGroupDto EffectGroup { get; set; }
        public uint Effect { get; set; }
    }
    [Table("shop_price_groups")]
    public class ShopPriceGroupDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Name { get; set; }
        public byte PriceType { get; set; }
        public IList<ShopPriceDto> ShopPrices { get; set; } = new List<ShopPriceDto>();
    }
    [Table("shop_prices")]
    public class ShopPriceDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(PriceGroup))] public int PriceGroupId { get; set; }
        public ShopPriceGroupDto PriceGroup { get; set; }
        public byte PeriodType { get; set; }
        public int Period { get; set; }
        public int Price { get; set; }
        public bool IsRefundable { get; set; }
        public int Durability { get; set; }
        public bool IsEnabled { get; set; }
    }
    [Table("shop_items")]
    public class ShopItemDto
    {
        [Key] public uint Id { get; set; }
        public byte RequiredGender { get; set; }
        public byte RequiredLicense { get; set; }
        public byte Colors { get; set; }
        public byte UniqueColors { get; set; }
        public byte RequiredLevel { get; set; }
        public byte LevelLimit { get; set; }
        public byte RequiredMasterLevel { get; set; }
        public bool IsOneTimeUse { get; set; }
        public bool IsDestroyable { get; set; }
        public byte MainTab { get; set; }
        public byte SubTab { get; set; }
        public IList<ShopItemInfoDto> ItemInfos { get; set; } = new List<ShopItemInfoDto>();
        [NotMapped]
        public int ItemNumber { get; set; }
    }
    [Table("shop_iteminfos")]
    public class ShopItemInfoDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(ShopItem))] public uint ShopItemId { get; set; }
        public ShopItemDto ShopItem { get; set; }
        public int PriceGroupId { get; set; }
        public int EffectGroupId { get; set; }
        public byte DiscountPercentage { get; set; }
        public byte Type { get; set; }
    }
    [Table("shop_version")]
    public class ShopVersionDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public byte Id { get; set; }
        public string Version { get; set; }
    }
    [Table("randomshop_version")]
    public class RandomShopVersionDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public byte Id { get; set; }
        public string Version { get; set; }
    }
    [Table("start_items")]
    public class StartItemDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int ShopItemInfoId { get; set; }
        public int ShopPriceId { get; set; }
        public int ShopEffectId { get; set; }
        public byte Color { get; set; }
        public int Count { get; set; }
        public byte RequiredSecurityLevel { get; set; }
    }
    [Table("player_purchase_history")]
    public class PlayerPurchaseHistoryDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(Player))] public int PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public uint ItemId { get; set; }
        public byte Color { get; set; }
        public long PurchaseDate { get; set; }
    }
    [Table("channels")]
    public class ChannelDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int PlayerLimit { get; set; }
        public byte MinLevel { get; set; }
        public byte MaxLevel { get; set; }
        public uint Color { get; set; }
    }
    [Table("clubs")]
    public class ClubDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public uint Id { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public int Level { get; set; }
        public uint Exp { get; set; }
        public uint Rank { get; set; }
        public uint Points { get; set; }
        public uint Win { get; set; }
        public uint Loss { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public int Area { get; set; }
        public int Activity { get; set; }
        public long CreatedAt { get; set; }
    }
    [Table("club_board")]
    public class ClubBoardDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public uint Id { get; set; }
        public uint ClubId { get; set; }
        public int AuthorId { get; set; }
        public string AuthorName { get; set; }
        public string Message { get; set; }
        public byte IsPublic { get; set; }
        public long CreatedAt { get; set; }
        public int AuthorLevel { get; set; }
        public uint ParentId { get; set; }
    }
    [Table("club_players")]
    public class ClubPlayerDto
    {
        [Key]
        [ForeignKey("Player")]
        public int PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        [ForeignKey("Club")]
        public uint ClubId { get; set; }
        public ClubDto Club { get; set; }
        public int State { get; set; }
        public int Rank { get; set; }
        public uint Points { get; set; }
        public uint Win { get; set; }
        public uint Loss { get; set; }
        public string LastLogin { get; set; }
    }
    [Table("clan_union")]
    public class ClanUnionDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey("Club")]
        public uint ClubId { get; set; }
        public ClubDto Club { get; set; }
        public uint UnionId { get; set; }
        public uint Stats { get; set; }
    }
    [Table("clan_history")]
    public class ClanHistoryDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey("Club")]
        public uint ClubId { get; set; }
        public ClubDto Club { get; set; }
        public uint EnemyClanId { get; set; }
        public int GameMode { get; set; }
        public int MapId { get; set; }
        public string ClanPlayers { get; set; }
        public string EnemyClanPlayers { get; set; }
        public string Date { get; set; }
        public string Status { get; set; }
    }
    [Table("club_request")]
    public class ClanRequestDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey("Club")]
        public uint ClubId { get; set; }
        public ClubDto Club { get; set; }
        [ForeignKey("Player")]
        public ulong PlayerId { get; set; }
        public PlayerDto Player { get; set; }
    }
    [Table("club_ban")]
    public class ClanBannedDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey("Club")]
        public uint ClubId { get; set; }
        public ClubDto Club { get; set; }
        [ForeignKey("Player")]
        public ulong PlayerId { get; set; }
        public PlayerDto Player { get; set; }
    }
    [Table("clan_new_name_history")]
    public class ClanNameChangeHistoryDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey("Club")]
        public uint ClubId { get; set; }
        public ClubDto Club { get; set; }
        public string OldName { get; set; }
        public string NewName { get; set; }
        public string Month { get; set; }
    }
    [Table("daily_reward")]
    public class DailyRewardDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public uint Reward { get; set; }
        public short IsItem { get; set; }
        public int Color { get; set; }
        public int Days { get; set; }
        public uint Untis { get; set; }
        public string Date { get; set; }
    }
    [Table("player_daily_reward")]
    public class PlayerDailyRewardDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(Player))]
        public ulong PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public string Date { get; set; }
    }
    [Table("events")]
    public class EventsDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Days { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public int RoundTime { get; set; }
        public int SubRoundTime { get; set; }
        public uint Reward { get; set; }
        public string RewardType { get; set; }
    }
    [Table("level_rewards")]
    public class LevelRewardDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Level { get; set; }
        public int Reward { get; set; }
        public uint Units { get; set; }
        public uint AP { get; set; }
        public uint PEN { get; set; }
    }
    [Table("event_level")]
    public class EventLevelDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Level { get; set; }
        public int MaxAccounts { get; set; }
        public string IsActive { get; set; }
    }
    [Table("accounts_event_levelup")]
    public class AccuntsEventLevelUpDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(Player))]
        public ulong PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public int Level { get; set; }
    }
    [Table("esper_skill")]
    public class EsperSkillDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(Player))]
        public ulong PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public int SkillId { get; set; }
        public int CharId { get; set; }
    }
    [Table("mission")]
    public class MissionDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(Player))]
        public ulong PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public string Mode { get; set; }
        public string Maps { get; set; }
        public string MapMode { get; set; }
        public string MissionDate { get; set; }
        public string Rewards { get; set; }
    }
    [Table("player_mission")]
    public class PlayerMissionDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(Player))]
        public ulong PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public string Mission { get; set; }
        public int MapId { get; set; }
        public string MissionDate { get; set; }
    }
    [Table("player_rank")]
    public class PlayerRankDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(Player))]
        public ulong PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public int Score { get; set; }
        public int Point { get; set; }
    }
    [Table("player_challenge")]
    public class PlayerChallengeDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(Player))]
        public ulong PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public ulong Player2Id { get; set; }
        public uint PEN { get; set; }
        public uint LP { get; set; }
        public string Winner { get; set; }
        public string Status { get; set; }
    }
    [Table("player_challenges")]
    public class PlayerChallengesDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(MainPlayer))]
        public ulong MainPlayerId { get; set; }
        public PlayerDto MainPlayer { get; set; }
        public ulong ChallengedPlayerId { get; set; }
        public uint PEN { get; set; }
        public uint AP { get; set; }
        public uint CreatedTime { get; set; }
        public uint Status { get; set; }
        public uint Winner { get; set; }
    }
    [Table("clanwar")]
    public class ClanWarDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Days { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public uint Reward { get; set; }
        public string RewardType { get; set; }
    }
    [Table("clanwarevent")]
    public class ClanWarEventDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string ClanAName { get; set; }
        public string ClanBName { get; set; }
        public string Status { get; set; }
        public string Winner { get; set; }
    }
    [Table("clanwars")]
    public class ClanWarsDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Clan { get; set; }
    }
    [Table("daily_event_reward")]
    public class DailyEventRewardDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public uint Reward { get; set; }
        public int Type { get; set; }
        public int Color { get; set; }
        public int Days { get; set; }
    }
    [Table("player_boosters")]
    public class PlayerBoostersDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(Player))] public ulong PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public int? MP { get; set; }
        public int? PEN { get; set; }
        public int? EXP { get; set; }
        public int? UNIQUE { get; set; }
        public int? NameTag { get; set; }
        public int? NameTag2 { get; set; }
        public int? NameTag3 { get; set; }
    }
    [Table("randomshop_category")]
    public class RandomshopCategoryDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string Type { get; set; }
        public string PiceType { get; set; }
        public int Price { get; set; }
        public string Gender { get; set; }
    }
    [Table("randomshop_items")]
    public class RandomShopItemDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public int ShopItemId { get; set; }
        public int Color { get; set; }
        public uint ItemPeriodType { get; set; }
        public uint Period { get; set; }
    }
    [Table("daily_mission")]
    public class Daily_MissionDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(Player))] public ulong PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public int Map { get; set; }
        public int MaxProgress { get; set; }
        public int Progress { get; set; }
        public int Reward { get; set; }
        public int Reward2 { get; set; }
        public int Reward3 { get; set; }
        public bool IsRewarded { get; set; }
        public string Date { get; set; }
    }
    [Table("daily_attendance_rewards")]
    public class DailyAttendanceRewardDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int ItemIndex { get; set; }
        public int UserType { get; set; }
        public int Unknown2 { get; set; }
        public int Unknown3 { get; set; }
        public int DayOfWeek { get; set; }
        public uint ItemKey { get; set; }
        public uint ShopId { get; set; }
        public string PeriodType { get; set; }
        public int Period { get; set; }
        public int Color { get; set; }
        public uint EffectId { get; set; }
    }
    [Table("player_daily_attendance_state")]
    public class PlayerDailyAttendanceStateDto
    {
        [Key]
        public int PlayerId { get; set; }
        [Key]
        public int WeekKey { get; set; }
        public int ClaimedMask { get; set; }
        public byte TotalClaimed { get; set; }
        public string LastClaimDate { get; set; }
    }
    [Table("characters_profile")]
    public class CharactersProfileDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(Player))] public int PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public string Name { get; set; }
        public int? HairId { get; set; }
        public int? FaceId { get; set; }
        public int? ShirtId { get; set; }
        public int? PantsId { get; set; }
        public int? GlovesId { get; set; }
        public int? ShoesId { get; set; }
        public int? AccessoryId { get; set; }
        public int? PetId { get; set; }
        public int? Weapon1Id { get; set; }
        public int? Weapon2Id { get; set; }
        public int? Weapon3Id { get; set; }
        public int? SkillId { get; set; }
    }
    [Table("achievements")]
    public class AchievementsDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Info { get; set; }
        public int Task { get; set; }
        public int Type { get; set; }
        public int Level { get; set; }
        public int Percentage { get; set; }
        public uint EXP { get; set; }
        public uint Reward { get; set; }
        public int Color { get; set; }
        public int RType { get; set; }
    }
    [Table("achievements_player")]
    public class AchievementsPlayerDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(Player))] public int PlayerId { get; set; }
        public PlayerDto Player { get; set; }
        public int AchievementId { get; set; }
        public int Level { get; set; }
        public int Percentage { get; set; }
        public bool IsClaim { get; set; }
    }
}
[Table("achieve_mission")]
public class AchieveMissionDto
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    [ForeignKey(nameof(Player))] public ulong PlayerId { get; set; }
    public PlayerDto Player { get; set; }
    public int Progress { get; set; }
    public int Progress2 { get; set; }
    public int Progress3 { get; set; }
    public int Progress4 { get; set; }
    public int Progress5 { get; set; }
    public int Progress6 { get; set; }
    public int Progress7 { get; set; }
    public int Progress8 { get; set; }
    public int Progress9 { get; set; }
    public int Progress10 { get; set; }
}
[Table("achieve_mission_progress")]
public class AchieveMissionProgressDto
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public int MaxProgress { get; set; }
    public int MaxProgress2 { get; set; }
    public int MaxProgress3 { get; set; }
    public int MaxProgress4 { get; set; }
    public int MaxProgress5 { get; set; }
    public int MaxProgress6 { get; set; }
    public int MaxProgress7 { get; set; }
    public int MaxProgress8 { get; set; }
    public int MaxProgress9 { get; set; }
    public int MaxProgress10 { get; set; }
}
[Table("achieve_mission_rewarded")]
public class AchieveMissionRewardedDto
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    [ForeignKey(nameof(Player))] public ulong PlayerId { get; set; }
    public PlayerDto Player { get; set; }
    public int MissionId { get; set; }
}
[Table("achieve_mission_rewards")]
public class AchieveMissionRewardsDto
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public int Reward { get; set; }
    public byte Color { get; set; }
}
