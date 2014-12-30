using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ProtocolLibrary {

    /// <summary>
    /// Асинхронный обработчик клиентов
    /// </summary>
    public class ClientHandler {
        private static readonly log4net.ILog LOGGER = log4net.LogManager.GetLogger(typeof(ClientHandler));
        private TcpClient _Client;

        public delegate void ClientHandlerDisconnect(ClientHandler sender);

        public event ClientHandlerDisconnect Disconnected;

        private EndPoint _RemoteEndPoint;
        private PacketManager _PacketManager;
        private FileHandler _FileHandler;

        private ClientHandler(TcpClient client) {
            if (client == null) {
                Exception e = new ArgumentNullException("Null client? No way...");
                LOGGER.Error(e);
                throw e;
            }
            this._Client = client;
            _PacketManager = new PacketManager(client);
            _RemoteEndPoint = client.Client.RemoteEndPoint;
            _FileHandler = new FileHandler(this);
        }

        /// <summary>
        /// Тело обработчика
        /// </summary>
        private void HandlerBody() {
            while (true) {
                if (!Utils.IsConnected(_Client.Client)) {
                    LOGGER.InfoFormat("Client {0} disconnected", _RemoteEndPoint);
                    ClientPool.RemoveHandler(this);
                    if (Disconnected != null) {
                        Disconnected(this);
                    }
                    break;
                }
                _PacketManager.ProcessPacket();
            }
        }

        /// <summary>
        /// Закрывает соединение с клиентом
        /// </summary>
        public void Close() {
            _Client.Close();
        }

        /// <summary>
        /// Получение и инициализация обработчика для указанного клиента
        /// </summary>
        /// <param name="client">Клиент</param>
        /// <returns>Экземпляр обработчика</returns>
        public static ClientHandler AsyncHandler(TcpClient client) {
            ClientHandler instance = new ClientHandler(client);
            ThreadPool.QueueUserWorkItem(new WaitCallback((o) => {
                ((ClientHandler)o).HandlerBody();
            }), instance);
            ClientPool.AddHandler(instance);
            return instance;
        }

        /// <summary>
        /// Получение связанного с обработчиком клиента
        /// </summary>
        public TcpClient Client {
            get {
                return _Client;
            }
        }

        /// <summary>
        /// Получение связанного с обработчиком менеджера пакетов
        /// </summary>
        public PacketManager PacketManager {
            get {
                return _PacketManager;
            }
        }

        /// <summary>
        /// Получение связанного с обработчиком обработчика передачи файлов
        /// </summary>
        public FileHandler FileHandler {
            get {
                return _FileHandler;
            }
        }
    }
}