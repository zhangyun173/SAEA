﻿/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPC.Net
*文件名： RClient
*版本号： V3.5.9.1
*唯一标识：6921ced2-8a62-45a7-89c6-84d1301c1a28
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/16 16:16:42
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/16 16:16:42
*修改人： yswenli
*版本号： V3.5.9.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RPC.Common;
using SAEA.RPC.Model;
using SAEA.RPC.Serialize;
using SAEA.Sockets.Core;
using SAEA.Sockets.Interface;
using System;
using System.Threading;

namespace SAEA.RPC.Net
{
    internal class RClient : BaseClientSocket, ISyncBase, IDisposable
    {
        bool _isDisposed = false;

        SyncHelper<byte[]> _syncHelper = new SyncHelper<byte[]>();


        object _syncLocker = new object();

        public object SyncLocker
        {
            get
            {
                return _syncLocker;
            }
        }

        public RClient(Uri uri) : this(100 * 1024, uri.Host, uri.Port)
        {
            if (string.IsNullOrEmpty(uri.Scheme) || string.Compare(uri.Scheme, "rpc", true) != 0)
            {
                ExceptionCollector.Add("Consumer", new RPCSocketException("当前连接协议不正确，请使用格式rpc://ip:port"));
                return;
            }
        }

        public RClient(int bufferSize = 100 * 1024, string ip = "127.0.0.1", int port = 39654) : base(new RContext(), ip, port, bufferSize)
        {

        }

        protected override void OnReceived(byte[] data)
        {
            ((RCoder)UserToken.Unpacker).Unpack(data, msg =>
            {
                switch ((RSocketMsgType)msg.Type)
                {
                    case RSocketMsgType.Ping:
                        break;
                    case RSocketMsgType.Pong:

                        break;
                    case RSocketMsgType.Request:
                        break;
                    case RSocketMsgType.Response:
                        _syncHelper.Set(msg.SequenceNumber, msg.Data);
                        break;
                    case RSocketMsgType.Error:
                        var offset = 0;
                        ExceptionCollector.Add("Consumer", new Exception((string)ParamsSerializeUtil.Deserialize(typeof(string), msg.Data, ref offset)));
                        _syncHelper.Set(msg.SequenceNumber, msg.Data);
                        break;
                    case RSocketMsgType.Close:
                        break;
                }
            });
        }

        /// <summary>
        /// 发送心跳
        /// </summary>
        internal void KeepAlive()
        {
            ThreadHelper.Run(() =>
            {
                while (!_isDisposed)
                {
                    try
                    {
                        if (this.Connected)
                        {
                            if (UserToken.Actived.AddSeconds(60) < DateTimeHelper.Now)
                            {
                                BeginSend(new RSocketMsg(RSocketMsgType.Ping));
                            }
                        }
                        ThreadHelper.Sleep(5 * 100);
                    }
                    catch (Exception ex)
                    {
                        ExceptionCollector.Add("Consumer", ex);
                    }
                }
            }, true, ThreadPriority.Highest);
        }
        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="msg"></param>
        internal void BeginSend(RSocketMsg msg)
        {
            BeginSend(((RCoder)UserToken.Unpacker).Encode(msg));
        }

        /// <summary>
        /// 发送请求
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="method"></param>
        /// <param name="args"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public byte[] Request(string serviceName, string method, byte[] args, int timeOut)
        {
            try
            {
                byte[] result = null;

                var msg = new RSocketMsg(RSocketMsgType.Request, serviceName, method)
                {
                    SequenceNumber = UniqueKeyHelper.Next()
                };

                msg.Data = args;

                if (_syncHelper.Wait(msg.SequenceNumber, () => { this.BeginSend(msg); }, (r) => { result = r; }, timeOut))
                {
                    return result;
                }
                else
                {
                    ExceptionCollector.Add("Consumer", new RPCSocketException($"serviceName:{serviceName}/method:{method} 调用超时！"));
                }
            }
            catch (Exception ex)
            {
                ExceptionCollector.Add("Consumer", new RPCSocketException($"serviceName:{serviceName}/method:{method} 调用出现异常！", ex));
            }
            return null;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public new void Dispose()
        {
            _isDisposed = true;
            this.Disconnect();
            base.Dispose();
        }
    }
}
