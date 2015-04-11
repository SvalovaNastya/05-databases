using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using Client;
using Domain;
using NUnit.Framework;

namespace SimpleStorage.Tests.ReplicationAndConsistency
{
    [TestFixture]
    public class Task2Tests : FuctionalTestBase
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            fullTopologyClient = new SimpleStorageClient(replica1Endpoint, replica2Endpoint, replica3Endpoint);
            using (var httpClient = new HttpClient())
            {
                ActionOnReplica(replica1Endpoint, httpClient, "start");
                ActionOnReplica(replica2Endpoint, httpClient, "start");
                ActionOnReplica(replica3Endpoint, httpClient, "start");
            }
        }

        private readonly string replica1Endpoint = "http://127.0.0.1:16000/";
        private readonly string replica2Endpoint = "http://127.0.0.1:16001/";
        private readonly string replica3Endpoint = "http://127.0.0.1:16002/";
        private SimpleStorageClient fullTopologyClient;

        private void TestReplicaDown(string replica, Action action)
        {
            using (var httpClient = new HttpClient())
            {
                ActionOnReplica(replica, httpClient, "stop");

                for (int i = 0; i < 10; ++i)
                {
                    action();
                }

                ActionOnReplica(replica, httpClient, "start");
            }
        }

        private static void ActionOnReplica(string replica, HttpClient httpClient, string action)
        {
            using (
                HttpResponseMessage response =
                    httpClient.PostAsync(replica + "api/admin/" + action, new ByteArrayContent(new byte[0])).Result)
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        }

        [Test]
        public void Get_ManyTimes_ShouldFaultTolerant()
        {
            string id = Guid.NewGuid().ToString();
            var value = new Value {Content = "content"};
            fullTopologyClient.Put(id, value);

            for (int i = 0; i < 100; ++i)
            {
                fullTopologyClient.Get(id);
            }
            Thread.Sleep(2000);
            TestReplicaDown(replica1Endpoint, () => fullTopologyClient.Get(id));
            TestReplicaDown(replica2Endpoint, () => fullTopologyClient.Get(id));
            TestReplicaDown(replica3Endpoint, () => fullTopologyClient.Get(id));
        }

        [Test]
        public void Put_ManyTimes_ShouldFaultTolerant()
        {
            string id = Guid.NewGuid().ToString();
            var value = new Value { Content = "content" };
            fullTopologyClient.Put(id, value);

            for (int i = 0; i < 100; ++i)
            {
                fullTopologyClient.Get(id);
            }
            Thread.Sleep(2000);
            TestReplicaDown(replica1Endpoint, () => fullTopologyClient.Put(id, value));
            TestReplicaDown(replica2Endpoint, () => fullTopologyClient.Put(id, value));
            TestReplicaDown(replica3Endpoint, () => fullTopologyClient.Put(id, value));
        }
    }
}