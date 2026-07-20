using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Santana.Network;
using Santana.Network.Data.Chat;
using Santana.Network.Message.Game;
using Santana.Network.Services;
using ProudNetSrc;

namespace Santana.Commands
{
    internal class RecreateCollectBookCommand : ICommand
    {
        public RecreateCollectBookCommand()
        {
            Name = "/recrear";
            AllowConsole = true;
            Permission = SecurityLevel.GameMaster;
            SubCommands = Array.Empty<ICommand>();
        }

        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }

        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            try
            {
                var recipients = CollectRecipients(plr, args);
                if (recipients.Count == 0)
                {
                    Notify(plr, "recrear target not online");
                    return true;
                }

                var variant = args.Length >= 2 ? args[1].Trim().ToLowerInvariant() : "safe";
                variant = variant switch
                {
                    "1" => "legacy-body-dec0",
                    "2" => "legacy-body-lendec",
                    "3" => "legacy-body-0dec",
                    "4" => "legacy-body-0body",
                    "5" => "legacy-s4-full",
                    "quick1" => "legacy-body-dec0",
                    "quick2" => "legacy-body-lendec",
                    "quick3" => "legacy-body-0dec",
                    "quick4" => "legacy-body-0body",
                    "quick5" => "legacy-s4-full",
                    _ => variant
                };
                foreach (var recipient in recipients)
                {
                    if (variant == "safe" || variant == "safe-replay")
                    {
                        var inventoryAck = ShopService.CreateCollectBookInventoryInfoAck(recipient, true);
                        var stamp = ShopService.GetCollectBookVersion();

                        await recipient.SendAsync(new CollectBook_UpdateRequest_Ack());
                        await recipient.SendAsync(new CollectBook_UpdateCheck_Ack
                        {
                            Data = stamp
                        });

                        foreach (var book in inventoryAck.Items ?? Array.Empty<CollectBook_ItemRegist_Ack>())
                            await recipient.SendAsync(book);

                        await recipient.SendAsync(inventoryAck);
                        var slotSum = inventoryAck.Items?.Sum(x => x.Unk4 + x.Unk5 + x.Unk6 + x.Unk7 + x.Unk8 + x.Unk9) ?? 0;
                        Console.WriteLine($"[CollectBook RECREATE] safe replay sent | player={recipient.Account.Nickname}({recipient.Account.Id}) version={stamp} books={inventoryAck.Items?.Length ?? 0} filled={slotSum}");
                        continue;
                    }

                    if (TryBuild1108Preset(variant, out var armed))
                    {
                        await recipient.SendAsync(new CollectBook_UpdateRequest_Ack());
                        ShopService.ForceCollectBookVersion(
                            recipient.Account.Id,
                            armed.Version,
                            true,
                            armed.Payload,
                            armed.Label,
                            armed.Unk2,
                            armed.Unk3,
                            armed.Unk4);
                        await recipient.SendAsync(new CollectBook_UpdateCheck_Ack
                        {
                            Data = armed.Version
                        });

                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateRequest_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear {variant}");
                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateCheck_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear {variant} data='{armed.Version}'");
                        Console.WriteLine($"[CollectBook RECREATE] armed 1108 preset | player={recipient.Account.Nickname}({recipient.Account.Id}) mode={variant} label={armed.Label} version={armed.Version} bytes={armed.Payload.Length} unk2={armed.Unk2} unk3={armed.Unk3} unk4='{armed.Unk4}' head={BitConverter.ToString(armed.Payload.Take(24).ToArray())} path={armed.SourcePath}");
                        continue;
                    }

                    if (variant == "legacy" || variant == "legacy-date" || variant == "double" || variant == "double-rev" ||
                        variant == "legacy-xml00" || variant == "legacy-xml-len" ||
                        variant == "legacy-body-lens" || variant == "legacy-s4-full" ||
                        variant == "legacy-body-dec0" || variant == "legacy-body-lendec" ||
                        variant == "legacy-body-0dec" || variant == "legacy-body-0body")
                    {
                        var stamp = variant == "legacy-date" || variant == "double" || variant == "double-rev"
                            ? DateTime.UtcNow.ToString("yyyyMMddHHmmss")
                            : "20171116121051";

                        await recipient.SendAsync(new CollectBook_UpdateRequest_Ack());
                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateRequest_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear {variant}");

                        await recipient.SendAsync(new CollectBook_UpdateCheck_Ack
                        {
                            Data = stamp
                        });
                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateCheck_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear {variant} data='{stamp}'");

                        if (variant == "legacy-xml00" || variant == "legacy-xml-len" || variant == "legacy-body-lens" || variant == "legacy-s4-full" ||
                            variant == "legacy-body-dec0" || variant == "legacy-body-lendec" || variant == "legacy-body-0dec" || variant == "legacy-body-0body")
                        {
                            byte[] blob;
                            int lenA;
                            int lenB;

                            if (variant == "legacy-xml00" || variant == "legacy-xml-len")
                            {
                                var xmlFile = LocateDecodedCollectBookXml();
                                if (xmlFile == null)
                                {
                                    Notify(plr, $"recrear {variant} failed: decoded collect book xml not found");
                                    return true;
                                }

                                blob = PatchXmlVersion(File.ReadAllBytes(xmlFile), stamp);
                                lenA = variant == "legacy-xml-len" ? blob.Length : 0;
                                lenB = 0;

                                Console.WriteLine($"[CollectBook RECREATE] {variant} source xml={xmlFile} bytes={blob.Length} unk2={lenA} unk3={lenB}");
                            }
                            else
                            {
                                var s4File = LocateLargestCollectBookS4();
                                if (s4File == null)
                                {
                                    Notify(plr, $"recrear {variant} failed: collect book s4 not found");
                                    return true;
                                }

                                var fileData = File.ReadAllBytes(s4File);
                                if (fileData.Length <= 4)
                                {
                                    Notify(plr, $"recrear {variant} failed: collect book s4 too small");
                                    return true;
                                }

                                var rawLength = BitConverter.ToInt32(fileData, 0);
                                var tail = fileData.Skip(4).ToArray();

                                if (variant == "legacy-body-lens")
                                {
                                    blob = tail;
                                    lenA = rawLength;
                                    lenB = tail.Length;
                                }
                                else if (variant == "legacy-body-dec0")
                                {
                                    blob = tail;
                                    lenA = rawLength;
                                    lenB = 0;
                                }
                                else if (variant == "legacy-body-lendec")
                                {
                                    blob = tail;
                                    lenA = tail.Length;
                                    lenB = rawLength;
                                }
                                else if (variant == "legacy-body-0dec")
                                {
                                    blob = tail;
                                    lenA = 0;
                                    lenB = rawLength;
                                }
                                else if (variant == "legacy-body-0body")
                                {
                                    blob = tail;
                                    lenA = 0;
                                    lenB = tail.Length;
                                }
                                else
                                {
                                    blob = fileData;
                                    lenA = fileData.Length;
                                    lenB = 0;
                                }

                                Console.WriteLine($"[CollectBook RECREATE] {variant} source s4={s4File} payload={blob.Length} decomp={rawLength} unk2={lenA} unk3={lenB}");
                            }

                            ShopService.ForceCollectBookVersion(
                                recipient.Account.Id,
                                stamp,
                                true,
                                blob,
                                variant,
                                lenA,
                                lenB,
                                stamp);
                            Console.WriteLine($"[CollectBook RECREATE] armed next UpdateCheck | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear {variant} bytes={blob.Length} unk2={lenA} unk3={lenB} unk4='{stamp}' head={BitConverter.ToString(blob.Take(24).ToArray())}");
                        }
                        else if (variant == "double-rev")
                        {
                            await recipient.SendAsync(new CollectBook_UpdateInfo_Ack());
                            Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateInfo_Ack EMPTY PURE | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear {variant}");
                            await recipient.SendAsync(new CollectBook_UpdateInfo_Ack
                            {
                                Unk1 = Array.Empty<byte>(),
                                Unk2 = 0,
                                Unk3 = 0,
                                Unk4 = stamp
                            });
                            Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateInfo_Ack EMPTY DATE | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear {variant} version='{stamp}'");
                        }
                        else
                        {
                            await recipient.SendAsync(new CollectBook_UpdateInfo_Ack
                            {
                                Unk1 = Array.Empty<byte>(),
                                Unk2 = 0,
                                Unk3 = 0,
                                Unk4 = stamp
                            });
                            Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateInfo_Ack EMPTY DATE | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear {variant} version='{stamp}'");
                        }

                        if (variant == "double")
                        {
                            await recipient.SendAsync(new CollectBook_UpdateInfo_Ack());
                            Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateInfo_Ack EMPTY PURE | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear {variant}");
                        }

                        Console.WriteLine($"[CollectBook RECREATE] player={recipient.Account.Nickname}({recipient.Account.Id}) mode={variant} version={stamp}");
                        continue;
                    }

                    if (variant == "s4" || variant == "bin" || variant == "packet" || variant == "one")
                    {
                        Notify(plr, $"recrear {variant} blocked: UpdateInfo freezes/empties client; use local only");
                        Console.WriteLine($"[CollectBook RECREATE] blocked mode={variant} player={recipient.Account.Nickname}({recipient.Account.Id})");
                        continue;
                    }

                    if (variant == "cache-s4")
                    {
                        var filePath = LocateLargestCollectBookS4();
                        if (filePath == null)
                        {
                            Notify(plr, "recrear cache-s4 failed: collect book s4 not found");
                            return true;
                        }

                        var fileData = File.ReadAllBytes(filePath);
                        if (fileData.Length <= 4)
                        {
                            Notify(plr, "recrear cache-s4 failed: s4 too small");
                            return true;
                        }

                        var rawLength = BitConverter.ToInt32(fileData, 0);
                        var packed = fileData.Skip(4).ToArray();
                        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

                        await recipient.SendAsync(new CollectBook_UpdateRequest_Ack());
                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateRequest_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear cache-s4");

                        await recipient.SendAsync(new CollectBook_UpdateCheck_Ack
                        {
                            Data = stamp
                        });
                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateCheck_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear cache-s4 data='{stamp}'");

                        await recipient.Session.SendAsync(new CollectBook_UpdateInfo_Ack
                        {
                            Unk1 = packed,
                            Unk2 = packed.Length,
                            Unk3 = rawLength,
                            Unk4 = stamp
                        }, SendOptions.ReliableSecureCompress);

                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateInfo_Ack CACHE-S4 | player={recipient.Account.Nickname}({recipient.Account.Id}) version='{stamp}' comp={packed.Length} decomp={rawLength} file={fileData.Length} path={filePath}");
                        Console.WriteLine($"[CollectBook RECREATE] player={recipient.Account.Nickname}({recipient.Account.Id}) mode=cache-s4 version={stamp}");
                        continue;
                    }

                    if (variant == "xml" || variant == "xml-raw" || variant == "xml-raw-lzo")
                    {
                        var filePath = LocateRootCollectBookXml();
                        if (filePath == null)
                        {
                            Notify(plr, "recrear xml failed: collect book xml not found");
                            return true;
                        }

                        var fileData = File.ReadAllBytes(filePath);
                        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                        var useLzo = variant == "xml-raw-lzo";
                        var blob = useLzo ? fileData.CompressLZO() : fileData;

                        await recipient.SendAsync(new CollectBook_UpdateRequest_Ack());
                        await recipient.SendAsync(new CollectBook_UpdateCheck_Ack
                        {
                            Data = stamp
                        });
                        await recipient.Session.SendAsync(new CollectBook_UpdateInfo_Ack
                        {
                            Unk1 = blob,
                            Unk2 = useLzo ? blob.Length : 0,
                            Unk3 = useLzo ? fileData.Length : 0,
                            Unk4 = stamp
                        }, SendOptions.ReliableSecureCompress);

                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateRequest_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear {variant}");
                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateCheck_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear {variant} data='{stamp}'");
                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateInfo_Ack XML-RAW | player={recipient.Account.Nickname}({recipient.Account.Id}) mode={variant} version={stamp} bytes={blob.Length} raw={fileData.Length} path={filePath}");
                        Console.WriteLine($"[CollectBook RECREATE] player={recipient.Account.Nickname}({recipient.Account.Id}) mode={variant} version={stamp}");
                        continue;
                    }

                    if (variant == "cbbin1" || variant == "cbbin2")
                    {
                        var filePath = LocateDecodedCollectBookXml();
                        if (filePath == null)
                        {
                            Notify(plr, $"{variant} failed: decoded collect book xml not found");
                            return true;
                        }

                        var limit = variant == "cbbin1" ? 1 : 2;
                        var blob = SerializeCollectBooks(filePath, out var embeddedVersion, out var bookTotal, limit);
                        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

                        await recipient.SendAsync(new CollectBook_UpdateRequest_Ack());
                        ShopService.ForceCollectBookVersion(
                            recipient.Account.Id,
                            stamp,
                            true,
                            blob,
                            variant,
                            0,
                            0,
                            stamp);
                        await recipient.SendAsync(new CollectBook_UpdateCheck_Ack
                        {
                            Data = stamp
                        });

                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateRequest_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear {variant}");
                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateCheck_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear {variant} data='{stamp}'");
                        Console.WriteLine($"[CollectBook RECREATE] armed CBBIN subset | player={recipient.Account.Nickname}({recipient.Account.Id}) mode={variant} xmlVersion={embeddedVersion} forcedVersion={stamp} books={bookTotal} bytes={blob.Length} head={BitConverter.ToString(blob.Take(24).ToArray())} path={filePath}");
                        continue;
                    }

                    if (variant == "twobooks" || variant == "two" || variant == "goodxml-2")
                    {
                        var filePath = LocateDecodedCollectBookXml();
                        if (filePath == null)
                        {
                            Notify(plr, "recrear twobooks failed: decoded collect book xml not found");
                            return true;
                        }

                        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                        var xmlData = TrimCollectBookXml(File.ReadAllBytes(filePath), stamp, 2);

                        await recipient.SendAsync(new CollectBook_UpdateRequest_Ack());
                        ShopService.ForceCollectBookVersion(recipient.Account.Id, stamp, true, xmlData, "twobooks");
                        await recipient.SendAsync(new CollectBook_UpdateCheck_Ack
                        {
                            Data = stamp
                        });

                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateRequest_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear twobooks");
                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateCheck_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear twobooks data='{stamp}'");
                        Console.WriteLine($"[CollectBook RECREATE] armed UpdateInfo on next UpdateCheck | player={recipient.Account.Nickname}({recipient.Account.Id}) mode=twobooks version={stamp} books=2 bytes={xmlData.Length} head={BitConverter.ToString(xmlData.Take(24).ToArray())} path={filePath}");
                        Console.WriteLine($"[CollectBook RECREATE] player={recipient.Account.Nickname}({recipient.Account.Id}) mode=twobooks version={stamp}");
                        continue;
                    }

                    if (variant == "itemids2" || variant == "ids2")
                    {
                        var filePath = LocateDecodedCollectBookXml();
                        if (filePath == null)
                        {
                            Notify(plr, "recrear itemids2 failed: decoded collect book xml not found");
                            return true;
                        }

                        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                        var blob = BuildKeyBlob(filePath, 2);

                        await recipient.SendAsync(new CollectBook_UpdateRequest_Ack());
                        ShopService.ForceCollectBookVersion(recipient.Account.Id, stamp, true, blob, "itemids2");
                        await recipient.SendAsync(new CollectBook_UpdateCheck_Ack
                        {
                            Data = stamp
                        });

                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateRequest_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear itemids2");
                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateCheck_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear itemids2 data='{stamp}'");
                        Console.WriteLine($"[CollectBook RECREATE] armed UpdateInfo on next UpdateCheck | player={recipient.Account.Nickname}({recipient.Account.Id}) mode=itemids2 version={stamp} ints={blob.Length / 4} bytes={blob.Length} head={BitConverter.ToString(blob.Take(24).ToArray())} path={filePath}");
                        Console.WriteLine($"[CollectBook RECREATE] player={recipient.Account.Nickname}({recipient.Account.Id}) mode=itemids2 version={stamp}");
                        continue;
                    }

                    if (variant == "goodxml-nc" || variant == "goodxml-lzo-nc" || variant == "xml-nc" || variant == "xml-lzo-nc" || variant == "s4-nc" || variant == "s4payload-nc" || variant == "s4-lzo-nc")
                    {
                        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                        byte[] blob;
                        int lenA;
                        int lenB;
                        string filePath;

                        if (variant == "goodxml-nc" || variant == "goodxml-lzo-nc" || variant == "xml-nc" || variant == "xml-lzo-nc")
                        {
                            filePath = variant == "goodxml-nc" || variant == "goodxml-lzo-nc"
                                ? LocateDecodedCollectBookXml()
                                : LocateRootCollectBookXml();
                            if (filePath == null)
                            {
                                Notify(plr, $"recrear {variant} failed: collect book xml not found");
                                return true;
                            }

                            var xmlData = File.ReadAllBytes(filePath);
                            if (variant == "goodxml-nc" || variant == "goodxml-lzo-nc")
                                xmlData = PatchXmlVersion(xmlData, stamp);
                            var useLzo = variant == "xml-lzo-nc" || variant == "goodxml-lzo-nc";
                            blob = useLzo ? xmlData.CompressLZO() : xmlData;
                            lenA = useLzo ? blob.Length : 0;
                            lenB = useLzo ? xmlData.Length : 0;
                        }
                        else
                        {
                            filePath = LocateLargestCollectBookS4();
                            if (filePath == null)
                            {
                                Notify(plr, $"recrear {variant} failed: collect book s4 not found");
                                return true;
                            }

                            var fileData = File.ReadAllBytes(filePath);
                            if (fileData.Length <= 4)
                            {
                                Notify(plr, $"recrear {variant} failed: collect book s4 too small");
                                return true;
                            }

                            var rawLength = BitConverter.ToInt32(fileData, 0);
                            var tail = fileData.Skip(4).ToArray();
                            if (variant == "s4-nc")
                            {
                                blob = fileData;
                                lenA = 0;
                                lenB = 0;
                            }
                            else if (variant == "s4payload-nc")
                            {
                                blob = tail;
                                lenA = 0;
                                lenB = 0;
                            }
                            else
                            {
                                blob = tail;
                                lenA = tail.Length;
                                lenB = rawLength;
                            }
                        }

                        await recipient.SendAsync(new CollectBook_UpdateRequest_Ack());
                        ShopService.ForceCollectBookVersion(recipient.Account.Id, stamp, true);
                        await recipient.SendAsync(new CollectBook_UpdateCheck_Ack
                        {
                            Data = stamp
                        });

                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateRequest_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear {variant}");
                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateCheck_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear {variant} data='{stamp}'");
                        Console.WriteLine($"[CollectBook RECREATE] armed UpdateInfo on next UpdateCheck | player={recipient.Account.Nickname}({recipient.Account.Id}) mode={variant} version={stamp} bytes={blob.Length} unk2={lenA} unk3={lenB} head={BitConverter.ToString(blob.Take(24).ToArray())} path={filePath}");
                        Console.WriteLine($"[CollectBook RECREATE] player={recipient.Account.Nickname}({recipient.Account.Id}) mode={variant} version={stamp}");
                        continue;
                    }

                    if (variant == "xml-lzo" || variant == "unsafe-bin" || variant == "unsafe-packet" || variant == "unsafe-one")
                    {
                        var filePath = LocateRootCollectBookXml();
                        if (filePath == null)
                        {
                            Notify(plr, "recrear bin failed: collect book xml not found");
                            return true;
                        }

                        var singleBook = variant == "unsafe-one";
                        var blob = SerializeCollectBooks(filePath, out var embeddedVersion, out var bookTotal, singleBook ? 1 : (int?)null);
                        var stamp = variant == "xml-lzo" ? DateTime.UtcNow.ToString("yyyyMMddHHmmss") : embeddedVersion;
                        var packed = !singleBook && blob.Length > 0xBB8
                            ? blob.CompressLZO()
                            : null;

                        await recipient.SendAsync(new CollectBook_UpdateRequest_Ack());
                        await recipient.SendAsync(new CollectBook_UpdateCheck_Ack
                        {
                            Data = stamp
                        });
                        await recipient.Session.SendAsync(new CollectBook_UpdateInfo_Ack
                        {
                            Unk1 = packed ?? blob,
                            Unk2 = packed?.Length ?? 0,
                            Unk3 = packed != null ? blob.Length : 0,
                            Unk4 = stamp
                        }, SendOptions.ReliableSecureCompress);

                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateRequest_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear {variant}");
                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateCheck_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear {variant} data='{stamp}'");
                        Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateInfo_Ack XML-LZO | player={recipient.Account.Nickname}({recipient.Account.Id}) mode={variant} version={stamp} bytes={(packed ?? blob).Length}");
                        Console.WriteLine($"[CollectBook RECREATE] player={recipient.Account.Nickname}({recipient.Account.Id}) mode={variant} version={stamp} books={bookTotal} raw={blob.Length} compressed={packed?.Length ?? 0} head={BitConverter.ToString((packed ?? blob).Take(24).ToArray())} path={filePath}");
                        continue;
                    }

                    if (variant == "unsafe-xml-lzo")
                    {
                        var filePath = LocateRootCollectBookXml();
                        if (filePath == null)
                        {
                            Notify(plr, "recrear xml failed: collect book xml not found");
                            return true;
                        }

                        var fileData = File.ReadAllBytes(filePath);
                        var packed = fileData.CompressLZO();
                        var stamp = ReadXmlVersion(filePath);

                        await recipient.SendAsync(new CollectBook_UpdateCheck_Ack());
                        await recipient.Session.SendAsync(new CollectBook_UpdateInfo_Ack
                        {
                            Unk1 = packed,
                            Unk2 = packed.Length,
                            Unk3 = fileData.Length,
                            Unk4 = stamp
                        }, SendOptions.ReliableSecureCompress);
                        await recipient.SendAsync(new CollectBook_UpdateCheck_Ack
                        {
                            Data = stamp
                        });

                        Console.WriteLine($"[CollectBook RECREATE] player={recipient.Account.Nickname}({recipient.Account.Id}) mode=unsafe-xml-lzo version={stamp} compressed={packed.Length} raw={fileData.Length} path={filePath}");
                        continue;
                    }

                    if (variant == "unsafe-s4" || variant == "unsafe-xml")
                    {
                        var kind = variant == "unsafe-xml" ? "xml" : "s4";
                        var filePath = kind == "xml" ? LocateRootCollectBookXml() : LocateCollectBookS4();
                        if (filePath == null)
                        {
                            Notify(plr, $"recrear {kind} failed: collect book file not found");
                            return true;
                        }

                        var fileData = File.ReadAllBytes(filePath);
                        var stamp = kind == "xml" ? ReadXmlVersion(filePath) : "20171116121051";

                        await recipient.SendAsync(new CollectBook_UpdateCheck_Ack());
                        await recipient.SendAsync(new CollectBook_UpdateInfo_Ack
                        {
                            Unk1 = fileData,
                            Unk2 = 0,
                            Unk3 = 0,
                            Unk4 = stamp
                        });
                        await recipient.SendAsync(new CollectBook_UpdateCheck_Ack
                        {
                            Data = stamp
                        });

                        Console.WriteLine($"[CollectBook RECREATE] player={recipient.Account.Nickname}({recipient.Account.Id}) mode={variant} version={stamp} bytes={fileData.Length} path={filePath}");
                        continue;
                    }

                    var localStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    var pushUpdateInfo = variant == "unsafe-update";
                    ShopService.ForceCollectBookVersion(recipient.Account.Id, localStamp, pushUpdateInfo);
                    await recipient.SendAsync(new CollectBook_UpdateRequest_Ack());
                    Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateRequest_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear");
                    await recipient.SendAsync(new CollectBook_UpdateCheck_Ack
                    {
                        Data = localStamp
                    });
                    Console.WriteLine($"[CollectBook PACKET] SE ENVIA CollectBook_UpdateCheck_Ack | player={recipient.Account.Nickname}({recipient.Account.Id}) source=/recrear data='{localStamp}'");

                    Console.WriteLine($"[CollectBook RECREATE] player={recipient.Account.Nickname}({recipient.Account.Id}) mode={(pushUpdateInfo ? "unsafe-update" : "local-date-check")} version={localStamp}");
                }

                Notify(plr, $"recrear sent targets={recipients.Count} mode={variant}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[CollectBook RECREATE] failed: " + ex);
                Notify(plr, "recrear failed: " + ex.Message);
            }

