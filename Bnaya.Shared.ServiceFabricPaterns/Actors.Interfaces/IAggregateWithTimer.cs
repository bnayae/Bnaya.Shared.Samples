using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;

namespace Actors.Interfaces
{
    public interface IAggregateWithTimer : IActor
    {
        Task Reduce(int[] localAggregation, CancellationToken cancellationToken);
    }
}
