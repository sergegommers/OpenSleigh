using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenSleigh.Core.DependencyInjection;
using OpenSleigh.Persistence.SQL;
using OpenSleigh.Persistence.SQLServer;
using OpenSleigh.Samples.Sample11.Common.Messages;
using OpenSleigh.Samples.Sample11.Sagas;
using OpenSleigh.Samples.Sample11.Worker.Sagas;
using OpenSleigh.Transport.RabbitMQ;

namespace OpenSleigh.Samples.Sample11.Worker
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var hostBuilder = CreateHostBuilder(args);
            var host = hostBuilder.Build();

            var serviceCollection = host.Services.GetRequiredService<IServiceCollection>();

            var keyboardTask = ProcessKeyBoard(serviceCollection);
            var hostTask = host.RunAsync();

            DisplayHelp();

            await Task.WhenAny(keyboardTask, hostTask);
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton(services);

                services.AddLogging(cfg =>
                    {
                        cfg.AddConsole();
                    })
                    .AddOpenSleigh(cfg =>
                    {
                        services.AddSingleton(cfg);

                        var rabbitSection = hostContext.Configuration.GetSection("Rabbit");
                        var rabbitCfg = new RabbitConfiguration(rabbitSection["HostName"],
                            rabbitSection["UserName"],
                            rabbitSection["Password"]);

                        var sqlConnStr = hostContext.Configuration.GetConnectionString("sql");
                        var sqlConfig = new SqlConfiguration(sqlConnStr);

                        cfg.UseRabbitMQTransport(rabbitCfg, builder =>
                            {
                                // OPTIONAL
                                // using a custom naming policy allows us to define the names for exchanges and queues.
                                // this allows us to have a single exchange bound to multiple queues.
                                // messages will be routed using the queue name.
                                builder.UseMessageNamingPolicy<StartChildSaga>(() =>
                                    new QueueReferences("child", "child.start", "child.start", "child.dead", "child.dead.start"));
                                builder.UseMessageNamingPolicy<ProcessChildSaga>(() =>
                                    new QueueReferences("child", "child.process", "child.process", "child.dead", "child.dead.process"));
                            })
                            .UseSqlServerPersistence(sqlConfig);

                        cfg.AddSaga<SimpleSaga, SimpleSagaState>()
                            .UseStateFactory<StartSimpleSaga>(msg => new SimpleSagaState(msg.CorrelationId))
                            .UseRabbitMQTransport();

                        cfg.AddSaga<ParentSaga, ParentSagaState>()
                            .UseStateFactory<StartParentSaga>(msg => new ParentSagaState(msg.CorrelationId))
                            .UseRabbitMQTransport();

                        cfg.AddSaga<ChildSaga, ChildSagaState>()
                            .UseStateFactory<StartChildSaga>(msg => new ChildSagaState(msg.CorrelationId))
                            .UseRabbitMQTransport();
                    });
            });

        static void DisplayHelp()
        {
            Console.WriteLine();
            Console.WriteLine("Available commands:");
            Console.WriteLine("1 - Add Saga");
            Console.WriteLine("q - quit");
            Console.WriteLine();
        }

        static async Task ProcessKeyBoard(IServiceCollection services)
        {
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey();

                    if (keyInfo.KeyChar == '1')
                    {
                        AddSaga(services);
                    }
                    if (keyInfo.KeyChar == 'q')
                    {
                        return;
                    }

                    DisplayHelp();
                }

                await Task.Delay(100);
            }
        }

        static void AddSaga(IServiceCollection services)
        {
            ServiceProvider q = services.BuildServiceProvider();
            var cfg = q.GetRequiredService<IBusConfigurator>();

            void configure(IBusConfigurator cfg)
            {
                cfg.AddSaga<PluginSaga, PluginSagaState>()
                    .UseStateFactory<StartPluginSaga>(msg => new PluginSagaState(msg.CorrelationId))
                    .UseRabbitMQTransport();
            }

            services.RebuildOpenSleigh(configure);
        }
    }
}
