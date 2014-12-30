using System;
using System.IO;
using System.Net;
using System.Windows;
using ProtocolLibrary;
using ProtocolLibrary.Packets;

namespace FileTransferClient {

    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private int serverPort = ProtocolLibrary.Utils.DEFAULT_PROTOCOL_PORT;
        private IPAddress serverHost;
        private ClientLogic clientLogic;

        private FileViewModel Model = new FileViewModel();

        private System.Windows.Forms.OpenFileDialog filePicker = new System.Windows.Forms.OpenFileDialog();

        public MainWindow() {
            InitializeComponent();
            FileList.DataContext = Model;
        }

        private void parseAddress() {
            serverPort = ProtocolLibrary.Utils.DEFAULT_PROTOCOL_PORT;
            String[] parts = ServerAddress.Text.Split(new char[] { ':' });
            switch (parts.Length) {
                case 1:
                case 2:
                    serverHost = IPAddress.Parse(parts[0]);
                    if (parts.Length == 2) {
                        serverPort = Convert.ToInt32(parts[1]);
                    }
                    break;

                default:
                    throw new ArgumentException();
            }
        }

        private void ConnectBtn_Click(object sender, RoutedEventArgs e) {
            bool connected = clientLogic != null;
            if (connected) {
                connected = clientLogic.IsConnected;
            }
            if (!connected) {
                parseAddress();
                clientLogic = new ClientLogic(serverHost, serverPort);
                clientLogic.Disconnected += clientLogic_Disconnected;
                clientLogic.StatusChanged += clientLogic_StatusChanged;
                clientLogic.ReceiveDone += clientLogic_TransferDone;
                clientLogic.SendDone += clientLogic_TransferDone;
                clientLogic.SendRequest += clientLogic_SendRequest;
                clientLogic.SendReady += clientLogic_SendReady;
                clientLogic.Connected += (s, m) => {
                    switchStatus(true);
                };
            } else {
                clientLogic.Disconnect();
            }
        }

        private void clientLogic_SendReady(ClientLogic sender, FileEntry entry) {
            if (!CheckAccess()) {
                ServerAddress.Dispatcher.BeginInvoke(new ClientLogic.DoneEvent((s, e) => {
                    clientLogic_SendReady(s, e);
                }), sender, entry);
                return;
            }
            var item = new FileViewModel.FileItemViewModel(clientLogic.Handler.FileHandler, entry.Header, true);
            item.setStatus(FileViewModel.FileItemViewModel.Status.Transfer);
            Model.Items.Add(item);
        }

        private void clientLogic_SendRequest(ProtocolLibrary.FileHandler sender, ProtocolLibrary.Packets.FileHeader header) {
            if (!CheckAccess()) {
                ServerAddress.Dispatcher.BeginInvoke(new FileHandler.FileRequestEvent((s, h) => {
                    clientLogic_SendRequest(s, h);
                }), sender, header);
                return;
            }
            Model.Items.Add(new FileViewModel.FileItemViewModel(sender, header));
        }

        private void clientLogic_TransferDone(ClientLogic sender, ProtocolLibrary.FileEntry entry) {
            if (!CheckAccess()) {
                ServerAddress.Dispatcher.BeginInvoke(new ClientLogic.DoneEvent((s, e) => {
                    clientLogic_TransferDone(s, e);
                }), sender, entry);
                return;
            }

            FileViewModel.FileItemViewModel item = Model.FindByGUID(entry.Header.Guid, true);
            if (item != null) {
                item.setStatus(FileViewModel.FileItemViewModel.Status.Completed);
                item.CurrentPacket = 1;
                item.MaxPackets = 1;
            }
        }

        private void clientLogic_StatusChanged(ProtocolLibrary.FileHandler sender, System.Net.Sockets.TcpClient client, ProtocolLibrary.FileEntry entry, int packetNumber) {
            if (!CheckAccess()) {
                ServerAddress.Dispatcher.BeginInvoke(new ProtocolLibrary.FileHandler.FileTransferStatusChangedEvent((s, c, e, p) => {
                    clientLogic_StatusChanged(s, c, e, p);
                }), sender, client, entry, packetNumber);
                return;
            }
            FileViewModel.FileItemViewModel item = Model.FindByGUID(entry.Header.Guid, true);
            if (item != null) {
                item.MaxPackets = entry.PacketsCount;
                item.CurrentPacket = packetNumber;
            }
        }

        private void clientLogic_Disconnected(ClientLogic sender, string message) {
            switchStatus(false);
            foreach (FileViewModel.FileItemViewModel item in Model.Items) {
                if (item.Active) {
                    item.setStatus(FileViewModel.FileItemViewModel.Status.Error);
                }
            }
            if (message != null) {
                MessageBox.Show(message);
            }
        }

        private void switchStatus(bool connected) {
            if (!CheckAccess()) {
                ServerAddress.Dispatcher.BeginInvoke(new Func<bool, bool>((e) => {
                    switchStatus(e);
                    return true;
                }), connected);
                return;
            }
            SendBtn.IsEnabled = connected;
            ServerAddress.IsReadOnly = connected;
            ConnectBtn.Content = connected ? "Disconnect" : "Connect";
        }

        private void SendFile_Click(object sender, RoutedEventArgs e) {
            if (filePicker.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                clientLogic.SendFile(filePicker.FileName);
            }
        }

        private void Accept_Click(object sender, RoutedEventArgs e) {
            FileViewModel.FileItemViewModel item = ((FrameworkElement)sender).DataContext as FileViewModel.FileItemViewModel;
            FileHandler handler = item.FileHandler;
            FileHeader header = item.FileHeader;

            if (MessageBox.Show(String.Format("Сервер запрашивает разрешение на прием файла \"{0}\". Вы действительно хотите сохранить этот файл?", header.Name),
                "Передача файла", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes) {
                SaveDialog saveDialog;
                while (true) {
                    saveDialog = new SaveDialog(header.Name);
                    if (saveDialog.Invoke() == System.Windows.Forms.DialogResult.OK) {
                        break;
                    }
                }
                if (File.Exists(saveDialog.InvokeDialog.FileName)) {
                    try {
                        File.Delete(saveDialog.InvokeDialog.FileName);
                    } catch (Exception ex) {
                        item.setStatus(FileViewModel.FileItemViewModel.Status.Error);
                        handler.AcceptFileRequest(false, header);
                        return;
                    }
                }
                handler.AcceptFileRequest(true, header, saveDialog.InvokeDialog.FileName);
                item.setStatus(FileViewModel.FileItemViewModel.Status.Transfer);
            } else {
                handler.AcceptFileRequest(false, header);
                item.setStatus(FileViewModel.FileItemViewModel.Status.Cancelled);
            }
        }
    }
}