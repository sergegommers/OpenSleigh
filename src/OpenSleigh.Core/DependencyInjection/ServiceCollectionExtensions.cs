using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using OpenSleigh.Core.BackgroundServices;
using OpenSleigh.Core.Messaging;
using OpenSleigh.Core.Utils;

namespace OpenSleigh.Core.DependencyInjection
{
    [ExcludeFromCodeCoverage]
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddOpenSleigh(this IServiceCollection services, Action<IBusConfigurator> configure = null)
        {
            var systemInfo = SystemInfo.New();

            var typeResolver = new TypeResolver();
            var sagaTypeResolver = new SagaTypeResolver(typeResolver);

            services.AddTransient<IMessageBus, DefaultMessageBus>()
                .AddSingleton(systemInfo)
                .AddSingleton<ISagaTypeResolver>(sagaTypeResolver)
                .AddSingleton<ISagasRunner, SagasRunner>()
                .AddSingleton<ITypesCache, TypesCache>()
                .AddSingleton<ITypeResolver>(typeResolver)

                .AddSingleton<ITransportSerializer, JsonSerializer>()
                .AddSingleton<IPersistenceSerializer, JsonSerializer>()

                .AddSingleton<IMessageHandlersResolver, DefaultMessageHandlersResolver>()
                .AddSingleton<IMessageHandlersRunner, DefaultMessageHandlersRunner>()
                .AddSingleton<IMessageContextFactory, DefaultMessageContextFactory>()

                .AddSingleton<IMessageProcessor, MessageProcessor>()

                .AddSingleton<SubscribersBackgroundService>()
                .AddHostedService(sp => sp.GetRequiredService<SubscribersBackgroundService>())

                .AddTransient<IOutboxProcessor, OutboxProcessor>()
                .AddSingleton(OutboxProcessorOptions.Default)
                .AddHostedService<OutboxBackgroundService>()

                .AddTransient<IOutboxCleaner, OutboxCleaner>()
                .AddSingleton(OutboxCleanerOptions.Default)
                .AddHostedService<OutboxCleanerBackgroundService>()
                ;

            var builder = new BusConfigurator(services, sagaTypeResolver, typeResolver, systemInfo);
            configure?.Invoke(builder);

            return services;
        }

        public static IServiceCollection AddBusSubscriber(this IServiceCollection services, Type subscriberType)
        {
            if (!services.Any(s => s.ImplementationType == subscriberType))
                services.AddSingleton(typeof(ISubscriber), subscriberType);
            return services;
        }

        public static IServiceCollection RebuildOpenSleigh(this IServiceCollection services, Action<OpenSleigh.Core.DependencyInjection.IBusConfigurator> configure = null)
        {
            ServiceProvider q = services.BuildServiceProvider();

            var sagaTypeResolver = q.GetRequiredService<ISagaTypeResolver>();
            var typeResolver = q.GetRequiredService<ITypeResolver>();
            var systemInfo = q.GetRequiredService<SystemInfo>();

            var builder = q.GetRequiredService<OpenSleigh.Core.DependencyInjection.IBusConfigurator>();

            configure?.Invoke(builder);

            var serv = q.GetService<SubscribersBackgroundService>();

            serv.StartAddedSubscribers(services).Wait();

            return services;
        }

    }

}