            return true;
        }

        private sealed class CollectBook1108Preset
        {
            public string Label { get; init; }
            public string Version { get; init; }
            public byte[] Payload { get; init; }
            public int Unk2 { get; init; }
            public int Unk3 { get; init; }
            public string Unk4 { get; init; }
            public string SourcePath { get; init; }
        }

        private static bool TryBuild1108Preset(string mode, out CollectBook1108Preset preset)
        {
            preset = null;
            var key = (mode ?? string.Empty).Trim().ToLowerInvariant();
            if (key != "1108a" && key != "1108b" && key != "1108c" &&
                key != "1108d" && key != "1108e" &&
                key != "preset-a" && key != "preset-b" && key != "preset-c" &&
                key != "preset-d" && key != "preset-e")
                return false;

            var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var xmlFile = LocateDecodedCollectBookXml();
            if (xmlFile == null)
                throw new FileNotFoundException("decoded collect book xml not found");

            var xmlBytes = PatchXmlVersion(File.ReadAllBytes(xmlFile), stamp);

            if (key == "1108a" || key == "preset-a")
            {
                preset = new CollectBook1108Preset
                {
                    Label = "1108-A-raw-xml",
                    Version = stamp,
                    Payload = xmlBytes,
                    Unk2 = 0,
                    Unk3 = 0,
                    Unk4 = stamp,
                    SourcePath = xmlFile
                };
                return true;
            }

            if (key == "1108c" || key == "preset-c")
            {
                var s4File = LocateLargestCollectBookS4();
                if (s4File == null)
                    throw new FileNotFoundException("collect book s4 not found");

                preset = new CollectBook1108Preset
                {
                    Label = "1108-C-full-s4",
                    Version = stamp,
                    Payload = File.ReadAllBytes(s4File),
                    Unk2 = 0,
                    Unk3 = 0,
                    Unk4 = stamp,
                    SourcePath = s4File
                };
                return true;
            }

            byte[] packed;
            try
            {
                packed = xmlBytes.CompressLZO();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("LZO compress failed for 1108 preset", ex);
            }

            if (key == "1108b" || key == "preset-b")
            {
                preset = new CollectBook1108Preset
                {
                    Label = "1108-B-lzo-xml",
                    Version = stamp,
                    Payload = packed,
                    Unk2 = 0,
                    Unk3 = 0,
                    Unk4 = stamp,
                    SourcePath = xmlFile
                };
                return true;
            }

            if (key == "1108d" || key == "preset-d")
            {
                preset = new CollectBook1108Preset
                {
                    Label = "1108-D-lzo-len-rawlen",
                    Version = stamp,
                    Payload = packed,
                    Unk2 = packed.Length,
                    Unk3 = xmlBytes.Length,
                    Unk4 = stamp,
                    SourcePath = xmlFile
                };
                return true;
            }

            preset = new CollectBook1108Preset
            {
                Label = "1108-E-rawlen-lzo-len",
                Version = stamp,
                Payload = packed,
                Unk2 = xmlBytes.Length,
                Unk3 = packed.Length,
                Unk4 = stamp,
                SourcePath = xmlFile
            };
            return true;
        }

        public string Help()
        {
            return "/recrear [player|id|all] [1|2|3|4|5|safe|1108a|1108b|1108c|1108d|1108e|cbbin1|cbbin2|legacy-xml00|legacy-xml-len|legacy-body-lens|legacy-body-dec0|legacy-body-lendec|legacy-body-0dec|legacy-body-0body|legacy-s4-full|twobooks|itemids2|goodxml-nc|goodxml-lzo-nc|xml-nc|xml-lzo-nc|s4-nc|s4payload-nc|s4-lzo-nc|local|xml|xml-raw-lzo|xml-lzo|cache-s4] - rebuild collect book";
        }

        private static List<Player> CollectRecipients(Player plr, string[] args)
        {
            if (args.Length == 0)
                return plr != null ? new List<Player> { plr } : new List<Player>();

            if (string.Equals(args[0], "all", StringComparison.OrdinalIgnoreCase))
                return GameServer.Instance.PlayerManager.Where(x => x?.Account != null).ToList();

            var wanted = args[0];
            var found = GameServer.Instance.PlayerManager.FirstOrDefault(x =>
                x?.Account != null &&
                (string.Equals(x.Account.Nickname, wanted, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(x.Account.Username, wanted, StringComparison.OrdinalIgnoreCase) ||
                 (ulong.TryParse(wanted, out var wantedId) && (ulong)x.Account.Id == wantedId)));

            return found != null ? new List<Player> { found } : new List<Player>();
        }

        private static string LocateCollectBookS4()
        {
            foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    var direct = Path.Combine(dir.FullName, "shop", "_eu_collect_book.s4");
                    if (File.Exists(direct))
                        return direct;

                    var release = Path.Combine(dir.FullName, "src", "GameServer", "bin", "LatestOld_Release", "shop", "_eu_collect_book.s4");
                    if (File.Exists(release))
                        return release;

                    dir = dir.Parent;
                }
            }

            return null;
        }

        private static string LocateLargestCollectBookS4()
        {
            var candidates = new List<string>();

            foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    candidates.Add(Path.Combine(dir.FullName, "shop", "_eu_collect_book.s4"));
                    candidates.Add(Path.Combine(dir.FullName, "src", "GameServer", "bin", "LatestOld_Release", "shop", "_eu_collect_book.s4"));
                    dir = dir.Parent;
                }
            }

            candidates.Add(@"C:\Users\sneo\Desktop\S4League\shop\_eu_collect_book.s4");

            return candidates
                .Where(File.Exists)
                .Select(path => new FileInfo(path))
                .Where(file => file.Length > 1024)
                .OrderByDescending(file => file.Length)
                .Select(file => file.FullName)
                .FirstOrDefault();
        }

        private static byte[] TrimCollectBookXml(byte[] raw, string version, int takeBooks)
        {
            if (raw == null || raw.Length == 0)
                return Array.Empty<byte>();

            var doc = XDocument.Parse(Encoding.UTF8.GetString(raw));
            var root = doc.Root;
            if (root == null)
                return raw;

            root.SetAttributeValue("version", version);
            var list = root.Element("collect_list");
            if (list == null)
                return Encoding.UTF8.GetBytes(doc.ToString(SaveOptions.DisableFormatting));

            list.ReplaceNodes(list.Elements("collect_book").Take(takeBooks));
            return Encoding.UTF8.GetBytes(doc.ToString(SaveOptions.DisableFormatting));
        }

        private static byte[] BuildKeyBlob(string path, int takeBooks)
        {
            var doc = XDocument.Load(path);
            var keys = doc.Descendants("collect_book")
                .Take(takeBooks)
                .Elements("collect")
                .Select(x => int.TryParse(x.Attribute("key")?.Value, out var key) ? key : 0)
                .Where(x => x > 0)
                .ToList();

            var buffer = new List<byte>(4 + keys.Count * 4);
            buffer.AddRange(BitConverter.GetBytes(keys.Count));
            foreach (var value in keys)
                buffer.AddRange(BitConverter.GetBytes(value));

            return buffer.ToArray();
        }

        private static string LocateRootCollectBookXml()
        {
            foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    var path = Path.Combine(dir.FullName, "_eu_collect_book.xml");
                    if (File.Exists(path))
                        return path;

                    dir = dir.Parent;
                }
            }

            return null;
        }

        private static string LocateDecodedCollectBookXml()
        {
            foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    var path = Path.Combine(dir.FullName, "decoded_collect_from_s4.xml");
                    if (File.Exists(path))
                        return path;

                    dir = dir.Parent;
                }
            }

            return LocateRootCollectBookXml();
        }

        private static byte[] PatchXmlVersion(byte[] xml, string version)
        {
            var text = Encoding.UTF8.GetString(xml);
            var marker = "version=\"";
            var start = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return xml;

            start += marker.Length;
            var end = text.IndexOf('"', start);
            if (end <= start)
                return xml;

            var patched = text.Substring(0, start) + version + text.Substring(end);
            return Encoding.UTF8.GetBytes(patched);
        }

        private static string ReadXmlVersion(string path)
        {
            var head = File.ReadLines(path).Take(5).FirstOrDefault(x => x.Contains("version=")) ?? string.Empty;
            var marker = "version=\"";
            var start = head.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return "20171116121051";

            start += marker.Length;
            var end = head.IndexOf('"', start);
            return end > start ? head.Substring(start, end - start) : "20171116121051";
        }

        private static byte[] SerializeCollectBooks(string path, out string version, out int bookCount, int? maxBooks = null)
        {
            var doc = XDocument.Load(path);
            var root = doc.Root;
            if (root == null || !string.Equals(root.Name.LocalName, "collect_book_info", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("collect_book_info root not found");

            version = root.Attribute("version")?.Value ?? "20171116121051";
            var query = root
                .Descendants("collect_book")
                .Select(ReadBookElement);
            if (maxBooks.HasValue)
                query = query.Take(maxBooks.Value);

            var books = query.ToList();

            bookCount = books.Count;
            var writer = new CollectBookStreamWriter();
            writer.WriteInt32(books.Count);

            foreach (var book in books)
            {
                writer.WriteInt32((int)book.Key);
                writer.WriteString(book.Type);
                writer.WriteString(book.Grade);
                writer.WriteString(book.PeriodType);
                writer.WriteUInt16(book.Period);

                foreach (var collect in book.Collects.Concat(Enumerable.Repeat(CollectSlot.Empty, 6)).Take(6))
                {
                    writer.WriteInt32((int)collect.Key);
                    writer.WriteInt32((int)collect.BuyCapsuleKey);
                    writer.WriteByte(collect.Color);
                }

                foreach (var reward in book.Rewards.Concat(Enumerable.Repeat(RewardSlot.Empty, 5)).Take(5))
                {
                    writer.WriteString(reward.RewardType);
                    writer.WriteInt32((int)reward.EffectId);
                }
            }

            return writer.ToArray();
        }

        private static CollectBookDefinition ReadBookElement(XElement book)
        {
            return new CollectBookDefinition
            {
                Key = AttrUInt(book, "key"),
                Type = AttrString(book, "type", "EQUIP"),
                Grade = AttrString(book, "grade", "NORMAL"),
                PeriodType = AttrString(book, "period_type", "DAYS"),
                Period = (ushort)AttrUInt(book, "period"),
                Collects = book.Elements("collect")
                    .Select(x => new CollectSlot
                    {
                        Key = AttrUInt(x, "key"),
                        Color = (byte)AttrUInt(x, "color"),
                        BuyCapsuleKey = AttrUInt(x, "buycapsulekey")
                    })
                    .ToList(),
                Rewards = book.Elements("reward")
                    .Select(x => new RewardSlot
                    {
                        EffectId = AttrUInt(x, "effect_id"),
                        RewardType = AttrString(x, "reward_type", "NONE")
                    })
                    .ToList()
            };
        }

        private static uint AttrUInt(XElement element, string name)
        {
            return uint.TryParse(element.Attribute(name)?.Value, out var value) ? value : 0;
        }

        private static string AttrString(XElement element, string name, string fallback)
        {
            var value = element.Attribute(name)?.Value;
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private sealed class CollectBookStreamWriter
        {
            private readonly MemoryStream _buffer = new MemoryStream();

            public void WriteInt32(int value)
            {
                Emit(BitConverter.GetBytes(value));
            }

            public void WriteUInt16(ushort value)
            {
                Emit(BitConverter.GetBytes(value));
            }

            public void WriteByte(byte value)
            {
                Emit(new[] { value });
            }

            public void WriteString(string value)
            {
                var bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
                WriteByte(1);
                WriteCompactInt(bytes.Length);
                if (bytes.Length > 0)
                    Emit(bytes);
            }

            public byte[] ToArray()
            {
                return _buffer.ToArray();
            }

            private void WriteCompactInt(int value)
            {
                if (value >= sbyte.MinValue && value <= sbyte.MaxValue)
                {
                    Emit(new[] { (byte)1, unchecked((byte)value) });
                    return;
                }

                if (value >= short.MinValue && value <= short.MaxValue)
                {
                    var bytes = BitConverter.GetBytes((short)value);
                    Emit(new[] { (byte)2, bytes[0], bytes[1] });
                    return;
                }

                var intBytes = BitConverter.GetBytes(value);
                Emit(new[] { (byte)4, intBytes[0], intBytes[1], intBytes[2], intBytes[3] });
            }

            private void Emit(byte[] bytes)
            {
                _buffer.Write(bytes, 0, bytes.Length);
            }
        }

        private sealed class CollectBookDefinition
        {
            public uint Key { get; set; }
            public string Type { get; set; }
            public string Grade { get; set; }
            public string PeriodType { get; set; }
            public ushort Period { get; set; }
            public List<CollectSlot> Collects { get; set; }
            public List<RewardSlot> Rewards { get; set; }
        }

        private struct CollectSlot
        {
            public static readonly CollectSlot Empty = new CollectSlot();

            public uint Key { get; set; }
            public uint BuyCapsuleKey { get; set; }
            public byte Color { get; set; }
        }

        private struct RewardSlot
        {
            public static readonly RewardSlot Empty = new RewardSlot
            {
                RewardType = "NONE"
            };

            public uint EffectId { get; set; }
            public string RewardType { get; set; }
        }

        private static void Notify(Player plr, string msg)
        {
            if (plr == null)
                Console.WriteLine(msg);
            else
                plr.SendConsoleMessage(S4Color.Green + msg);
        }
    }
}
