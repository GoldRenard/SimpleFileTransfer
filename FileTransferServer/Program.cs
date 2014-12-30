using System;
using System.Threading;
using log4net.Config;

namespace FileTransferServer {

    internal class Program {
        private static readonly log4net.ILog LOGGER = log4net.LogManager.GetLogger(typeof(Program));

        private static void Main(string[] args) {
            XmlConfigurator.Configure();
            int MaxThreadsCount = Environment.ProcessorCount * 8;
            ThreadPool.SetMaxThreads(MaxThreadsCount, MaxThreadsCount);
            ThreadPool.SetMinThreads(4, 4);
            Server instance = new Server(ProtocolLibrary.Utils.DEFAULT_PROTOCOL_PORT);
            Console.ReadKey();
        }
    }
}