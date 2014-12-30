using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ProtocolLibrary;
using ProtocolLibrary.Packets;

namespace FileTransferClient {

    internal class FileViewModel : INotifyPropertyChanged {

        public class FileItemViewModel : INotifyPropertyChanged {
            private static BitmapImage IncomingImage = new BitmapImage(new Uri("incoming.png", UriKind.Relative));
            private static BitmapImage OutcomingImage = new BitmapImage(new Uri("outcoming.png", UriKind.Relative));

            public FileItemViewModel(FileHandler handler, FileHeader header, bool isSending = false) {
                this.FileHandler = handler;
                this.FileHeader = header;
                _Name = FileHeader.Name;
                _Status = Status.WaitForAccept;
                IsSending = isSending;
                MaxPackets = 1;
                CurrentPacket = 0;
            }

            public FileHandler FileHandler {
                get;
                set;
            }

            public FileHeader FileHeader {
                get;
                set;
            }

            public enum Status {
                WaitForAccept,
                Transfer,
                Completed,
                Error,
                Cancelled
            }

            public bool IsSending {
                get;
                set;
            }

            private String _Name;

            private long _MaxPackets;

            private int _CurrentPacket;

            private Status _Status;

            public ImageSource Icon {
                get {
                    return IsSending ? OutcomingImage : IncomingImage;
                }
            }

            public String Name {
                get {
                    return _Name;
                }
                set {
                    _Name = value;
                    NotifyPropertyChanged("Name");
                }
            }

            public long MaxPackets {
                get {
                    return _MaxPackets;
                }
                set {
                    _MaxPackets = value;
                    NotifyPropertyChanged("MaxPackets");
                }
            }

            public int CurrentPacket {
                get {
                    return _CurrentPacket;
                }
                set {
                    _CurrentPacket = value;
                    NotifyPropertyChanged("CurrentPacket");
                }
            }

            public Visibility IsWaitingForAccept {
                get {
                    return _Status == Status.WaitForAccept ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            public Visibility IsTextStatus {
                get {
                    return _Status != Status.WaitForAccept ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            public bool Active {
                get {
                    return _Status == Status.Transfer;
                }
            }

            public string TextStatus {
                get {
                    switch (_Status) {
                        case FileItemViewModel.Status.Completed:
                            return "Завершено";

                        case FileItemViewModel.Status.Transfer:
                            return IsSending ? "Отправка..." : "Получение...";

                        case FileItemViewModel.Status.Cancelled:
                            return "Отменено...";

                        case FileItemViewModel.Status.Error:
                            return "Ошибка";

                        default:
                            return "Неизвестно";
                    }
                }
            }

            public void setStatus(Status status) {
                _Status = status;
                NotifyPropertyChanged("IsWaitingForAccept");
                NotifyPropertyChanged("IsTextStatus");
                NotifyPropertyChanged("TextStatus");
            }

            public Status getStatus() {
                return _Status;
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private void NotifyPropertyChanged(String propertyName) {
                PropertyChangedEventHandler handler = PropertyChanged;
                if (null != handler) {
                    handler(this, new PropertyChangedEventArgs(propertyName));
                }
            }
        }

        public FileItemViewModel FindByGUID(string guid, bool activeOnly = false) {
            try {
                return Items.First(e => e.FileHeader.Guid == guid && (activeOnly ? e.Active : true));
            } catch {
                return null;
            }
        }

        public FileViewModel() {
            this.Items = new ObservableCollection<FileItemViewModel>();
        }

        public ObservableCollection<FileItemViewModel> Items {
            get;
            set;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String propertyName) {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (null != handler) {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}