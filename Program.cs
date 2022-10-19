using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
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
            Transfer t = new Transfer("ws://192.168.111.222:8181", "127.0.0.1", 3389, 60000, 60000);
            //AuthigTransfer t = new AuthigTransfer("192.168.124.26", 8181, "10.0.0.15", 389);
            //进行远程与本地服务端口的连接操作
            t.Connnect();
            Console.WriteLine("按任意键退出！");
            Console.ReadLine();
            //停止远程与本地端口的连接
            t.Stop();
        }
    }

    public class Transfer
    {
        /// <summary>
        /// 本地 HOST 地址
        /// </summary>
        private readonly string _localHost;

        /// <summary>
        /// 本地 HOST 端口
        /// </summary>
        private readonly ushort _localPort;

        /// <summary>
        /// 日志实例
        /// </summary>
        private readonly EventLog _eventLog;

        /// <summary>
        /// 本地 TCP 客户端
        /// </summary>
        private TcpClient _localClient;

        /// <summary>
        /// 远程 Websocket 客户端
        /// </summary>
        private readonly WebSocket _remoteClient;

        /// <summary>
        /// 消息发送线程
        /// </summary>
        private Task _sendThread;

        /// <summary>
        /// 退出标志
        /// </summary>
        private bool _forceClose;

        /// <summary>
        /// 发送超时时间
        /// </summary>
        private readonly int _sendTimeOut;

        /// <summary>
        /// 接收超时时间
        /// </summary>
        private readonly int _receiveTimeOut;

        private CancellationTokenSource _sendToken;

        private Timer _sendTimer;

        private Timer _receiveTimer;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="wsAddress">远程 WebSocket 地址</param>
        /// <param name="localHost">本地主机地址</param>
        /// <param name="localPort">本地主机端口</param>
        /// <param name="receiveTimeOut">接收超时</param>
        /// <param name="sendTimeOut">发送超时</param>
        /// <param name="eventLog">日志实列</param>
        public Transfer(string wsAddress, string localHost, ushort localPort, int sendTimeOut, int receiveTimeOut, EventLog eventLog = null)
        {
            _localHost = localHost;
            _localPort = localPort;
            _eventLog = eventLog;
            _sendTimeOut = sendTimeOut;
            _receiveTimeOut = receiveTimeOut;
            _localClient = new TcpClient();
            _remoteClient = new WebSocket(wsAddress);
            _remoteClient.WaitTime = TimeSpan.FromSeconds(15);
            _sendToken = new CancellationTokenSource();
            _sendThread = new Task(SendData, _sendToken.Token);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            //_eventLog.WriteEntry("未捕获的异常", EventLogEntryType.Error);
            LogException(e.ExceptionObject as Exception, "主线程");
            if (!e.IsTerminating)
                Try2Reconnect(ReConnectInfo.Exception);
        }

        private void TimeOut(object state)
        {
            Try2Reconnect();
        }

        private void ResetTimer()
        {
            if (_sendTimer == null)
                _sendTimer = new Timer(TimeOut, null, _sendTimeOut, Timeout.Infinite);
            else
            {
                _sendTimer.Dispose();
                _sendTimer = null;
            }
        }

        private void ResetReceiveTimer()
        {
            if (_receiveTimer == null)
                _receiveTimer = new Timer(TimeOut, null, _receiveTimeOut, Timeout.Infinite);
            else
            {
                _receiveTimer.Dispose();
                _receiveTimer = new Timer(TimeOut, null, _receiveTimeOut, Timeout.Infinite);
            }

        }

        /// <summary>
        /// 连接远程与本地端口
        /// </summary>
        public void Connnect()
        {
            _remoteClient.OnError += RemoteClientOnOnError;
            _remoteClient.OnOpen += (sender, args) =>
            {
                _eventLog?.WriteEntry("远程 WebSocket 连接已建立！", EventLogEntryType.Information);
                ClearAllTimer();
            };
            _remoteClient.Log.Output += LogRedrection;
            do
            {
                _remoteClient.Connect();
            } while (!_remoteClient.IsAlive);
            _localClient.Connect(_localHost, _localPort);
            _remoteClient.OnClose += RemoteClientOnOnClose;
            if (_localClient.Connected)
            {
                _eventLog?.WriteEntry("本地 TCP 连接已建立！", EventLogEntryType.Information);
                _remoteClient.OnMessage += RemoteClientOnOnMessage;
                _sendThread.Start();
            }
            ResetReceiveTimer();
        }

        private void LogRedrection(LogData log, string data)
        {
            switch (log.Level)
            {
                case LogLevel.Info:
                    _eventLog?.WriteEntry(log.Message, EventLogEntryType.Information);
                    break;
                case LogLevel.Warn:
                    _eventLog?.WriteEntry(log.Message, EventLogEntryType.Warning);
                    break;
                case LogLevel.Error:
                case LogLevel.Fatal:
                    _eventLog?.WriteEntry(log.Message, EventLogEntryType.Error);
                    break;
                default:
                case LogLevel.Trace:
                case LogLevel.Debug:
                    break;
            }
        }

        #region 测试代码

        /// <summary>
        /// 循环检测数据流中是否存在数据，存在则一直发送给远端
        /// </summary>
        private void SendData()
        {
            while (!_sendToken.IsCancellationRequested)
            {
                try
                {
                    NetworkStream stream;
                    if (_localClient.Connected)
                        stream = _localClient.GetStream();
                    else
                    {
                        Thread.Sleep(1);
                        continue;
                    }
                    if (stream.DataAvailable)
                    {
                        int count;
                        do
                        {
                            var buffer = new byte[1024 * 512];
                            count = stream.Read(buffer, 0, buffer.Length);
                            //if (count == 0)
                            //{
                            //    ReconnectLocalClient();
                            //    continue;
                            //}
                            if (count >= 1024 * 512)
                            {
                                if (_remoteClient.IsAlive)
                                {
                                    ResetTimer();
                                    _remoteClient.Send(buffer);
                                    ResetTimer();
                                }
                            }
                            else
                            {
                                byte[] truebytes = buffer.Take(count).ToArray();
                                if (_remoteClient.IsAlive)
                                {
                                    ResetTimer();
                                    _remoteClient.Send(truebytes);
                                    ResetTimer();
                                }
                            }
                            //Console.WriteLine($"Get Data Lenth From AD: {count} - {DateTime.Now}");
                        } while (count >= 1024);
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception e)
                {
                    //_eventLog.WriteEntry("远程 Websocket 连接错误，尝试重连！", EventLogEntryType.Error);
                    LogException(e, "发送线程");
                    Task.Run(() =>
                    {
                        Try2Reconnect(ReConnectInfo.Exception);
                    });
                    return;
                }
            }
        }

        #endregion

        /// <summary>
        /// Websocket 连接错误时的操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoteClientOnOnError(object sender, ErrorEventArgs e)
        {
            LogException(e.Exception, "接收线程");
            Try2Reconnect(ReConnectInfo.Exception);
        }

        /// <summary>
        /// Websocket 连接被关闭时的操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoteClientOnOnClose(object sender, CloseEventArgs e)
        {
            if (_forceClose) return;
            _remoteClient.OnClose -= RemoteClientOnOnClose;
            _remoteClient.OnMessage -= RemoteClientOnOnMessage;
            _sendToken.Cancel();
            do
            {
                Thread.Sleep(1);
            } while (!_sendThread.IsCompleted);
            do
            {
                _remoteClient.Connect();
            } while (!_remoteClient.IsAlive);

            if (_remoteClient.IsAlive)
            {
                ReconnectLocalClient();
                if (!_remoteClient.IsAlive)
                    _remoteClient.Connect();
                _eventLog?.WriteEntry("双向重连成功！", EventLogEntryType.Information);
                _remoteClient.OnClose += RemoteClientOnOnClose;
                _remoteClient.OnMessage += RemoteClientOnOnMessage;
                _sendToken = new CancellationTokenSource();
                _sendThread = new Task(SendData, _sendToken.Token);
                _sendThread.Start();
                ResetReceiveTimer();
            }
        }

        /// <summary>
        /// 重新连接本地端口
        /// </summary>
        private void ReconnectLocalClient()
        {
            lock (_localClient)
            {
                _localClient.Close();
                _localClient = new TcpClient();
                do
                {
                    try
                    {
                        _localClient.Connect(_localHost, _localPort);
                    }
                    catch (Exception ex)
                    {
                        LogException(ex, "接收线程");
                    }
                } while (!_localClient.Connected);
            }
        }

        /// <summary>
        /// Websocket 客户端，消息到达事件响应
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoteClientOnOnMessage(object sender, MessageEventArgs e)
        {
            ResetReceiveTimer();
            if (e.RawData.Length > 0)
            {
                try
                {
                    #region 测试代码

                    //var stream = LocalClient.GetStream();
                    //if (stream.CanWrite)
                    //{
                    //    lock (stream)
                    //    {
                    //        try
                    //        {
                    //            stream.Write(e.RawData, 0, e.RawData.Length);
                    //            Console.WriteLine($"Get Data Lenth From WebSocket: {e.RawData.Length} - {DateTime.Now}");
                    //        }
                    //        catch (Exception exception)
                    //        {
                    //            Console.WriteLine(exception);
                    //            Console.WriteLine("向 Windows Server 写入数据时出现错误，尝试重连！");
                    //            Try2Reconnect();
                    //        }
                    //    }

                    //}

                    #endregion

                    var stream = _localClient.GetStream();
                    if (stream.CanWrite)
                    {
                        WebSocket2Socket(e, stream);
                        //Socket2WebSocket();
                    }
                }
                catch (Exception exception)
                {
                    //_eventLog.WriteEntry("Windows Server 转发时出现错误，尝试重连！", EventLogEntryType.Error);
                    LogException(exception, "接收线程");
                    Try2Reconnect(ReConnectInfo.Exception);
                }
            }
        }

        /// <summary>
        /// 将 TCPClient 接收到的数据转发到 WebSocket 中
        /// </summary>
        private void Socket2WebSocket()
        {
            NetworkStream stream;
            try
            {
                do
                {
                    Thread.Sleep(1);
                    if (_localClient.Connected)
                        stream = _localClient.GetStream();
                    else return;
                } while (!stream.DataAvailable);

                int count;
                do
                {
                    var buffer = new byte[1024];
                    count = stream.Read(buffer, 0, buffer.Length);
                    if (count >= 1024)
                    {
                        if (_remoteClient.IsAlive)
                        {
                            ResetTimer();
                            _remoteClient.Send(buffer);
                            ResetTimer();
                        }
                    }
                    else
                    {
                        byte[] truebytes = buffer.Take(count).ToArray();
                        if (_remoteClient.IsAlive)
                        {
                            ResetTimer();
                            _remoteClient.Send(truebytes);
                            ResetTimer();
                        }
                    }
                } while (count >= 1024);
            }
            catch (Exception exception)
            {
                //_eventLog.WriteEntry("向 WebSocket 写入数据时出现错误，尝试重连！", EventLogEntryType.Error);
                LogException(exception, "接收线程");
                Try2Reconnect(ReConnectInfo.Exception);
            }
        }

        /// <summary>
        /// 将 WebSocket 发送到的数据流转发到 TCPClient 的 Socket 中
        /// </summary>
        /// <param name="e"></param>
        /// <param name="stream"></param>
        private void WebSocket2Socket(MessageEventArgs e, NetworkStream stream)
        {
            try
            {
                stream.Write(e.RawData, 0, e.RawData.Length);
            }
            catch (Exception exception)
            {
                LogException(exception, "接收线程");
                //_eventLog.WriteEntry("向 Windows Server 写入数据时出现错误，尝试重连！", EventLogEntryType.Error);
                Try2Reconnect(ReConnectInfo.Exception);
            }
        }

        /// <summary>
        /// 重新连接所有客户端
        /// </summary>
        private void Try2Reconnect(ReConnectInfo info = ReConnectInfo.TimeOut)
        {
            ClearAllTimer();
            switch (info)
            {
                case ReConnectInfo.TimeOut:
                    _eventLog?.WriteEntry("因网络连接超时，关闭远程 Websocket 连接！", EventLogEntryType.Warning);
                    break;
                case ReConnectInfo.Exception:
                default:
                    _eventLog?.WriteEntry("因程序异常，关闭远程 Websocket 连接！", EventLogEntryType.Warning);
                    break;
            }
            _remoteClient.Close();
        }

        private void ClearAllTimer()
        {
            _sendTimer?.Dispose();
            _receiveTimer?.Dispose();
            _sendTimer = null;
            _receiveTimer = null;
        }

        /// <summary>
        /// 关闭远程与本地客户端
        /// </summary>
        public void Stop()
        {
            _eventLog?.WriteEntry("Authing LDAP Service 关闭所有连接，等待安全退出", EventLogEntryType.Warning);
            _forceClose = true;
            _sendToken.Cancel();
            _remoteClient.CloseAsync();
            _localClient.Close();
            _eventLog?.WriteEntry("Authing LDAP Service 关闭所有连接，结束服务", EventLogEntryType.Information);
        }

        public void LogException(Exception ex, string str)
        {
            StringBuilder errorstr = new StringBuilder();
            errorstr.AppendLine("线程名称：" + str);
            errorstr.AppendLine("当前时间：" + DateTime.Now.ToString(CultureInfo.InvariantCulture));
            errorstr.AppendLine("异常信息：" + ex?.Message);
            errorstr.AppendLine("异常信息：" + ex?.Message);
            errorstr.AppendLine("异常对象：" + ex?.Source);
            errorstr.AppendLine("调用堆栈：\n" + ex?.StackTrace.Trim());
            errorstr.AppendLine("触发方法：" + ex?.TargetSite);
            _eventLog?.WriteEntry(errorstr.ToString(), EventLogEntryType.Error);
        }
    }

    public enum ReConnectInfo
    {
        TimeOut,
        Exception,
    }
}
