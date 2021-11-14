using OpenSleigh.Core.Messaging;
using System;

namespace OpenSleigh.Samples.Sample11.Common.Messages
{
    public record StartSimpleSaga(Guid Id, Guid CorrelationId) : ICommand { }
}
