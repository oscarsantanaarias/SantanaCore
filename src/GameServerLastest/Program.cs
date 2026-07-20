using System.Text;
using Santana.Commands;
using Santana.Resource;
using Serilog.Events;
using System.Xml.Linq;
namespace Santana
{
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Security.Permissions;
    using System.Threading.Tasks;
    using SantanaLib;
    using Dapper;
    using Dapper.FastCrud;
    using DotNetty.Transport.Channels;
    using Microsoft.Data.Sqlite;
    using MySqlConnector;
    using Santana.Database.Game;
    using Santana.Network;
    using Santana.Network.Message.Game;
    using Newtonsoft.Json;
    using ProudNetSrc;
    using Serilog;
    using Serilog.Core;
    using Serilog.Formatting.Json;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels.Sockets;
    using System.Runtime.InteropServices;
    using System;
    public static class CollectBookMapper
    {
        public static Dictionary<(int itemId, byte color), int> Map = new();
        public static void Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }
            var document = XDocument.Load(path);
            foreach (var bookNode in document.Descendants("collect_book"))
            {
                var bookId = int.Parse(bookNode.Attribute("key").Value);
                foreach (var entryNode in bookNode.Elements("collect"))
                {
                    var entryItemId = int.Parse(entryNode.Attribute("key").Value);
                    var entryColor = byte.Parse(entryNode.Attribute("color").Value);
                    Map[(entryItemId, entryColor)] = bookId;
                }
            }
        }
        public static int GetBook(int itemId, byte color)
        {
            return Map.TryGetValue((itemId, color), out var found) ? found : 0;
        }
    }
    internal class Program
    {
        private static readonly object _shutdownGate = new object();
        private static bool _alreadyShutDown;
        private static string ResolveCollectBookXml()
        {
            var searchRoots = new[] { Environment.CurrentDirectory, AppContext.BaseDirectory };
            foreach (var root in searchRoots)
            {
                var cursor = new DirectoryInfo(root);
                while (cursor != null)
                {
                    var candidate = Path.Combine(cursor.FullName, "_eu_collect_book.xml");
                    if (File.Exists(candidate))
                        return candidate;
                    cursor = cursor.Parent;
                }
            }
            return null;
        }
        class ContextEnricher : ILogEventEnricher
        {
            public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
            {
                const int width = 20;
                var label = "";
                var isBlank = true;
                var contextProp = logEvent.Properties.FirstOrDefault(x => x.Key == "SourceContext");
                if (contextProp.Value != null)
                {
                    label += contextProp.Value.ToString().Replace("\"", string.Empty);
                    if (label.Length > width - 2)
                        label = label.Substring(0, width - 2);
                    isBlank = false;
                }
                var padded = "";
                if (label.Length < width)
                {
                    var half = (width - label.Length) / 2;
                    for (var i = 0; i < half; i++)
                        padded += " ";
                }
                if (isBlank)
                    padded = new string(' ', width - 1);
                else
                    padded = padded + label + padded;
                if (padded.Length >= width)
                    padded = padded.Substring(1, padded.Length - 1);
                var property = propertyFactory.CreateProperty("SrcContext", padded);
                logEvent.AddPropertyIfAbsent(property);
            }
        }
        class AccUserEnricher : ILogEventEnricher
        {
            public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
            {
                const int width = 14;
                var label = "";
                var isBlank = true;
                var accuserProp = logEvent.Properties.FirstOrDefault(x => x.Key == "Accuser");
                if (accuserProp.Value != null)
                {
                    label += accuserProp.Value.ToString().Replace("\"", string.Empty);
                    if (label.Length > width - 2)
                        label = label.Substring(0, width - 2);
                    isBlank = false;
                }
                var padded = "";
                if (label.Length < width)
                {
                    var half = (width - label.Length) / 2;
                    for (var i = 0; i < half; i++)
                        padded += " ";
                }
                if (isBlank)
                    padded = new string(' ', width - 1);
                else
                    padded = padded + label + padded;
                if (padded.Length >= width)
                    padded = padded.Substring(1, padded.Length - 1);
                var property = propertyFactory.CreateProperty("AccUSpace", padded);
                logEvent.AddPropertyIfAbsent(property);
            }
        }
        private static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new IPEndPointConverter() }
            };
            var jsonLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameServer.json");
            var textLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameServer.log");
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(new JsonFormatter(), jsonLogPath)
                .WriteTo.File(textLogPath)
                .Enrich.With<ContextEnricher>()
                .Enrich.With<AccUserEnricher>()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] |{SrcContext}| {AccUSpace:u3} | {Message}{NewLine}{Exception}")
                .MinimumLevel.Verbose()
                .CreateLogger();
            var log = Log.ForContext(Constants.SourceContextPropertyName, "Bootstrap");
            log.Information("--------------------------------------------");
            log.Information("Santana world node coming up");
            log.Information("--------------------------------------------");
            log.Information("Opening data stores");
            AuthDatabase.Initialize();
            GameDatabase.Initialize();
            log.Information("Bringing up world service");
            log.Information("--------------------------------------------");
            log.Information("Priming id sequences");
            ItemIdGenerator.Initialize();
            DenyIdGenerator.Initialize();
            var acceptGroup = new MultithreadEventLoopGroup(Config.Instance.ListenerThreads);
            var sharedWorker = new SingleThreadEventLoop();
            ChatServer.Initialize(new Configuration
            {
                SocketListenerThreads = acceptGroup,
                SocketWorkerThreads = new MultithreadEventLoopGroup(Config.Instance.WorkerThreads / 3),
                WorkerThread = sharedWorker
            });
            GameServer.Initialize(new Configuration
            {
                SocketListenerThreads = acceptGroup,
                SocketWorkerThreads = new MultithreadEventLoopGroup(Config.Instance.WorkerThreads / 3),
                WorkerThread = sharedWorker
            });
            FillShop();
            Ipc.Ipc.Initialize(Config.Instance.Redis);
            Network.Services.IpcService.StartAsync().GetAwaiter().GetResult();
            log.Information("Bus reachable at {r}; edge node attaches on its own start", Config.Instance.Redis);
            ChatServer.Instance.Listen(Config.Instance.ChatListener);
            GameServer.Instance.Listen(Config.Instance.Listener);
            log.Information("World node up; accepting sessions");
            log.Information("--------------------------------------------");
            Console.CancelKeyPress += OnCancelKeyPress;
            while (true)
            {
                var line = Console.ReadLine();
                if (line == null)
                    break;
                if (line.Equals("exit", StringComparison.InvariantCultureIgnoreCase) ||
                    line.Equals("quit", StringComparison.InvariantCultureIgnoreCase) ||
                    line.Equals("stop", StringComparison.InvariantCultureIgnoreCase))
                    break;
                var tokens = line.GetArgs();
                if (tokens.Length == 0)
                    continue;
                Task.Run(async () =>
                {
                    if (!await GameServer.Instance.CommandManager.Execute(null, tokens))
                        CommandManager.Logger.Information("No such directive");
                });
            }
            Exit();
        }
        private static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Exit();
        }
        private static void Exit()
        {
            lock (_shutdownGate)
            {
                if (_alreadyShutDown)
                    return;
                Log.Information("Winding down");
                try
                {
                    foreach (var raw in GameServer.Instance.Sessions.Values)
                    {
                        var gameSession = (GameSession)raw;
                        gameSession.Player?.Room?.Leave(gameSession.Player);
                    }
                    GameServer.Instance.Broadcast(new ItemUseChangeNickAckMessage { Result = 0 });
                    GameServer.Instance.Broadcast(new ServerResultAckMessage(ServerResult.CreateNicknameSuccess));
                }
                catch (Exception)
                {
                }
                _alreadyShutDown = true;
                Environment.Exit(0);
            }
        }
        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            if (e.Exception.InnerException is ClosedChannelException)
                return;
            WriteCrashLog("orphaned task fault", e.Exception);
            Log.Error(e.Exception, "Background task faulted unnoticed");
        }
        private static void OnUnhandledException(object s, UnhandledExceptionEventArgs args)
        {
            var error = (Exception)args.ExceptionObject;
            WriteCrashLog(args.IsTerminating ? "escaped fault (TERMINAL - node is going down)" : "escaped fault", error);
            Log.Error(error.ToString(), "Fatal fault escaped the domain");
            try
            {
                foreach (var raw in GameServer.Instance.Sessions.Values)
                {
                    var gameSession = (GameSession)raw;
                    gameSession.Player?.Room?.Leave(gameSession.Player);
                }
                GameServer.Instance.Broadcast(new ItemUseChangeNickAckMessage { Result = 0 });
                GameServer.Instance.Broadcast(new ServerResultAckMessage(ServerResult.CreateNicknameSuccess));
            }
            catch (Exception)
            {
            }
            Environment.Exit(-1);
        }
        private static void WriteCrashLog(string source, Exception ex)
        {
            try
            {
                var crashPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServerCrashed.log");
                File.AppendAllText(crashPath,
                    $"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss} | GameServer | {source} ===={Environment.NewLine}" +
                    (ex?.ToString() ?? "(no exception object attached)") + Environment.NewLine + Environment.NewLine);
            }
            catch { }
        }
        private static void FillShop()
        {
            if (!Config.Instance.NoobMode)
                return;
            using (var db = GameDatabase.Open())
            {
                if (!DbUtil.Find<ShopVersionDto>(db).Any())
                {
                    var freshVersion = new ShopVersionDto
                    {
                        Version = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss")
                    };
                    DbUtil.Insert(db, freshVersion);
                }
                if (DbUtil.Find<ShopEffectGroupDto>(db).Any() || DbUtil.Find<ShopEffectDto>(db).Any() ||
                    DbUtil.Find<ShopPriceGroupDto>(db).Any() || DbUtil.Find<ShopPriceDto>(db).Any() ||
                    DbUtil.Find<ShopItemDto>(db).Any() || DbUtil.Find<ShopItemInfoDto>(db).Any())
                    return;
                Log.Information("Permissive mode: seeding catalogue entries");
                using (var transaction = DbUtil.BeginTransaction(db))
                {
                    var effectTable = new Dictionary<string, Tuple<uint[], uint>>
                    {
                        {"None", Tuple.Create(Array.Empty<uint>(), (uint) 0)},
                        {
                            "Shooting Weapon Defense (Head) +5%",
                            Tuple.Create(new uint[] {1100313003, 1100315003, 1100317003}, (uint) 1100315002)
                        },
                        {"SP+6", Tuple.Create(new uint[] {1101301006}, (uint) 1101301006)},
                        {"Attack+1%", Tuple.Create(new uint[] {1299600001}, (uint) 1299600001)},
                        {"Attack+3%", Tuple.Create(new uint[] {1299600002}, (uint) 1299600002)},
                        {"Attack+5%", Tuple.Create(new uint[] {1299600003}, (uint) 1299600003)},
                        {"Attack+8%", Tuple.Create(new uint[] {1299600005}, (uint) 1299600005)},
                        {"Attack+10%", Tuple.Create(new uint[] {1299600006}, (uint) 1299600006)},
                        {"Defense+5%", Tuple.Create(new uint[] {1103302004}, (uint) 1103302004)},
                        {"HP+4", Tuple.Create(new uint[] {1105300004}, (uint) 1105300004)},
                        {"HP+30", Tuple.Create(new uint[] {1999300011}, (uint) 1999300011)},
                        {"HP+15", Tuple.Create(new uint[] {1999300009}, (uint) 1999300009)},
                        {"SP+40", Tuple.Create(new uint[] {1300301012}, (uint) 1300301012)},
                        {"HP+20 & SP+20", Tuple.Create(new uint[] {1999300010, 1999301011}, (uint) 30001)},
                        {"HP+25 & SP+25", Tuple.Create(new uint[] {1999300012, 1999301013}, (uint) 30003)}
                    };
                    #region Effects
                    foreach (var entry in effectTable.ToArray())
                    {
                        var groupRow = new ShopEffectGroupDto { Name = entry.Key, Effect = entry.Value.Item2 };
                        DbUtil.Insert(db, groupRow, statement => statement.AttachToTransaction(transaction));
                        effectTable[entry.Key] = Tuple.Create(entry.Value.Item1, (uint)groupRow.Id);
                        foreach (var effectId in entry.Value.Item1)
                        {
                            DbUtil.Insert(db, new ShopEffectDto { EffectGroupId = groupRow.Id, Effect = effectId },
                                statement => statement.AttachToTransaction(transaction));
                        }
                    }
                    #endregion
                    #region Price
                    var penGroup = new ShopPriceGroupDto
                    {
                        Name = "PEN",
                        PriceType = (byte)ItemPriceType.PEN
                    };
                    var premiumGroup = new ShopPriceGroupDto
                    {
                        Name = "Item",
                        PriceType = (byte)ItemPriceType.Premium
                    };
                    var apGroup = new ShopPriceGroupDto
                    {
                        Name = "AP",
                        PriceType = (byte)ItemPriceType.AP
                    };
                    DbUtil.Insert(db, penGroup);
                    DbUtil.Insert(db, premiumGroup);
                    DbUtil.Insert(db, apGroup);
                    var penPermanentPrice = new ShopPriceDto
                    {
                        PriceGroupId = penGroup.Id,
                        PeriodType = (byte)ItemPeriodType.None,
                        IsRefundable = true,
                        Durability = 2400,
                        IsEnabled = true,
                        Price = 1
                    };
                    var premiumOneUnit = new ShopPriceDto
                    {
                        PriceGroupId = premiumGroup.Id,
                        Durability = 1,
                        PeriodType = (byte)ItemPeriodType.Units,
                        IsRefundable = true,
                        Period = 1,
                        IsEnabled = true,
                        Price = 1000
                    };
                    var premiumTwoUnits = new ShopPriceDto
                    {
                        PriceGroupId = premiumGroup.Id,
                        Durability = 1,
                        PeriodType = (byte)ItemPeriodType.Units,
                        IsRefundable = true,
                        Period = 2,
                        IsEnabled = true,
                        Price = 2000
                    };
                    var premiumFiveUnits = new ShopPriceDto
                    {
                        PriceGroupId = premiumGroup.Id,
                        Durability = 1,
                        PeriodType = (byte)ItemPeriodType.Units,
                        IsRefundable = true,
                        Period = 5,
                        IsEnabled = true,
                        Price = 4500
                    };
                    var premiumTenUnits = new ShopPriceDto
                    {
                        PriceGroupId = premiumGroup.Id,
                        Durability = 1,
                        PeriodType = (byte)ItemPeriodType.Units,
                        IsRefundable = true,
                        Period = 10,
                        IsEnabled = true,
                        Price = 8000
                    };
                    var apPermanentPrice = new ShopPriceDto
                    {
                        PriceGroupId = apGroup.Id,
                        PeriodType = (byte)ItemPeriodType.None,
                        IsRefundable = true,
                        Durability = 2400,
                        IsEnabled = true,
                        Price = 1
                    };
                    DbUtil.Insert(db, penPermanentPrice);
                    DbUtil.Insert(db, premiumOneUnit);
                    DbUtil.Insert(db, premiumTwoUnits);
                    DbUtil.Insert(db, premiumFiveUnits);
                    DbUtil.Insert(db, premiumTenUnits);
                    DbUtil.Insert(db, apPermanentPrice);
                    #endregion
                    #region Items
                    var catalog = GameServer.Instance.ResourceCache.GetItems().Values.ToArray();
                    for (var index = 0; index < catalog.Length; ++index)
                    {
                        var resourceItem = catalog[index];
                        var chosenEffect = effectTable["None"];
                        byte mainTab = 0;
                        byte subTab = 0;
                        bool enabled = true;
                        switch (resourceItem.ItemNumber.Category)
                        {
                            case ItemCategory.Card:
                            case ItemCategory.Coupon:
                                mainTab = 4;
                                subTab = 6;
                                break;
                            case ItemCategory.EsperChip:
                                mainTab = 4;
                                subTab = 2;
                                break;
                            case ItemCategory.Boost:
                                switch ((BoostCategory)resourceItem.ItemNumber.SubCategory)
                                {
                                    case BoostCategory.Pen:
                                        mainTab = 4;
                                        subTab = 4;
                                        break;
                                    case BoostCategory.Exp:
                                        mainTab = 4;
                                        subTab = 5;
                                        break;
                                    case BoostCategory.Mp:
                                        mainTab = 4;
                                        subTab = 3;
                                        break;
                                    case BoostCategory.Unique:
                                        mainTab = 4;
                                        subTab = 5;
                                        break;
                                }
                                break;
                            case ItemCategory.OneTimeUse:
                                switch ((OneTimeUseCategory)resourceItem.ItemNumber.SubCategory)
                                {
                                    case OneTimeUseCategory.Namechange:
                                        mainTab = 4;
                                        subTab = 0;
                                        break;
                                    case OneTimeUseCategory.Stat:
                                        mainTab = 4;
                                        subTab = 0;
                                        break;
                                    case OneTimeUseCategory.Capsule:
                                        mainTab = 1;
                                        subTab = 0;
                                        break;
                                    case OneTimeUseCategory.FumbiCapsule:
                                        mainTab = 1;
                                        subTab = 4;
                                        break;
                                    case OneTimeUseCategory.Event:
                                        mainTab = 4;
                                        subTab = 0;
                                        break;
                                    case OneTimeUseCategory.Set:
                                        mainTab = 1;
                                        subTab = 7;
                                        break;
                                    default:
                                        continue;
                                }
                                break;
                            case ItemCategory.Weapon:
                                chosenEffect = effectTable["Attack+1%"];
                                mainTab = 2;
                                switch ((WeaponCategory)resourceItem.ItemNumber.SubCategory)
                                {
                                    case WeaponCategory.Melee:
                                        subTab = 1;
                                        break;
                                    case WeaponCategory.RifleGun:
                                        subTab = 2;
                                        break;
                                    case WeaponCategory.HeavyGun:
                                        subTab = 4;
                                        break;
                                    case WeaponCategory.Sniper:
                                        subTab = 5;
                                        break;
                                    case WeaponCategory.Sentry:
                                        subTab = 6;
                                        break;
                                    case WeaponCategory.Bomb:
                                        subTab = 7;
                                        break;
                                    case WeaponCategory.Mind:
                                        subTab = 6;
                                        break;
                                }
                                break;
                            case ItemCategory.Skill:
                                mainTab = 2;
                                subTab = 8;
                                if (resourceItem.ItemNumber.SubCategory == 0 && resourceItem.ItemNumber.Number == 0)
                                    chosenEffect = effectTable["HP+15"];
                                if (resourceItem.ItemNumber.SubCategory == 0 && resourceItem.ItemNumber.Number == 1)
                                    chosenEffect = effectTable["HP+30"];
                                if (resourceItem.ItemNumber.SubCategory == 0 && resourceItem.ItemNumber.Number == 2)
                                    chosenEffect = effectTable["SP+40"];
                                if (resourceItem.ItemNumber.SubCategory == 0 && resourceItem.ItemNumber.Number == 3)
                                    chosenEffect = effectTable["HP+20 & SP+20"];
                                if (resourceItem.ItemNumber.SubCategory == 0 && resourceItem.ItemNumber.Number == 5
                                )
                                    chosenEffect = effectTable["HP+20 & SP+20"];
                                if (resourceItem.ItemNumber.SubCategory == 0 && resourceItem.ItemNumber.Number == 7
                                )
                                    chosenEffect = effectTable["HP+25 & SP+25"];
                                break;
                            case ItemCategory.Costume:
                                mainTab = 3;
                                subTab = (byte)(resourceItem.ItemNumber.SubCategory + 2);
                                switch ((CostumeSlot)resourceItem.ItemNumber.SubCategory)
                                {
                                    case CostumeSlot.Hair:
                                        chosenEffect = effectTable["Shooting Weapon Defense (Head) +5%"];
                                        break;
                                    case CostumeSlot.Face:
                                        chosenEffect = effectTable["SP+6"];
                                        break;
                                    case CostumeSlot.Shirt:
                                        chosenEffect = effectTable["Attack+5%"];
                                        break;
                                    case CostumeSlot.Pants:
                                        chosenEffect = effectTable["Defense+5%"];
                                        break;
                                    case CostumeSlot.Gloves:
                                        chosenEffect = effectTable["HP+4"];
                                        break;
                                    case CostumeSlot.Shoes:
                                        chosenEffect = effectTable["HP+4"];
                                        break;
                                    case CostumeSlot.Accessory:
                                        chosenEffect = effectTable["SP+6"];
                                        break;
                                    case CostumeSlot.Pet:
                                        chosenEffect = effectTable["SP+6"];
                                        break;
                                }
                                break;
                            default:
                                chosenEffect = effectTable["None"];
                                mainTab = 4;
                                subTab = 6;
                                break;
                        }
                        var shopItem = new ShopItemDto
                        {
                            Id = resourceItem.ItemNumber,
                            RequiredGender = (byte)resourceItem.Gender,
                            RequiredLicense = (byte)resourceItem.License,
                            IsDestroyable = true,
                            MainTab = mainTab,
                            SubTab = subTab,
                            Colors = (byte)resourceItem.Colors
                        };
                        DbUtil.Insert(db, shopItem, statement => statement.AttachToTransaction(transaction));
                        var shopItemInfo = new ShopItemInfoDto
                        {
                            ShopItemId = shopItem.Id,
                            PriceGroupId = penGroup.Id,
                            EffectGroupId = (int)chosenEffect.Item2,
                            Type = enabled ? (byte)1 : (byte)0,
                        };
                        var shopItemInfo_onetimeuse = new ShopItemInfoDto
                        {
                            ShopItemId = shopItem.Id,
                            PriceGroupId = premiumGroup.Id,
                            EffectGroupId = (int)chosenEffect.Item2,
                            Type = enabled ? (byte)1 : (byte)0,
                        };
                        if (resourceItem.ItemNumber.Category == ItemCategory.Costume || resourceItem.ItemNumber.Category ==
                                                                             ItemCategory.Weapon
                                                                             || resourceItem.ItemNumber.Category ==
                                                                             ItemCategory.Skill ||
                                                                             resourceItem.ItemNumber.Category ==
                                                                             ItemCategory.EsperChip)
                        {
                            DbUtil.Insert(db, shopItemInfo, statement => statement.AttachToTransaction(transaction));
                        }
                        else
                        {
                            DbUtil.Insert(db, shopItemInfo_onetimeuse,
                                statement => statement.AttachToTransaction(transaction));
                        }
                        Log.Information($"[{index}/{catalog.Length}] {resourceItem.ItemNumber}: {resourceItem.Name} | Colors: {resourceItem.Colors}");
                    }
                    #endregion
                    var storedInfos = DbUtil.Find<ShopItemInfoDto>(db, statement => statement
                        .Include<ShopItemDto>(join => join.LeftOuterJoin())
                        .AttachToTransaction(transaction));
                    var workingCapsules = GameServer.Instance.ResourceCache._loader.GetWorkingCapsules();
                    foreach (var oneTimeItem in catalog.Where(x => x.ItemNumber.Category == ItemCategory.OneTimeUse))
                    {
                        var matchingInfo = storedInfos.FirstOrDefault(x => x.ShopItemId == oneTimeItem.ItemNumber);
                        if (matchingInfo != null)
                        {
                            if (!workingCapsules.Contains(oneTimeItem.ItemNumber))
                            {
                                matchingInfo.Type = 0;
                                DbUtil.Update(db, matchingInfo, statement => statement.AttachToTransaction(transaction));
                            }
                        }
                    }
                    try
                    {
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
    internal static class AuthDatabase
    {
        private static readonly ILogger Logger =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(AuthDatabase));
        private static string _sConnectionString;
        public static void Initialize()
        {
            Logger.Information("Data store opening");
            var config = Config.Instance.Database;
            switch (config.Engine)
            {
                case DatabaseEngine.MySQL:
                    _sConnectionString =
                        $"SslMode=none;Server={config.Auth.Host};Port={config.Auth.Port};Database={config.Auth.Database};Uid={config.Auth.Username};Pwd={config.Auth.Password};Pooling=true;";
                    OrmConfiguration.DefaultDialect = SqlDialect.MySql;
                    using (var probe = Open())
                    {
                        if (probe.QueryFirstOrDefault($"SHOW DATABASES LIKE \"{config.Auth.Database}\"") == null)
                        {
                            Logger.Error($"No such schema: '{config.Auth.Database}'");
                            Environment.Exit(0);
                        }
                    }
                    break;
                case DatabaseEngine.SQLite:
                    _sConnectionString = $"Data Source={config.Auth.Filename};";
                    OrmConfiguration.DefaultDialect = SqlDialect.SqLite;
                    if (!File.Exists(config.Auth.Filename))
                    {
                        Logger.Error($"No such data file: '{config.Auth.Filename}'");
                        Environment.Exit(0);
                    }
                    break;
                default:
                    Logger.Error($"Unsupported storage driver {config.Engine}");
                    Environment.Exit(0);
                    return;
            }
        }
        public static IDbConnection Open()
        {
            var engine = Config.Instance.Database.Engine;
            IDbConnection connection = engine switch
            {
                DatabaseEngine.MySQL => new MySqlConnection(_sConnectionString),
                DatabaseEngine.SQLite => new SqliteConnection(_sConnectionString),
                _ => throw new Exception($"Invalid database engine {engine}")
            };
            connection.Open();
            return connection;
        }
    }
    internal static class GameDatabase
    {
        private static readonly ILogger Logger =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(GameDatabase));
        private static string _sConnectionString;
        public static void Initialize()
        {
            Logger.Information("Data store opening");
            var config = Config.Instance.Database;
            switch (config.Engine)
            {
                case DatabaseEngine.MySQL:
                    _sConnectionString =
                        $"SslMode=none;Server={config.Game.Host};Port={config.Game.Port};Database={config.Game.Database};Uid={config.Game.Username};Pwd={config.Game.Password};Pooling=true;";
                    OrmConfiguration.DefaultDialect = SqlDialect.MySql;
                    using (var probe = Open())
                    {
                        if (probe.QueryFirstOrDefault($"SHOW DATABASES LIKE \"{config.Game.Database}\"") == null)
                        {
                            Logger.Error($"No such schema: '{config.Game.Database}'");
                            Environment.Exit(0);
                        }
                    }
                    break;
                case DatabaseEngine.SQLite:
                    _sConnectionString = $"Data Source={config.Game.Filename};";
                    OrmConfiguration.DefaultDialect = SqlDialect.SqLite;
                    if (!File.Exists(config.Game.Filename))
                    {
                        Logger.Error($"No such data file: '{config.Game.Filename}'");
                        Environment.Exit(0);
                    }
                    break;
                default:
                    Logger.Error($"Unsupported storage driver {config.Engine}");
                    Environment.Exit(0);
                    return;
            }
        }
        public static IDbConnection Open()
        {
            var engine = Config.Instance.Database.Engine;
            IDbConnection connection;
            switch (engine)
            {
                case DatabaseEngine.MySQL:
                    connection = new MySqlConnection(_sConnectionString);
                    break;
                case DatabaseEngine.SQLite:
                    connection = new SqliteConnection(_sConnectionString);
                    break;
                default:
                    Log.Error($"Unsupported storage driver {engine}");
                    Environment.Exit(0);
                    return null;
            }
            connection.Open();
            return connection;
        }
    }
}
