//------------------------------------------------------------------------------
//  此代码版权（除特别声明或在XREF结尾的命名空间的代码）归作者本人若汝棋茗所有
//  源代码使用协议遵循本仓库的开源协议及附加协议，若本仓库没有设置，则按MIT开源协议授权
//  CSDN博客：https://blog.csdn.net/qq_40374647
//  哔哩哔哩视频：https://space.bilibili.com/94253567
//  Gitee源代码仓库：https://gitee.com/RRQM_Home
//  Github源代码仓库：https://github.com/RRQM
//  API首页：https://touchsocket.net/
//  交流QQ群：234762506
//  感谢您的下载和使用
//------------------------------------------------------------------------------

using System.Buffers;
using System.Text;
using TouchSocket.Core;
using TouchSocket.Sockets;

namespace TcpServiceReadAsyncConsoleApp;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var service = new TcpService();
        await service.SetupAsync(new TouchSocketConfig()//载入配置
                                                        //.SetListenIPHosts("tcp://127.0.0.1:7789", 7790)//同时监听两个地址
                                                        //.SetListenIPHosts("tcp://172.27.229.102:5019", 5012)//同时监听两个地址
                                                        //.SetListenIPHosts("127.0.0.1:5012")//同时监听两个地址
                                                        //.SetListenIPHosts(new IPHost("127.0.0.1:5019"))
              .SetListenIPHosts(new IPHost("0.0.0.0:5019"))
             //.SetListenIPHosts("tcp://8.130.37.131:5019", 5020)//同时监听两个地址
             //.SetListenIPHosts("tcp://0.0.0.0:5019", 5020)//同时监听两个地址

             .ConfigureContainer(a =>//容器的配置顺序应该在最前面
             {
                 a.AddConsoleLogger();//添加一个控制台日志注入（注意：在maui中控制台日志不可用）
             })
             .ConfigurePlugins(a =>
             {
                 a.UseTcpSessionCheckClear(options =>
                 {
                     options.CheckClearType = CheckClearType.All;
                     options.Tick = TimeSpan.FromSeconds(60);
                     options.OnClose = async (client, e) =>
                     {
                         await client.CloseAsync("超时无数据");
                     };
                 });

                 a.Add<TcpServiceReceiveAsyncPlugin>();
             }));
        await service.StartAsync();//启动

        Console.ReadKey();

    }

    /// <summary>
    /// 以Received异步委托接收数据
    /// </summary>
    private static async Task RunClientForReceived()
    {
        var client = new TcpClient();
        client.Connected = (client, e) => { return EasyTask.CompletedTask; };//成功连接到服务器
        client.Closed = (client, e) => { return EasyTask.CompletedTask; };//从服务器断开连接，当连接不成功时不会触发。
        client.Received = (client, e) =>
        {
            //从服务器收到信息
            var mes = e.Memory.Span.ToString(Encoding.UTF8);
            client.Logger.Info($"客户端接收到信息：{mes}");
            return EasyTask.CompletedTask;
        };

        await client.SetupAsync(new TouchSocketConfig()
                .SetRemoteIPHost(new IPHost("127.0.0.1:5012"))
                .ConfigurePlugins(a =>
                {
                    a.UseReconnection<TcpClient>(options =>
                    {
                        options.PollingInterval = TimeSpan.FromSeconds(1);
                    });
                })
                .ConfigureContainer(a =>
                {
                    a.AddConsoleLogger();//添加一个日志注入
                }));//载入配置
        await client.ConnectAsync();//连接
        client.Logger.Info("客户端成功连接");

        Console.WriteLine("输入任意内容，回车发送");
        while (true)
        {
            await client.SendAsync(Console.ReadLine());
        }
    }

    private static async Task RunClientForReadAsync()
    {
        #region Tcp客户端异步阻塞接收
        var client = new TcpClient();
        await client.ConnectAsync("tcp://127.0.0.1:7789");//连接

        client.Logger.Info("客户端成功连接");

        Console.WriteLine("输入任意内容，回车发送");
        //receiver可以复用，不需要每次接收都新建
        using (var receiver = client.CreateReceiver())
        {
            while (true)
            {
                //发送信息
                await client.SendAsync(Console.ReadLine());

                //设置接收超时
                using (var cts = new CancellationTokenSource(1000 * 60))
                {
                    //receiverResult必须释放
                    using (var receiverResult = await receiver.ReadAsync(cts.Token))
                    {
                        if (receiverResult.IsCompleted)
                        {
                            //断开连接了
                        }

                        //从服务器收到信息。
                        var mes = receiverResult.Memory.Span.ToString(Encoding.UTF8);
                        client.Logger.Info($"客户端接收到信息：{mes}");

                        //如果是适配器信息，则可以直接获取receiverResult.RequestInfo;
                    }
                }

            }
        }
        #endregion

    }
}


  


internal class TcpServiceReceiveAsyncPlugin : PluginBase, ITcpConnectedPlugin
{
    public async Task OnTcpConnected(ITcpSession client, ConnectedEventArgs e)
    {
        if (client is ITcpSessionClient sessionClient)
        {
            //receiver可以复用，不需要每次接收都新建
            using (var receiver = sessionClient.CreateReceiver())
            {
                while (true)
                {
                    //receiverResult每次接收完必须释放
                    using (var receiverResult = await receiver.ReadAsync(CancellationToken.None))
                    {
                        //收到的数据，此处的数据会根据适配器投递不同的数据。
                        var memory = receiverResult.Memory;
                        Console.WriteLine(StringConverter.ConvertToString(memory));
                      
                        var requestInfo = receiverResult.RequestInfo;

                        if (receiverResult.IsCompleted)
                        {
                            //断开连接了
                            Console.WriteLine($"断开信息：{receiverResult.Message}");
                            return;
                        }
                    }
                }
            }
        }

        await e.InvokeNext();
    }
}

public static class StringConverter
{
    // 缓存编码器以提高性能
    private static readonly Encoding UTF8 = Encoding.UTF8;
    private static readonly Encoding ASCII = Encoding.ASCII;

    public static string ConvertToString(
        ReadOnlyMemory<byte> memory,
        Encoding encoding = null,
        bool poolBuffer = false)
    {
        encoding ??= UTF8;

        // 对于大内存，使用 ArrayPool 优化
        int charCount = encoding.GetCharCount(memory.Span);

        if (poolBuffer && charCount > 1024)
        {
            char[] buffer = ArrayPool<char>.Shared.Rent(charCount);
            try
            {
                int actualChars = encoding.GetChars(memory.Span, buffer);
                return new string(buffer, 0, actualChars);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }
        else
        {
            return encoding.GetString(memory.Span);
        }
    }
}
