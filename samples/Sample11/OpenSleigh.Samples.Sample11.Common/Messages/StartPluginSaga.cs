using System;
using OpenSleigh.Core.Messaging;

namespace OpenSleigh.Samples.Sample11.Common.Messages
{
    public record StartPluginSaga(Guid Id, Guid CorrelationId) : ICommand { }
}
