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

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TouchSocket.Core;
using TouchSocket.Sockets;

namespace TcpServiceForWebApi.Services;

public class TcpClientPoolHostedService : IHostedService, IAsyncDisposable
{
    private const string RemoteAddress = "tcp://8.130.37.131:41051";
    private readonly List<TcpClient> m_clients = new();
    private readonly ILogger<TcpClientPoolHostedService> m_logger;

    public TcpClientPoolHostedService(ILogger<TcpClientPoolHostedService> logger)
    {
        this.m_logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        //for (var i = 0; i < 10; i++)
        //{
        //    cancellationToken.ThrowIfCancellationRequested();

        //    var client = new TcpClient();
        //    var clientIndex = i + 1;
        //    client.Connected = (c, e) =>
        //    {
        //        this.m_logger.LogInformation("TCP客户端 {ClientId} 已连接至 {Remote}", c.Id, RemoteAddress);
        //        return EasyTask.CompletedTask;
        //    };
        //    client.Closed = (c, e) =>
        //    {
        //        this.m_logger.LogWarning("TCP客户端 {ClientId} 已断开：{Reason}", c.Id, e.Message);
        //        return EasyTask.CompletedTask;
        //    };

        //    try
        //    {
        //        await client.SetupAsync(new TouchSocketConfig()
        //            .SetRemoteIPHost(RemoteAddress)
        //            .ConfigureContainer(a =>
        //            {
        //                a.AddConsoleLogger();
        //            }));

        //        await client.ConnectAsync();
        //        this.m_clients.Add(client);
        //    }
        //    catch (Exception ex)
        //    {
        //        this.m_logger.LogError(ex, "TCP客户端 {Index} 连接 {Remote} 失败", clientIndex, RemoteAddress);
        //        try
        //        {
        //            await client.CloseAsync(ex.Message);
        //        }
        //        catch
        //        {
        //        }
        //        client.Dispose();
        //    }
        //}
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var client in this.m_clients)
        {
            try
            {
                await client.CloseAsync("Host stopping");
            }
            catch (Exception ex)
            {
                this.m_logger.LogDebug(ex, "关闭 TCP 客户端时出错");
            }

            client.Dispose();
        }

        this.m_clients.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in this.m_clients)
        {
            try
            {
                await client.CloseAsync();
            }
            catch
            {
            }

            client.Dispose();
        }

        this.m_clients.Clear();
    }
}
