using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;

namespace Actors.Interfaces
{
    public interface IAggregateLocalWithTimer : IActor
    {
        Task Aggregate(int value, CancellationToken cancellationToken);
    }
}
