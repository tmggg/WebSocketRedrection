using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fleck;

namespace WebsocketServer
{
    class Program
    {
        static IWebSocketConnection _server;
        static TcpListener _client;
        private static bool forceStop;
        private static Thread _receiveThread;
        private static TcpClient client;
        private static CancellationTokenSource token = new CancellationTokenSource();
        static void Main(string[] args)
        {
            var server = new WebSocketServer("ws://127.0.0.1:8181");
            server.Start(socket =>
            {
                _server = socket;
                socket.OnOpen = OnOpen;
                socket.OnClose = OnClose;
                socket.OnMessage = OnMessage;
                socket.OnBinary = OnBinary;
            });
            _client = new TcpListener(IPAddress.Parse("0.0.0.0"), 56565);
            _client.Start();
            _receiveThread = new Thread(ReceiveData);
            _receiveThread.Start();
            Console.WriteLine("按任意键退出");
            Console.ReadLine();
            forceStop = true;
            OnClose();
        }

        private static async void ReceiveData()
        {
            client = await _client.AcceptTcpClientAsync();
            try
            {
                while (client.Connected)
                {
                    if (_server.IsAvailable)
                    {
                        int count;
                        var stream = client.GetStream();
                        lock (stream)
                        {
                            if (stream.DataAvailable)
                            {
                                do
                                {
                                    byte[] buffer = new byte[1024 * 512];
                                    count = stream.Read(buffer, 0, buffer.Length);
                                    if (count >= 1024 * 512)
                                    {
                                        _server.Send(buffer).GetAwaiter().GetResult();
                                    }
                                    else
                                    {
                                        byte[] truebytes = buffer.Take(count).ToArray();
                                        _server.Send(truebytes).GetAwaiter().GetResult();
                                    }
                                    Console.WriteLine($"Receive Data from Client: {DateTime.Now.ToString(CultureInfo.InvariantCulture)} Length: {count}");
                                    //Console.WriteLine(Encoding.UTF8.GetString(buffer));
                                } while (count >= 1024);
                            }
                            else
                            {
                                Thread.Sleep(1);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static async void OnBinary(byte[] obj)
        {
            try
            {
                //Console.WriteLine($"Receive Data Time: {DateTime.Now.ToString()} Length: {obj.Length}");
                if (client.Connected)
                {
                    var stream = client.GetStream();
                    if (stream.CanWrite)
                    {
                        lock (stream)
                        {
                            stream.Write(obj, 0, obj.Length);
                            Console.WriteLine($"Receive Data from Websocket: {DateTime.Now.ToString(CultureInfo.InvariantCulture)} Length: {obj.Length}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static void OnMessage(string obj)
        {
        }

        private static void OnClose()
        {
            Console.WriteLine($"{DateTime.Now} Close WebSocket");
            client?.Close();
            _receiveThread.Abort();
            if (!forceStop)
            {
                _receiveThread = new Thread(ReceiveData);
                _receiveThread.Start();
            }
        }

        private static async void OnOpen()
        {
            Console.WriteLine($"{DateTime.Now} Open WebSocket");
            //try
            //{
            //    await Task.Run(async () =>
            //    {
            //        while (true)
            //        {
            //            if (_server.IsAvailable)
            //            {
            //                await _server.Send(DateTime.Now.ToString(CultureInfo.InvariantCulture));
            //                //await c.Send("{\"code\":200,\"message\":\"获取成功\",\"data\":{\"list\":[{\"assignedAt\":\"2022-08-31T08:33:59.782Z\",\"inheritByChildren\":null,\"enabled\":true,\"policyId\":\"62aadd2607aa79072e9f2bce\",\"code\":\"ApplicationLoginAccess:6IlJxqGXW\",\"policy\":{\"id\":\"62aadd2607aa79072e9f2bce\",\"createdAt\":\"2022-06-16T07:35:02.011Z\",\"updatedAt\":\"2022-06-16T07:35:02.011Z\",\"userPoolId\":\"617280674680a6ca2b1f6317\",\"isDefault\":true,\"isAuto\":false,\"hidden\":true,\"code\":\"ApplicationLoginAccess:6IlJxqGXW\",\"description\":\"允许登录应用 62a99822ff635db21c2ec21c\",\"statements\":[{\"condition\":null,\"id\":\"62aadd265c2b2a7984f9f602\",\"createdAt\":\"2022-06-16T07:35:02.016Z\",\"updatedAt\":\"2022-06-16T07:35:02.016Z\",\"userPoolId\":\"617280674680a6ca2b1f6317\",\"policyId\":\"62aadd2607aa79072e9f2bce\",\"resource\":\"arn:cn:authing:617280674680a6ca2b1f6317:application:62a99822ff635db21c2ec21c\",\"resourceType\":null,\"actions\":[\"application:login\"],\"effect\":\"ALLOW\"}],\"namespaceId\":143233},\"targetNamespace\":\"62a99822ff635db21c2ec21c\",\"targetType\":\"ROLE\",\"targetIdentifier\":\"userList\",\"target\":{\"id\":\"62aae37aa44bbb0427991d33\",\"createdAt\":\"2022-06-16T08:02:02.444Z\",\"updatedAt\":\"2022-06-16T08:07:07.321Z\",\"userPoolId\":\"617280674680a6ca2b1f6317\",\"code\":\"userList\",\"description\":null,\"parentCode\":null,\"isSystem\":false,\"namespaceId\":143233},\"namespace\":\"62a99822ff635db21c2ec21c\",\"namespaceName\":\"testresource\",\"namesapceDes\":\"\"},{\"assignedAt\":\"2022-08-10T06:42:38.198Z\",\"inheritByChildren\":null,\"enabled\":true,\"policyId\":\"62aadd26df836406bf1983b3\",\"code\":\"ApplicationLoginDeny:o5PCprQjWm\",\"policy\":{\"id\":\"62aadd26df836406bf1983b3\",\"createdAt\":\"2022-06-16T07:35:02.028Z\",\"updatedAt\":\"2022-06-16T07:35:02.028Z\",\"userPoolId\":\"617280674680a6ca2b1f6317\",\"isDefault\":true,\"isAuto\":false,\"hidden\":true,\"code\":\"ApplicationLoginDeny:o5PCprQjWm\",\"description\":\"拒绝登录应用 62a99822ff635db21c2ec21c\",\"statements\":[{\"condition\":null,\"id\":\"62aadd26693139f444bc0c60\",\"createdAt\":\"2022-06-16T07:35:02.032Z\",\"updatedAt\":\"2022-06-16T07:35:02.032Z\",\"userPoolId\":\"617280674680a6ca2b1f6317\",\"policyId\":\"62aadd26df836406bf1983b3\",\"resource\":\"arn:cn:authing:617280674680a6ca2b1f6317:application:62a99822ff635db21c2ec21c\",\"resourceType\":null,\"actions\":[\"application:login\"],\"effect\":\"DENY\"}],\"namespaceId\":143233},\"targetType\":\"USER\",\"targetIdentifier\":\"61a82941979c96c04ed9e920\",\"target\":{\"thirdPartyIdentity\":{\"provider\":null,\"refreshToken\":null,\"accessToken\":null,\"scope\":null,\"expiresIn\":null,\"updatedAt\":null},\"id\":\"61a82941979c96c04ed9e920\",\"createdAt\":\"2021-12-02T02:02:41.058Z\",\"updatedAt\":\"2022-02-18T05:43:16.479Z\",\"userPoolId\":\"617280674680a6ca2b1f6317\",\"isRoot\":false,\"status\":\"Activated\",\"statusChangedAt\":null,\"oauth\":null,\"email\":null,\"phone\":\"1800000000\",\"username\":\"user_eab1c811\",\"unionid\":null,\"openid\":null,\"nickname\":null,\"company\":null,\"photo\":\"https://files.authing.co/authing-console/default-user-avatar.png\",\"browser\":null,\"device\":null,\"password\":\"7b74e2f2d2c0c041c5aad9c6b8500aca\",\"passwordLastSetAt\":null,\"salt\":\"3mojp188pcjb\",\"loginsCount\":1,\"lastIp\":\"218.88.125.64\",\"name\":null,\"givenName\":null,\"familyName\":null,\"middleName\":null,\"profile\":null,\"preferredUsername\":null,\"website\":null,\"gender\":\"U\",\"birthdate\":null,\"zoneinfo\":null,\"locale\":null,\"address\":null,\"formatted\":null,\"streetAddress\":null,\"locality\":null,\"region\":null,\"postalCode\":null,\"city\":null,\"province\":null,\"country\":null,\"registerSource\":[\"import:manual\"],\"secretInfo\":null,\"emailVerified\":false,\"phoneVerified\":false,\"lastLogin\":\"2021-12-01T18:05:08.700Z\",\"lastLoginApp\":\"6172807001258f603126a78a\",\"isDeleted\":false,\"sendSmsCount\":0,\"sendSmsLimitCount\":1000,\"encryptedPassword\":\"kxpdzwe1VeSkqFrogIGy2R8VdBa1G1yUIKz2tG7ji8o+Hvl6oI3bCbcysie+OllJBdUixs6BpuQQoHcAKhuEinQlWzi+FY59gZ/NXGR5846AA25TpEsiB7IgjhFo/lVgjL35Ro8fpGrwqLvI7WdTHH8FHiOlPRz2jKWDChXzA4g=\",\"userSourceType\":null,\"userSourceId\":null,\"signedUp\":\"2021-12-02T02:02:41.058Z\",\"externalId\":null,\"mainDepartmentId\":\"62f0dae660cfae9bd667d5e1\",\"mainDepartmentCode\":\"4gRhODYx1ay1CMu6w5I1vgzjuFH5RD\",\"lastMfaTime\":null,\"passwordSecurityLevel\":1,\"resetPasswordOnFirstLogin\":false,\"resetPasswordOnNextLogin\":false,\"resetedPassword\":false,\"syncExtInfo\":null,\"phoneCountryCode\":null,\"lastIP\":\"218.88.125.64\",\"tokenExpiredAt\":\"1970-01-01T00:00:00.000Z\",\"blocked\":false},\"namespace\":\"62a99822ff635db21c2ec21c\",\"namespaceName\":\"testresource\",\"namesapceDes\":\"\"}],\"totalCount\":4}}");
            //            }
            //            await Task.Delay(1000);
            //        }
            //    }, token.Token);
            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine(e.Message);
            //}
        }
    }
}
