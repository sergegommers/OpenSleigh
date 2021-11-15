using System.Threading;
using System.Threading.Tasks;

namespace OpenSleigh.Core.Messaging
{
    public interface ISubscriber
    {
        public string QueueName { get; }

        Task StartAsync(CancellationToken cancellationToken = default);

        Task StopAsync(CancellationToken cancellationToken = default);
    }

    public interface ISubscriber<TM> : ISubscriber
        where TM : IMessage
    { }
}