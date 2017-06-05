using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Actors.Client;
using Actors.Interfaces;
using System.Collections.Immutable;

namespace Actors
{
    [StatePersistence(StatePersistence.Volatile)]
    internal class AggregateWithTimer : Actor, IAggregateWithTimer
    {
        private const string STATE = "Aggregation";
        private ImmutableList<int> _volatileState;

        public AggregateWithTimer(ActorService actorService, ActorId actorId)
            : base(actorService, actorId)
        {
        }

        protected override async Task OnActivateAsync()
        {
            _volatileState = await StateManager.GetStateAsync<ImmutableList<int>>(STATE);
        }

        Task IAggregateWithTimer.Reduce(int[] localAggregation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _volatileState = _volatileState.AddRange(localAggregation);
            return Task.CompletedTask;
        }

    }
}
