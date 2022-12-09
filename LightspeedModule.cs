using AssettoServer.Server.Plugin;
using Autofac;

namespace LightspeedPlugin;

public class LightspeedModule : AssettoServerModule
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<LightspeedPlugin>().AsSelf().AutoActivate().SingleInstance();
    }
}