using System;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace ProtocolLibrary {

    /// <summary>
    /// Пул клиентов и из обработчиков
    /// </summary>
    public static class ClientPool {

        public delegate void ClientPoolChanged(int connectedCount);

        /// <summary>
        /// Ивент изменения пула клиентов
        /// </summary>
        public static event ClientPoolChanged Changed;

        private static ConcurrentDictionary<TcpClient, ClientHandler> _Clients = new ConcurrentDictionary<TcpClient, ClientHandler>();

        /// <summary>
        /// Библиотека клиентов пула
        /// </summary>
        public static ConcurrentDictionary<TcpClient, ClientHandler> Clients {
            get {
                return _Clients;
            }
        }

        /// <summary>
        /// Добавить нового клиента
        /// </summary>
        /// <param name="handler">Обработчик клиента</param>
        /// <returns>True в случае успеха операции</returns>
        public static bool AddHandler(ClientHandler handler) {
            try {
                if (_Clients.TryAdd(handler.Client, handler)) {
                    if (Changed != null) {
                        Changed(_Clients.Count);
                    }
                }
            } catch (Exception e) {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Удалить и отключить указанного клиента
        /// </summary>
        /// <param name="handler">Обработчик клиента</param>
        /// <returns>True в случае успеха операции</returns>
        public static bool RemoveHandler(ClientHandler handler) {
            try {
                ClientHandler removedHandler;
                if (_Clients.TryRemove(handler.Client, out removedHandler)) {
                    handler.Close();
                    if (Changed != null) {
                        Changed(_Clients.Count);
                    }
                }
            } catch (Exception e) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Очистить весь пул клиентов
        /// </summary>
        public static void Clear() {
            foreach (var client in Clients) {
                if (RemoveHandler(client.Value)) {
                    try {
                        client.Key.Close();
                    } catch {
                    }
                    continue;
                }
                throw new Exception("Cannot clear ClientPool");
            }
        }
    }
}