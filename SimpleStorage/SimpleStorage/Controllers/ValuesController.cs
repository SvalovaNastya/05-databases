using System;
using System.Linq;
using System.Net;
using System.Web.Http;
using Client;
using Domain;
using SimpleStorage.Infrastructure;

namespace SimpleStorage.Controllers
{
    public class ValuesController : ApiController
    {
        private readonly IConfiguration configuration;
        private readonly IStateRepository stateRepository;
        private readonly IStorage storage;

        public ValuesController(IStorage storage, IStateRepository stateRepository, IConfiguration configuration)
        {
            this.storage = storage;
            this.stateRepository = stateRepository;
            this.configuration = configuration;
        }

        private void CheckState()
        {
            if (stateRepository.GetState() != State.Started)
                throw new HttpResponseException(HttpStatusCode.InternalServerError);
        }

        // GET api/values/5 
        public Value Get(string id)
        {
            var shards = configuration.OtherShardsPorts.Union(new[] { configuration.CurrentNodePort }).OrderBy(p => p).ToArray();
            var quorumCount = (int)(shards.Count() / 2) + 1;
            var answer = storage.Get(id);
            int i = 0;
            while (quorumCount > 0 && i < shards.Count())
            {
                var client = new InternalClient(string.Format("http://127.0.0.1:{0}/", shards[i]));
                try
                {
                    var result = client.Get(id);
                    if (answer == null)
                        answer = result;
                    else
                        if (result != null && result.Revision > answer.Revision)
                            answer = result;
                    quorumCount--;
                }
                catch(HttpResponseException e)
                {
                    if (e.Message.Contains("404"))
                    {
                        quorumCount--;
                    }
                }
                catch (Exception)
                {
                }
                i++;
            }
            if (answer == null)
                throw new HttpResponseException(HttpStatusCode.NotFound);
            CheckState();
            return answer;
        }

        // PUT api/values/5
        public void Put(string id, [FromBody] Value value)
        {
            var shards = configuration.OtherShardsPorts.Union(new[] { configuration.CurrentNodePort }).OrderBy(p => p).ToArray();
            var quorumCount = (int)(shards.Count() / 2) + 1;
            int i = 0;
            while (quorumCount > 0 && i < shards.Count())
            {
                if (shards[i] == configuration.CurrentNodePort)
                {
                    i++;
                    continue;
                }
                var client = new InternalClient(string.Format("http://127.0.0.1:{0}/", shards[i]));
                try
                {
                    client.Put(id, value);
                    quorumCount--;
                }
                catch (Exception)
                {
                }
                i++;
            }
            CheckState();
            storage.Set(id, value);
        }
    }
}