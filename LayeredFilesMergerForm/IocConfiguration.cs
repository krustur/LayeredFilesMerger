using LayeredFilesMergerEngine;
using Serilog;
using Unity;
using Unity.Injection;
using Unity.Lifetime;

namespace LayeredFilesMergerForm
{
    public static class IocConfiguration
    {
        public static void Configure(IUnityContainer container, ILogger logger)
        {
            container.RegisterType<IEngine, Engine>();
            container.RegisterType<IMainForm, MainForm>();
            container.RegisterType<ILayerFactory, LayerFactory>();
            container.RegisterType<ILayerDetailsService, LayerDetailsService>();
            container.RegisterType<ILayerNameInfoService, LayerNameInfoService>();
            //container.RegisterTypes(
            //    AllClasses.FromLoadedAssemblies(),
            //    WithMapping.MatchingInterface,
            //    WithName.Default,
            //    WithLifetime.ContainerControlled);

            container.RegisterType<ILogger>(new ContainerControlledLifetimeManager(), new InjectionFactory((ctr, type, name) =>
            {
                return logger;
            }));
        }
    }
}