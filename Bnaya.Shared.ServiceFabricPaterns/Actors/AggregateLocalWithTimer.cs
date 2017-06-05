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
    internal class AggregateLocalWithTimer : Actor, IAggregateLocalWithTimer
    {
        private const string STATE = "Aggregation";
        private ImmutableList<int> _volatileState;

        public AggregateLocalWithTimer(ActorService actorService, ActorId actorId)
            : base(actorService, actorId)
        {
        }

        protected override async Task OnActivateAsync()
        {
            _volatileState = await StateManager.GetStateAsync<ImmutableList<int>>(STATE);
            if(_volatileState == null)
                _volatileState = ImmutableList < int >.Empty;
            this.RegisterTimer(OnTime, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        private async Task OnTime(object state)
        {
            string myId = this.Id.ToString();
            var id = new ActorId(myId.Substring(0, myId.Length));
            var proxy = ActorProxy.Create<IAggregateWithTimer>(id);
            await proxy.Reduce(_volatileState.ToArray(), CancellationToken.None);
        }

        Task IAggregateLocalWithTimer.Aggregate(int value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _volatileState = _volatileState.Add(value);
            return Task.CompletedTask;
        }
    }
}
