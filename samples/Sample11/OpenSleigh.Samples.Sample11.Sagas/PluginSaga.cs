using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenSleigh.Core;
using OpenSleigh.Core.Messaging;
using OpenSleigh.Samples.Sample11.Common.Messages;

namespace OpenSleigh.Samples.Sample11.Sagas
{
    public class PluginSagaState : SagaState
    {
        public PluginSagaState(Guid id) : base(id) { }
    }

    public record ProcessPluginSaga(Guid Id, Guid CorrelationId) : ICommand { }

    public record PluginSagaCompleted(Guid Id, Guid CorrelationId) : IEvent { }

    public class PluginSaga :
        Saga<PluginSagaState>,
        IStartedBy<StartPluginSaga>,
        IHandleMessage<ProcessPluginSaga>,
        IHandleMessage<PluginSagaCompleted>
    {
        private readonly ILogger<PluginSaga> _logger;

        public PluginSaga(ILogger<PluginSaga> logger, PluginSagaState state) : base(state)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task HandleAsync(IMessageContext<StartPluginSaga> context, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"starting saga '{context.Message.CorrelationId}'...");

            //throw new ApplicationException("Kaboem");

            var message = new ProcessPluginSaga(Guid.NewGuid(), context.Message.CorrelationId);
            Publish(message);
        }

        public async Task HandleAsync(IMessageContext<ProcessPluginSaga> context, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"processing saga '{context.Message.CorrelationId}'...");
            //throw new ApplicationException("Kaboem");

            var message = new PluginSagaCompleted(Guid.NewGuid(), context.Message.CorrelationId);
            Publish(message);
        }

        public Task HandleAsync(IMessageContext<PluginSagaCompleted> context, CancellationToken cancellationToken = default)
        {
            State.MarkAsCompleted();
            _logger.LogInformation($"saga '{context.Message.CorrelationId}' completed!");
            return Task.CompletedTask;
        }
    }
}
