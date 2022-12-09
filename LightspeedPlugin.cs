using AssettoServer.Server;
using AssettoServer.Network.Tcp;
using Serilog;
using System.Text.RegularExpressions;

namespace LightspeedPlugin;

public class LightspeedPlugin
{
    private const string ENTRY_CAR_DATA_DIR = "entry_car_data";
    public const string NO_NAME = "No Name";

    public class entryCarData
    {
        public int TotalKmDriven { get; set; } = 0;
        public int BestTopSpeed { get; set; } = 0;
        public string MostCommonDriver { get; set; } = NO_NAME;
    }

    public class clientEntryCarData
    {
        public string Username { get; set; } = NO_NAME;
        public double KmDriven { get; set; } = 0;
        public double TimeSpent { get; set; } = 0;
        public int TopSpeed { get; set; } = 0;
    }

    private readonly LightspeedSessionManager _lightspeedSessionManager;
    private readonly System.Timers.Timer _entryCarDataTimer;
    
    private readonly EntryCarManager _entryCarManager;
    public readonly SessionManager _sessionManager;
    private readonly CSPFeatureManager _cspFeatureManager;

    private static readonly string[] SensitiveCharacters = { "\\", "*", "_", "~", "`", "|", ">", ":", "@" };
    private static readonly string[] ForbiddenUsernameSubstrings = { "discord", "@", "#", ":", "```" };
    private static readonly string[] ForbiddenUsernames = { "everyone", "here" };

    public LightspeedPlugin(EntryCarManager entryCarManager, SessionManager sessionManager, CSPFeatureManager cspFeatureManager)
    {
        Log.Information("------------------------------------");
        Log.Information("Starting: Lightspeed Plugin by Yhugi");
        Log.Information("Made for Lightspeed");
        Log.Information("discord.gg/dnzdf5E7Zb");
        Log.Information("------------------------------------");

        _entryCarManager = entryCarManager;
        _sessionManager = sessionManager;
        _cspFeatureManager = cspFeatureManager;

        _lightspeedSessionManager = new LightspeedSessionManager(this);

        entryCarManager.ClientConnected += OnClientConnected;
        entryCarManager.ClientDisconnected += OnClientDisconnected;

        _cspFeatureManager.Add(new CSPFeature { Name = "The Lightspeed Experience" });
        _cspFeatureManager.Add(new CSPFeature { Name = "VALID_DRIVERS" });

        if(!Directory.Exists(ENTRY_CAR_DATA_DIR))
        {
            Directory.CreateDirectory(ENTRY_CAR_DATA_DIR);
        }

        var timer1 = new System.Timers.Timer();
        timer1.Elapsed += new System.Timers.ElapsedEventHandler(HandleClientEntryCars);
        timer1.Interval = 1000;
        timer1.Start();

        _entryCarDataTimer = new System.Timers.Timer();
        _entryCarDataTimer.Elapsed += new System.Timers.ElapsedEventHandler(StartEntryCarData);
        _entryCarDataTimer.Interval = 8000;
        _entryCarDataTimer.Start();
    }

    private void OnClientConnected(ACTcpClient client, EventArgs args)
    {
        _lightspeedSessionManager.AddSession(client);
    }

    private void OnClientDisconnected(ACTcpClient client, EventArgs args)
    {
        _lightspeedSessionManager.RemoveSession(client);
    }

    private void HandleClientEntryCars(object? sender, EventArgs e)
    {
        foreach (var dictSession in _lightspeedSessionManager.GetSessions())
        {
            var session = dictSession.Value;
            if(session.GetType() == typeof(LightspeedSession))
            {
                ACTcpClient client = session._client;
                if(client.IsConnected && client.HasSentFirstUpdate)
                {
                    EntryCar car = client.EntryCar;
                    if(car != null)
                    {
                        int speedKmh = ((int) (car.Status.Velocity.Length() * 3.6f));
                        session.AddToAverageSpeed(speedKmh);
                        if(speedKmh > session.GetTopSpeed())
                        {
                            session.SetTopSpeed(speedKmh);
                        }
                    }
                }
            }
        }
    }

    private void StartEntryCarData(object? sender, EventArgs e)
    {
        foreach (var car in _entryCarManager.EntryCars)
        {
            if(!car.AiControlled)
            {
                HandleEntryCarData(car);
            }
        }

        _entryCarDataTimer.Stop();
    }

    private async void HandleEntryCarData(EntryCar car)
    {
        string commonDriver = NO_NAME;
        ACTcpClient? client = car.Client;
        if(client != null)
        {
            commonDriver = client.Name ?? NO_NAME;
        }

        entryCarData newData = new entryCarData(){
            TotalKmDriven = 0,
            BestTopSpeed = 0,
            MostCommonDriver = commonDriver,
        };

        string model = car.Model;
        string dir = $"{ENTRY_CAR_DATA_DIR}/{model}/";
        if(!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string newDir =  $"{dir}{model}.json";
        if(!File.Exists(newDir))
        {
            FileStream stream = File.Create(newDir);
            await System.Text.Json.JsonSerializer.SerializeAsync(stream, newData);
            await stream.DisposeAsync();
        }
    }

    public async void CreateClientForEntryCarData(EntryCar car, ACTcpClient client, LightspeedSession session)
    {
        string guid = client.Guid.ToString();
        string dir = $"{ENTRY_CAR_DATA_DIR}/{car.Model}/{guid}/";
        if(!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        clientEntryCarData newData = new clientEntryCarData(){
            Username = session.getUsername(),
            KmDriven = session.CalculateDistanceDriven(),
            TimeSpent = session.CalculateTimeSpent(),
            TopSpeed = session.GetTopSpeed(),
        };

        string newDir = $"{dir}{guid}.json";
        FileStream stream;
        if(!File.Exists(newDir))
        {
            stream = File.Create(newDir);
        }
        else
        {
            stream = File.Open(newDir, FileMode.Open);
            clientEntryCarData? existingData = DeserializeFromStream(stream, newDir);
            if(existingData != null)
            {
                newData.KmDriven += existingData.KmDriven;
                newData.TimeSpent += existingData.TimeSpent;
                if(existingData.TopSpeed > newData.TopSpeed)
                {
                    newData.TopSpeed = existingData.TopSpeed;
                }
            }
        }

        stream.SetLength(0);
        
        await System.Text.Json.JsonSerializer.SerializeAsync(stream, newData);
        await stream.DisposeAsync();
    }

    public static string SanitizeUsername(string name)
    {
        foreach (string str in ForbiddenUsernames)
        {
            if (name == str) return $"_{str}";
        }

        foreach (string str in ForbiddenUsernameSubstrings)
        {
            name = Regex.Replace(name, str, new string('*', str.Length), RegexOptions.IgnoreCase);
        }

        name = name.Substring(0, Math.Min(name.Length, 80));

        return name;
    }

    public static clientEntryCarData? DeserializeFromStream(FileStream stream, string dir)
    {
        return (clientEntryCarData?) System.Text.Json.JsonSerializer.Deserialize(stream, typeof(clientEntryCarData));
    }
}
