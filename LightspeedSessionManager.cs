using AssettoServer.Network.Tcp;
using Serilog;

namespace LightspeedPlugin;

public class LightspeedSessionManager
{
    private LightspeedPlugin _plugin;
    private Dictionary<ulong, LightspeedSession> _sessions = new Dictionary<ulong, LightspeedSession>();

    public LightspeedSessionManager(LightspeedPlugin plugin)
    {
        _plugin = plugin;
    }

    public Dictionary<ulong, LightspeedSession> GetSessions()
    {
        return _sessions;
    }

    public LightspeedSession? GetSession(ACTcpClient client)
    {
        if(_sessions.ContainsKey(client.Guid))
        {
            return _sessions[client.Guid];
        }

        return null;
    }

    public LightspeedSession? AddSession(ACTcpClient client)
    {
        if(GetSession(client) != null)
        {
            return null;
        }

        Log.Information(string.Format("Creating new Session for: {0}", client.Name ?? LightspeedPlugin.NO_NAME));
        LightspeedSession newSession = new LightspeedSession(client, _plugin);
        _sessions[client.Guid] = newSession;
        newSession.OnCreation();

        return newSession;
    }

    public void RemoveSession(ACTcpClient client)
    {
        LightspeedSession? session = GetSession(client);
        if(session == null)
        {
            return;
        }

        session.OnRemove();
        _sessions.Remove(client.Guid);
        Log.Information(string.Format("Closing Session for: {0}", client.Name ?? LightspeedPlugin.NO_NAME));
    }
}