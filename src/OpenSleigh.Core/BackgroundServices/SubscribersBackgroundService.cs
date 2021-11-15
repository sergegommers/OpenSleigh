using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenSleigh.Core.Messaging;

namespace OpenSleigh.Core.BackgroundServices
{
    public class SubscribersBackgroundService : BackgroundService
    {
        private readonly IEnumerable<ISubscriber> _subscribers;
        private readonly SystemInfo _systemInfo;
        private readonly ILogger<SubscribersBackgroundService> _logger;

        public SubscribersBackgroundService(IEnumerable<ISubscriber> subscribers, 
            SystemInfo systemInfo, 
            ILogger<SubscribersBackgroundService> logger)
        {
            _subscribers = subscribers ?? throw new ArgumentNullException(nameof(subscribers));
            _systemInfo = systemInfo ?? throw new ArgumentNullException(nameof(systemInfo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (!_systemInfo.PublishOnly)
            {
                _logger.LogInformation($"stopping subscribers on client '{_systemInfo.ClientId}' ...");
                await Task.WhenAll(_subscribers.Select(s => s.StopAsync(cancellationToken)));
            }                

            await base.StopAsync(cancellationToken);
        }

        public async Task StartAddedSubscribers(IServiceCollection services)
        {
            _logger.LogInformation($"Finding new subscribers to start");

            var q = services.BuildServiceProvider();
            var subscribers = q.GetServices<ISubscriber>();

            var missingSubscribers = new List<ISubscriber>();
            foreach (var sub in subscribers)
            {
                // todo: include version of plugin
                if (!this._subscribers.Any(x => x.QueueName == sub.QueueName))
                {
                    missingSubscribers.Add(sub);

                    _logger.LogInformation($"Found subscriber to start: {sub.QueueName}");
                }
            }

            // todo: we do start the tasks here, but are they long-running?
            // todo: maybe we should create a new instance of the backgroundservice for each plugin?
            // this makes it maybe easier to unload the plugin.

            _logger.LogInformation($"Starting new subscribers on client '{_systemInfo.ClientGroup}/{_systemInfo.ClientId}' ...");

            var tasks = missingSubscribers.Select(s => s.StartAsync(default));
            var combinedTask = Task.WhenAll(tasks);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_systemInfo.PublishOnly)
            {
                _logger.LogInformation($"no subscribers on client '{_systemInfo.ClientId}'");
                return Task.CompletedTask;
            }                

            _logger.LogInformation($"starting subscribers on client '{_systemInfo.ClientGroup}/{_systemInfo.ClientId}' ...");
            
            var tasks = _subscribers.Select(s => s.StartAsync(stoppingToken));
            var combinedTask = Task.WhenAll(tasks);
            return combinedTask;            
        }            
    }
}
