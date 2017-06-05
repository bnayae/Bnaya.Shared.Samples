using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Actors.Interfaces;
using System.Threading;

namespace EntryPoints.Controllers
{
    [Route("api/[controller]")]
    public class FacadeController : Controller
    {
        // GET api/values
        [HttpGet]
        public async ValueTask<string> Get()
        {
            var id = ActorId.CreateRandom();
            var proxy = ActorProxy.Create<IHelloWorldActor>(id);
            int count = await proxy.GetCountAsync(CancellationToken.None);
            return $"value {count}";
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public ValueTask<string> Get(int id)
        {
            return new ValueTask<string>($"value {id}");
        }

    }
}