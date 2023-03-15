using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Konata.Core;
using Konata.Core.Common;
using Konata.Core.Events.Model;
using Konata.Core.Interfaces;
using Konata.Core.Interfaces.Api;
using Konata.Core.Message.Model;

using System.Net.Sockets;

using Konata.Core.Events;
using Konata.Core.Message;

using KonataAdapter.Connection;
using KonataAdapter.Extensions;
using KonataAdapter.Message;

using Newtonsoft.Json;

using JsonSerializer = System.Text.Json.JsonSerializer;
using MessageBuilder = Konata.Core.Message.MessageBuilder;

namespace KonataAdapter;

public static class Program
{
    private static Bot _bot = null!;
    private static StringOverSocket _stringOverSocket;
    private static List<MessageStruct> _msgBuff = new List<MessageStruct>();

    public static async Task Main()
    {
        // Create bot
        _bot = BotFather.Create(GetConfig(), GetDevice(), GetKeyStore());

        // Print the log
        _bot.OnLog += (_, e) =>
        {
            if (e.Level != LogLevel.Verbose)
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] " +
                                  $"[{e.Level}] [{e.Tag}] {e.EventMessage}");
        };

        // Handle the captcha
        _bot.OnCaptcha += (s, e) =>
        {
            switch (e.Type)
            {
                case CaptchaEvent.CaptchaType.Sms:
                    Console.WriteLine(e.Phone);
                    s.SubmitSmsCode(Console.ReadLine());
                    break;

                case CaptchaEvent.CaptchaType.Slider:
                    Console.WriteLine(e.SliderUrl);
                    s.SubmitSliderTicket(Console.ReadLine());
                    break;

                default:
                case CaptchaEvent.CaptchaType.Unknown:
                    break;
            }
        };

        // Handle group messages
        _bot.OnGroupMessage += GroupMessageHandler;

        // Update the keystore
        _bot.OnBotOnline += (bot, _) =>
        {
            UpdateKeystore(bot.KeyStore);
            Console.WriteLine("Bot keystore updated.");
        };

        // Login the bot
        if (!await _bot.Login())
        {
            Console.WriteLine("Oops... Login failed.");
            return;
        }

        FuturedSocket futuredSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Console.WriteLine("Begin Accept");
        await futuredSocket.Connect("127.0.0.1", 10616, 4);
        Console.WriteLine($"Accepted {futuredSocket.InnerSocket.RemoteEndPoint}");

        _stringOverSocket = new StringOverSocket(futuredSocket);

        SocketMessage msg = new SocketMessage()
        {
            BotAdapter = "konata",
            Conn = "socket",
            Protocol = "cs",
            Type = "connection"
        };
        var startMessage = JsonConvert.SerializeObject(msg);
        await _stringOverSocket.Send(startMessage);

        // cli
        while (true)
        {
            try
            {
                if (!_stringOverSocket.WorkSocket.Connected)
                {
                    await _stringOverSocket.WorkSocket.Connect("127.0.0.1", 10616, 4);
                }
                
                var s = await _stringOverSocket.Receive();
                if (s.IsNullOrEmpty())
                {
                    await _stringOverSocket.WorkSocket.Disconnect();
                }
                else
                {
                    await HandleMessage(s);
                }

                /*switch (Console.ReadLine())
                {
                    case "/stop":
                        await _bot.Logout();
                        _bot.Dispose();
                        return;

                    case "/login":
                        await _bot.Login();
                        break;
                }*/
            }
            catch (Exception e)
            {
                Console.WriteLine($"{e.Message}\n{e.StackTrace}");
            }
        }

    }

    private static async Task<(string, string)> ConstructSocketMessage(ProtocolEvent events)
    {
        if (events is GroupMessageEvent gme)
        {
            var info = await _bot.GetGroupMemberInfo(gme.GroupUin, gme.MemberUin);
            var gl = info.Role switch
            {
                RoleType.Owner  => 2u,
                RoleType.Admin  => 1u,
                RoleType.Member => 0u,
                _               => 0u,
            };
            MessageHeader mh = new()
            {
                GroupId = gme.GroupUin,
                GroupName = gme.GroupName,
                Level = gl,
                SenderId = gme.MemberUin,
                SenderName = gme.MemberCard,
                Sequence = gme.Message.Sequence,
                Time = gme.Message.Time,
                Uuid = gme.Message.Uuid,
            };

            var mc = gme.Chain.ToString();
            return (
                JsonConvert.SerializeObject(mh),
                mc
            );
        }

        return ("", "");
    }

    private static async Task HandleMessage(string message)
    {
        Console.WriteLine(message);

        var header = message.Split('\n')[0];
        var SenderId = 0u;
        if (header.StartsWith("[cs:gs:"))
        {
            SenderId = Convert.ToUInt32(header.GetSandwichedText("[cs:gs:", "]"));
        }

        var body = message.Substring(header.Length + 1).Trim();

        var mc = MessageBuilder.Eval(body);

        await _bot.SendGroupMessage(SenderId, mc);
    }

    /// <summary>
    /// Load or create device 
    /// </summary>
    /// <returns></returns>
    private static BotDevice? GetDevice()
    {
        // Read the device from config
        if (File.Exists("device.json"))
        {
            return JsonSerializer.Deserialize
                <BotDevice>(File.ReadAllText("device.json"));
        }

        // Create new one
        var device = BotDevice.Default();
        {
            var deviceJson = JsonSerializer.Serialize(device,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("device.json", deviceJson);
        }

        return device;
    }

    /// <summary>
    /// Load or create configuration
    /// </summary>
    /// <returns></returns>
    private static BotConfig? GetConfig()
    {
        // Read the device from config
        if (File.Exists("config.json"))
        {
            return JsonSerializer.Deserialize
                <BotConfig>(File.ReadAllText("config.json"));
        }

        // Create new one
        var config = new BotConfig
        {
            EnableAudio = true,
            TryReconnect = true,
            HighwayChunkSize = 8192,
            DefaultTimeout = 6000,
            Protocol = OicqProtocol.AndroidPhone
        };

        // Write to file
        var configJson = JsonSerializer.Serialize(config,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText("config.json", configJson);

        return config;
    }

    /// <summary>
    /// Load or create keystore
    /// </summary>
    /// <returns></returns>
    private static BotKeyStore? GetKeyStore()
    {
        // Read the device from config
        if (File.Exists("keystore.json"))
        {
            return JsonSerializer.Deserialize
                <BotKeyStore>(File.ReadAllText("keystore.json"));
        }

        Console.WriteLine("For first running, please " +
                          "type your account and password.");

        Console.Write("Account: ");
        var account = Console.ReadLine();

        Console.Write("Password: ");
        var password = Console.ReadLine();

        // Create new one
        Console.WriteLine("Bot created.");
        return UpdateKeystore(new BotKeyStore(account, password));
    }

    /// <summary>
    /// Update keystore
    /// </summary>
    /// <param name="keystore"></param>
    /// <returns></returns>
    private static BotKeyStore UpdateKeystore(BotKeyStore keystore)
    {
        var keystoreJson = JsonSerializer.Serialize(keystore,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText("keystore.json", keystoreJson);
        return keystore;
    }

    private static async void GroupMessageHandler(Bot bot, GroupMessageEvent group)
    {
        // Ignore messages from bot itself
        if (group.MemberUin == bot.Uin) return;

        // Takeout text chain for below processing
        //var textChain = group.Chain.GetChain<TextChain>();
        //if (textChain is null) return;

        _msgBuff.Add(group.Message);
        if (_msgBuff.Count > 4096)
            _msgBuff.RemoveAt(0);

        var rc = group.Chain.GetChain<ReplyChain>();

        MessageBuilder? reply = null;

        try
        {
            var (header, body) = await ConstructSocketMessage(group);
            
            if(!_stringOverSocket.WorkSocket.Connected)
                await _stringOverSocket.WorkSocket.Connect("127.0.0.1", 10616, 4);
                
            await _stringOverSocket.Send(header + "\n" + body);

            // Send reply message
            //if (reply is not null) await bot.SendGroupMessage(group.GroupUin, reply);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
        }
    }
}
