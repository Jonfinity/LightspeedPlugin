using AssettoServer.Network.Tcp;
using Serilog;

namespace LightspeedPlugin;

public sealed class LightspeedSession
{
    public ACTcpClient _client;
    private LightspeedPlugin _plugin;
    public string _username = LightspeedPlugin.NO_NAME;

    private Queue<int> _speedList = new Queue<int>();
    private long _creationTime;

    private int _topSpeed = 0;

    public LightspeedSession(ACTcpClient client, LightspeedPlugin plugin)
    {
        Log.Information(string.Format("Session created: {0}", client.Name ?? LightspeedPlugin.NO_NAME));

        _client = client;
        _plugin = plugin;
        _username = client.Name ?? LightspeedPlugin.NO_NAME;
    }

    public void OnCreation()
    {
        _creationTime = _plugin._sessionManager.ServerTimeMilliseconds;
    }

    public void OnRemove()
    {
        if(!_client.HasSentFirstUpdate || !_client.HasPassedChecksum)
        {
            return;
        }

        _plugin.CreateClientForEntryCarData(_client.EntryCar, _client, this);
    }

    public string getUsername()
    {
        return LightspeedPlugin.SanitizeUsername(_username);
    }

    public int GetAverageSpeed()
    {
        if(_speedList.Count < 1)
        {
            return 0;
        }
        
        return _speedList.Sum() / _speedList.Count;
    }

    public Queue<int> GetSpeedList()
    {
        return _speedList;
    }

    public void AddToAverageSpeed(int speed)
    {
        if(_speedList.Count > 15)
        {
            _speedList.Dequeue();
        }

        _speedList.Enqueue(speed);
    }

    public double CalculateDistanceDriven()
    {
        double hours = TimeSpan.FromMilliseconds(CalculateTimeSpent()).TotalHours;
        long avgSpeed = GetAverageSpeed();
        return avgSpeed * hours;
    }

    public int GetTopSpeed()
    {
        return _topSpeed;
    }

    public void SetTopSpeed(int speed)
    {
        _topSpeed = speed;
    }

    public long CalculateTimeSpent()
    {
        return _plugin._sessionManager.ServerTimeMilliseconds - _creationTime;
    }
}
