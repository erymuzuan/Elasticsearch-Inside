using System;

namespace ElasticsearchInside.CommandLine
{
    public interface IElasticsearchParameters
    {
        IElasticsearchParameters HeapSize(int initialHeapsizeMb = 128, int maximumHeapsizeMb = 128);

        IElasticsearchParameters Port(int port);

        IElasticsearchParameters EnableLogging(bool enable = true);

        IElasticsearchParameters LogTo(Action<string, object[]> logger);

        IElasticsearchParameters AddArgument(string argument);

        IElasticsearchParameters SetNodeName(string node);
        IElasticsearchParameters SetClusterName(string cluster);
        IElasticsearchParameters RootFolder(string rootFolder);

        IElasticsearchParameters OverwriteRootFolder();

    }
}