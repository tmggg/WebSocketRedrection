using System;
using System.Linq;
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
            AuthigTransfer t = new AuthigTransfer("192.168.111.244", 8181, "127.0.0.1", 389);
            //AuthigTransfer t = new AuthigTransfer("192.168.124.26", 8181, "10.0.0.15", 389);
            //进行远程与本地服务端口的连接操作
            t.Connnect();
            Console.WriteLine("按任意键退出！");
            Console.ReadLine();
            //停止远程与本地端口的连接
            t.Stop();
        }
    }

    public class AuthigTransfer
    {
        /// <summary>
        ///远程 HOST 地址
        /// </summary>
        private readonly string _remoteHost;
        /// <summary>
        /// 远程 HOST 端口
        /// </summary>
        private readonly ushort _remotePort;
        /// <summary>
        /// 本地 HOST 地址
        /// </summary>
        private readonly string _locaHost;
        /// <summary>
        /// 本地 HOST 端口
        /// </summary>
        private readonly ushort _localPort;
        /// <summary>
        /// 本地 TCP 客户端
        /// </summary>
        private TcpClient LocalClient;
        /// <summary>
        /// 远程 Websocket 客户端
        /// </summary>
        private readonly WebSocket RemoteClient;
        /// <summary>
        /// 消息发送线程
        /// </summary>
        private Thread sendThread;
        /// <summary>
        /// 退出标志
        /// </summary>
        private bool ForceClose;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="remoteHost">远程主机地址</param>
        /// <param name="remotePort">远程主机端口</param>
        /// <param name="locaHost">本地主机地址</param>
        /// <param name="localPort">本地主机端口</param>
        public AuthigTransfer(string remoteHost, ushort remotePort, string locaHost, ushort localPort)
        {
            _remoteHost = remoteHost;
            _remotePort = remotePort;
            _locaHost = locaHost;
            _localPort = localPort;
            LocalClient = new TcpClient();
            RemoteClient = new WebSocket($"ws://{_remoteHost}:{_remotePort}");
            RemoteClient.WaitTime = TimeSpan.FromSeconds(60);
            sendThread = new Thread(SendData);
        }

        /// <summary>
        /// 连接远程与本地端口
        /// </summary>
        public void Connnect()
        {
            RemoteClient.OnMessage += RemoteClientOnOnMessage;
            RemoteClient.OnError += RemoteClientOnOnError;
            RemoteClient.OnOpen += (sender, args) => Console.WriteLine("远程 WebSocket 连接已建立！");
            do
            {
                RemoteClient.Connect();
            } while (!RemoteClient.IsAlive);
            LocalClient.Connect(_locaHost, _localPort);
            RemoteClient.OnClose += RemoteClientOnOnClose;
            if (LocalClient.Connected)
            {
                Console.WriteLine("本地 TCP 连接已建立！");
                sendThread.Start();
            }
        }

        /// <summary>
        /// 循环检测数据流中是否存在数据，存在则一直发送给远端
        /// </summary>
        private void SendData()
        {
            while (true)
            {
                try
                {
                    NetworkStream stream;
                    if (LocalClient.Connected)
                        stream = LocalClient.GetStream();
                    else
                    {
                        Thread.Sleep(1);
                        continue;
                    }
                    //if (stream.DataAvailable)
                    //{
                    //    lock (stream)
                    //    {
                    //        var buffer = new byte[1024];
                    //        var count = stream.Read(buffer, 0, buffer.Length);
                    //        if (count == 0)
                    //        {
                    //            ReconnectLocalClient();
                    //            continue;
                    //        }
                    //    }
                    //}
                    if (stream.DataAvailable)
                    {
                        lock (stream)
                        {
                            int count;
                            do
                            {
                                var buffer = new byte[1024];
                                count = stream.Read(buffer, 0, buffer.Length);
                                //if (count == 0)
                                //{
                                //    ReconnectLocalClient();
                                //    continue;
                                //}
                                if (count >= 1024)
                                {
                                    if (RemoteClient.IsAlive)
                                        RemoteClient.Send(buffer);
                                }
                                else
                                {
                                    byte[] truebytes = buffer.Take(count).ToArray();
                                    if (RemoteClient.IsAlive)
                                        RemoteClient.Send(truebytes);
                                }
                                Console.WriteLine($"Get Data Lenth From AD: {count} - {DateTime.Now}");
                            } while (count >= 1024);
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    Console.WriteLine("远程 Websocket 连接，尝试重连！");
                    Try2Reconnect();
                }
            }
        }

        /// <summary>
        /// Websocket 连接错误时的操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoteClientOnOnError(object sender, ErrorEventArgs e)
        {
            RemoteClient.Close(500, "I Have Error");
        }

        /// <summary>
        /// Websocket 连接被关闭时的操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoteClientOnOnClose(object sender, CloseEventArgs e)
        {
            if (ForceClose) return;
            RemoteClient.OnClose -= RemoteClientOnOnClose;
            do
            {
                RemoteClient.Connect();
            } while (!RemoteClient.IsAlive);

            if (RemoteClient.IsAlive)
            {
                ReconnectLocalClient();
                Console.WriteLine("双向重连成功！");
                RemoteClient.OnClose += RemoteClientOnOnClose;
            }
        }

        /// <summary>
        /// 重新连接本地端口
        /// </summary>
        private void ReconnectLocalClient()
        {
            lock (LocalClient)
            {
                try
                {
                    LocalClient.Close();
                    LocalClient = new TcpClient();
                    LocalClient.Connect(_locaHost, _localPort);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        /// <summary>
        /// Websocket 客户端，消息到达事件响应
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoteClientOnOnMessage(object sender, MessageEventArgs e)
        {
            if (e.RawData.Length > 0)
            {
                try
                {
                    var stream = LocalClient.GetStream();
                    if (stream.CanWrite)
                    {
                        lock (stream)
                        {
                            try
                            {
                                stream.Write(e.RawData, 0, e.RawData.Length);
                                Console.WriteLine($"Get Data Lenth From WebSocket: {e.RawData.Length} - {DateTime.Now}");
                            }
                            catch (Exception exception)
                            {
                                Console.WriteLine(exception);
                                Console.WriteLine("向 Windows Server 写入数据时出现错误，尝试重连！");
                                Try2Reconnect(e.RawData);
                            }
                        }

                    }
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    Console.WriteLine("Windows Server 转发时出现错误，尝试重连！");
                    Try2Reconnect();
                }
            }
        }

        /// <summary>
        /// 重新连接所有客户端
        /// </summary>
        private void Try2Reconnect(byte[] data = null)
        {
            lock (LocalClient)
            {
                //Console.WriteLine("准备重建本地连接");
                //ReconnectLocalClient();
                //if (data != null)
                //{
                //    var stream = LocalClient.GetStream();
                //    if (stream.CanWrite)
                //    {
                //        lock (stream)
                //        {
                //            try
                //            {
                //                stream.Write(data, 0, data.Length);
                //                Console.WriteLine($"Get Data Lenth From WebSocket: {data.Length} - {DateTime.Now}");
                //            }
                //            catch (Exception exception)
                //            {
                //                Console.WriteLine(exception);
                //                Console.WriteLine("本地连接出现错误，尝试重连！");
                //                Try2Reconnect(data);
                //            }
                //        }

                //    }
                //}
                Console.WriteLine("关闭远程 Websocket 连接！");
                RemoteClient.Close();
            }
        }

        /// <summary>
        /// 关闭远程与本地客户端
        /// </summary>
        public void Stop()
        {
            Console.WriteLine("等待安全退出。。。。。！");
            ForceClose = true;
            sendThread.Abort();
            RemoteClient.Close();
            LocalClient.Close();
        }
    }
}
