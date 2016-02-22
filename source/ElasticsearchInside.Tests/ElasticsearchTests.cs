using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Nest;
using NUnit.Framework;

namespace ElasticsearchInside.Tests
{
    [TestFixture]
    public class ElasticsearchTests
    {
        [Test]
        public void Can_start_with_persistent()
        {
            using (var elasticsearch = new Elasticsearch(c => c.RootFolder(@"c:\\temp\\es")))
            {
                ////Arrange
                var client = new ElasticClient(new ConnectionSettings(elasticsearch.Url));

                ////Act
                var result = client.Ping();

                ////Assert
                Assert.That(result.IsValid);
            }
        }
        [Test]
        public void Can_start()
        {
            using (var elasticsearch = new Elasticsearch())
            {
                ////Arrange
                var client = new ElasticClient(new ConnectionSettings(elasticsearch.Url));

                ////Act
                var result = client.Ping();

                ////Assert
                Assert.That(result.IsValid);
            }
        }


        [Test]
        public void Can_insert_data()
        {
            using (var elasticsearch = new Elasticsearch())
            {
                ////Arrange
                var client = new ElasticClient(new ConnectionSettings(elasticsearch.Url));

                ////Act
                client.Index(new { id = "tester" }, i => i.Index("test-index").Type("test-type"));

                ////Assert
                var result = client.Get<dynamic>("tester", "test-index", "test-type");
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Found);
            }
        }
        [Test]
        public async Task Can_insert_data_with_persistent()
        {
            var nodeName = @"data_with_persistent_" + DateTime.Now.Millisecond;
            var clusterName = @"cluster_data_with_persistent_" + DateTime.Now.Millisecond; ;
            const string FOLDER = @"c:\\temp\es004";
            const string INDEX = "test-index";
            const string TYPE = "test-type";
            const int PORT = 9205;
            var id = Guid.NewGuid().ToString();

            if (Directory.Exists(FOLDER)) Directory.Delete(FOLDER, true);

            using (var elasticsearch = new Elasticsearch(c => c
                .SetNodeName(nodeName)
                .SetClusterName(clusterName)
                .Port(PORT)
                .RootFolder(FOLDER)
                .OverwriteRootFolder()
                ))
            {
                ////Arrange
                var client = new ElasticClient(new ConnectionSettings(elasticsearch.Url));

                ////Act
                client.Index(new { id }, i => i.Index(INDEX).Type(TYPE));

                ////Assert
                var result = client.Get<dynamic>(id, INDEX, TYPE);
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Found);
            }
            Console.WriteLine("Shutting down the 1st instance");
            var count = 1;
            while (count > 0)
            {
                Console.Write(".");
                count = Process.GetProcessesByName("java").Length;
                await Task.Delay(500);
            }
            Console.WriteLine();


            using (var elasticsearch = new Elasticsearch(c => c
                .Port(PORT)
                .RootFolder(FOLDER)
                .SetNodeName(nodeName)
                .SetClusterName(clusterName)
                ))
            {

                Console.WriteLine("2nd instance started");
                ////Arrange
                var client = new ElasticClient(new ConnectionSettings(elasticsearch.Url));

                ////Assert
                var result = client.Get<dynamic>(id, INDEX, TYPE);
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Found);
            }
        }

        [Test]
        public void Can_change_configuration()
        {
            using (var elasticsearch = new Elasticsearch(c => c.Port(444).EnableLogging()))
            {
                ////Arrange
                var client = new ElasticClient(new ConnectionSettings(elasticsearch.Url));

                ////Act
                var result = client.Ping();

                ////Assert
                Assert.That(result.IsValid);
                Assert.That(elasticsearch.Url.Port, Is.EqualTo(444));
            }
        }

        [Test]
        public void Can_log_output()
        {
            var logged = false;
            using (new Elasticsearch(c => c.EnableLogging().LogTo((f, a) => logged = true)))
            {
                ////Assert
                Assert.That(logged);
            }
        }
    }
}
