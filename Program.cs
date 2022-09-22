using System;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocket = WebSocketSharp.WebSocket;

namespace WebSocketRedrection
{
    class Program
    {
        static async Task Main(string[] args)
        {
            AuthigTransfer t = new AuthigTransfer("127.0.0.1", 8181, "127.0.0.1", 5555);
            t.Connnect();
            Console.WriteLine("按任意键退出！");
            Console.ReadLine();
            t.Stop();
        }
    }

    public class AuthigTransfer
    {
        private readonly string _remoteHost;
        private readonly ushort _remotePort;
        private readonly string _locaHost;
        private readonly ushort _localPort;
        private TcpClient LocalClient;
        private readonly WebSocket RemoteClient;
        private Thread sendThread;

        public AuthigTransfer(string remoteHost, ushort remotePort, string locaHost, ushort localPort)
        {
            _remoteHost = remoteHost;
            _remotePort = remotePort;
            _locaHost = locaHost;
            _localPort = localPort;
            LocalClient = new TcpClient();
            RemoteClient = new WebSocket($"ws://{_remoteHost}:{_remotePort}");
            RemoteClient.WaitTime = TimeSpan.FromSeconds(60);
        }

        public async void Connnect()
        {
            do
            {
                RemoteClient.Connect();
            } while (!RemoteClient.IsAlive);
            LocalClient.Connect(_locaHost, _localPort);
            if (LocalClient.Connected)
                Console.WriteLine("本地连接已建立！");
            RemoteClient.OnMessage += RemoteClientOnOnMessage;
            RemoteClient.OnClose += RemoteClientOnOnClose;
            RemoteClient.OnError += RemoteClientOnOnError;
            RemoteClient.OnOpen += (sender, args) => { Console.WriteLine("远程连接已建立！"); };
            sendThread = new Thread(SendData);
            sendThread.Start();
        }

        private void SendData()
        {
            while (true)
            {
                try
                {
                    var steam = LocalClient.GetStream();
                    if (steam.DataAvailable)
                    {
                        lock (steam)
                        {
                            int count;
                            do
                            {
                                var buffer = new byte[1024];
                                count = steam.Read(buffer, 0, buffer.Length);
                                if (RemoteClient.IsAlive)
                                    RemoteClient.Send(buffer);
                            } while (count == 0);
                        }
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    Console.WriteLine("本地连接丢失，尝试重连！");
                    Try2Reconnect();
                }
            }
        }

        private void RemoteClientOnOnError(object sender, ErrorEventArgs e)
        {
            RemoteClient.CloseAsync(500, "I Have Error");
        }

        private void RemoteClientOnOnClose(object sender, CloseEventArgs e)
        {
            do
            {
                RemoteClient.Connect();
            } while (!RemoteClient.IsAlive);
        }

        private void RemoteClientOnOnMessage(object sender, MessageEventArgs e)
        {
            Console.WriteLine($"Get Data Lenth : {e.RawData.Length}");
            if (e.RawData.Length > 0)
            {
                try
                {
                    var stream = LocalClient.GetStream();
                    if (stream.CanWrite)
                    {
                        lock (stream)
                        {
                            stream.Write(e.RawData, 0, e.RawData.Length);
                        }
                    }
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    Console.WriteLine("本地连接丢失，尝试重连！");
                    Try2Reconnect();
                }
            }
        }

        private void Try2Reconnect()
        {
            lock (LocalClient)
            {
                if (!LocalClient.Connected)
                {
                    LocalClient.Close();
                    LocalClient = new TcpClient();
                    LocalClient.Connect(_locaHost, _localPort);
                    if (LocalClient.Connected)
                        Console.WriteLine("重连成功！");
                }
            }
        }
        public void Stop()
        {
            sendThread.Abort();
            LocalClient.Close();
            RemoteClient.Close();
        }
    }
}
