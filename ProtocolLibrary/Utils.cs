using System;
using System.Net.Sockets;

namespace ProtocolLibrary {

    /// <summary>
    /// Вспомогательный класс
    /// </summary>
    public static class Utils {

        /// <summary>
        /// Стандартный порт протокола передачи файлов
        /// </summary>
        public static int DEFAULT_PROTOCOL_PORT = 5630;

        public static byte[] MAGIC_HEADER = {
            0x47, 0x4F, 0x4C, 0x44, 0x52, 0x45, 0x4E, 0x41, 0x52, 0x44, 0x54, 0x52,
            0x41, 0x4E, 0x53, 0x46, 0x45, 0x52, 0x50, 0x52, 0x4F, 0x54, 0x4F, 0x43,
            0x4F, 0x4C
        };

        /// <summary>
        /// Проверяет активность соединения сокета
        /// </summary>
        /// <returns>True - активно.</returns>
        public static bool IsConnected(Socket socket) {
            if (socket == null) {
                return false;
            }
            try {
                return !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
            } catch (SocketException) {
                return false;
            } catch (ObjectDisposedException) {
                return false;
            }
        }
    }
}