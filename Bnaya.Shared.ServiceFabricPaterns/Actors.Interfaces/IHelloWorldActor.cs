using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;

namespace Actors.Interfaces
{
    public interface IHelloWorldActor : IActor
    {
        Task<int> GetCountAsync(CancellationToken cancellationToken);

        Task SetCountAsync(int count, CancellationToken cancellationToken);
    }
}
