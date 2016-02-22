using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ElasticsearchInside.CommandLine;
using ElasticsearchInside.Executables;
using ElasticsearchInside.Utilities.Archive;
using LZ4PCL;

namespace ElasticsearchInside
{
    public class Elasticsearch : IDisposable
    {
        private Process _elasticSearchProcess;
        private bool _disposed;
        private readonly DirectoryInfo _rootFolder;
        private DirectoryInfo ElasticsearchHome { get; set; }
        private DirectoryInfo JavaHome { get; set; }
        private readonly ElasticsearchParameters _parameters = new ElasticsearchParameters();
        private readonly CommandLineBuilder _commandLineBuilder = new CommandLineBuilder();
        private readonly Stopwatch _startup;


        static Elasticsearch()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                using (var memStream = new MemoryStream())
                {
                    using (var stream = typeof(Elasticsearch).Assembly.GetManifestResourceStream(typeof(RessourceTarget), "LZ4PCL.dll"))
                        stream?.CopyTo(memStream);

                    return Assembly.Load(memStream.GetBuffer());
                }
            };
        }

        public Uri Url
        {
            get
            {
                if (!_parameters.ElasticsearchPort.HasValue)
                    throw new ApplicationException("Expected HttpPort to be set");

                return new UriBuilder
                {
                    Scheme = Uri.UriSchemeHttp,
                    Host = _parameters.NetworkHost,
                    Port = _parameters.ElasticsearchPort.Value
                }.Uri;
            }
        }

        private void Info(string format, params object[] args)
        {
            if (!_parameters.LoggingEnabled) return;
            if (args == null || args.Length == 0)
                _parameters.Logger("{0}", new object[] { format });
            else
                _parameters.Logger(format, args);
        }


        public Elasticsearch(Func<IElasticsearchParameters, IElasticsearchParameters> configurationAction = null)
        {
            configurationAction?.Invoke(_parameters);
            var rootFolder = _parameters.ElasticsearchRootFolder ?? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            _rootFolder = new DirectoryInfo(rootFolder);
            _startup = Stopwatch.StartNew();

            if (_rootFolder.Exists && _parameters.OverwriteElasticsearchRootFolder)
            {
                _rootFolder.Delete(true);
            }

            SetupEnvironment();

            Info("Environment ready after {0} seconds", _startup.Elapsed.TotalSeconds);

            StartProcess();
            WaitForGreen();
        }

        private void SetupEnvironment()
        {
            _parameters.EsHomePath = _rootFolder;
            JavaHome = new DirectoryInfo(Path.Combine(_rootFolder.FullName, "jre"));
            ElasticsearchHome = new DirectoryInfo(Path.Combine(_rootFolder.FullName, "es"));

            if (_rootFolder.Exists) return;
            var jreTask = Task.Run(() => ExtractEmbeddedLz4Stream("jre.lz4", JavaHome));
            var esTask = Task.Run(() => ExtractEmbeddedLz4Stream("elasticsearch.lz4", ElasticsearchHome));

            Task.WaitAll(jreTask, esTask);
        }


        private void WaitForGreen()
        {
            var statusUrl = new UriBuilder(Url)
            {
                Path = "_cluster/health",
                Query = "wait_for_status=yellow"
            }.Uri;

            var statusCode = (HttpStatusCode)0;
            do
            {
                try
                {
                    var request = WebRequest.Create(statusUrl);
                    using (var response = (HttpWebResponse)request.GetResponse())
                        statusCode = response.StatusCode;
                }
                catch (WebException)
                {
                }

                Thread.Sleep(100);

            } while (statusCode != HttpStatusCode.OK);

            _startup.Stop();
            Info("Started in {0} seconds", _startup.Elapsed.TotalSeconds);
        }

        private void StartProcess()
        {
            var processStartInfo = new ProcessStartInfo($@"""{Path.Combine(JavaHome.FullName, "bin/java.exe")}""")
            {
                UseShellExecute = false,
                Arguments = _commandLineBuilder.Build(_parameters),
                WindowStyle = ProcessWindowStyle.Maximized,
                CreateNoWindow = true,
                LoadUserProfile = false,
                WorkingDirectory = ElasticsearchHome.FullName,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.ASCII,
            };

            _elasticSearchProcess = Process.Start(processStartInfo);
            if (null == _elasticSearchProcess) throw new Exception("Fail to start elasticsearch");

            _elasticSearchProcess.ErrorDataReceived += (sender, eventargs) => Info(eventargs.Data);
            _elasticSearchProcess.OutputDataReceived += (sender, eventargs) => Info(eventargs.Data);
            _elasticSearchProcess.BeginOutputReadLine();
        }

        private void ExtractEmbeddedLz4Stream(string name, DirectoryInfo destination)
        {
            var started = Stopwatch.StartNew();

            using (var stream = GetType().Assembly.GetManifestResourceStream(typeof(RessourceTarget), name))
            using (var decompresStream = new LZ4Stream(stream, CompressionMode.Decompress))
            using (var archiveReader = new ArchiveReader(decompresStream))
                archiveReader.ExtractToDirectory(destination);

            Info("Extracted {0} in {1} seconds", name, started.Elapsed.TotalSeconds);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            try
            {
                _elasticSearchProcess.Kill();
                _elasticSearchProcess.WaitForExit();

            }
            catch (Exception ex)
            {
                Info(ex.ToString());
            }
            _disposed = true;

        }

        ~Elasticsearch()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
