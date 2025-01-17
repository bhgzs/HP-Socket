﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using HPSocketCS;

namespace SSLServerNS
{
    public enum AppState
    {
        Starting, Started, Stoping, Stoped, Error
    }

    public partial class frmServer : Form
    {
        private AppState appState = AppState.Stoped;

        private delegate void ShowMsg(string msg);
        private ShowMsg AddMsgDelegate;

        // 两种构造方式,第一种
        HPSocketCS.SSLServer server = null;
        HPSocketCS.Extra<ClientInfo> extra = new HPSocketCS.Extra<ClientInfo>();
        // 两种构造方式,第二种
        //HPSocketCS.SSLServer server = new HPSocketCS.SSLServer(SSLVerifyMode.Peer | SSLVerifyMode.FailIfNoPeerCert, "ssl-cert\\server.cer", "ssl-cert\\server.key", "123456", "ssl-cert\\ca.crt");
        private string title = "Echo-SSLServer [ 'C' - clear list box ]";
        public frmServer()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                server = new HPSocketCS.SSLServer();
                server.VerifyMode = SSLVerifyMode.Peer | SSLVerifyMode.FailIfNoPeerCert;
                server.CAPemCertFileOrPath = "ssl-cert\\ca.crt";
                server.PemCertFile = "ssl-cert\\server.cer";
                server.PemKeyFile = "ssl-cert\\server.key";
                server.KeyPassword = "123456";

                // 初始化ssl环境
                // 初始化ssl环境
                if (!server.Initialize())
                {
                    SetAppState(AppState.Error);
                    AddMsg("初始化ssl环境失败：" + Sdk.SYS_GetLastError());
                    return;
                }

                this.Text = title;
                // 本机测试没必要改地址,有需求请注释或删除
                this.txtIpAddress.ReadOnly = true;

                // 加个委托显示msg,因为on系列都是在工作线程中调用的,ui不允许直接操作
                AddMsgDelegate = new ShowMsg(AddMsg);


                // 设置服务器事件
                server.OnPrepareListen += new ServerEvent.OnPrepareListenEventHandler(OnPrepareListen);
                server.OnAccept += new ServerEvent.OnAcceptEventHandler(OnAccept);
                server.OnSend += new ServerEvent.OnSendEventHandler(OnSend);
                server.OnReceive += new ServerEvent.OnReceiveEventHandler(OnReceive);
                server.OnClose += new ServerEvent.OnCloseEventHandler(OnClose);
                server.OnShutdown += new ServerEvent.OnShutdownEventHandler(OnShutdown);
                server.OnHandShake += new ServerEvent.OnHandShakeEventHandler(OnHandShake);

                SetAppState(AppState.Stoped);
            }
            catch (Exception ex)
            {
                SetAppState(AppState.Error);
                AddMsg(ex.Message);
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            try
            {
                String ip = this.txtIpAddress.Text.Trim();
                ushort port = ushort.Parse(this.txtPort.Text.Trim());

                // 写在这个位置是上面可能会异常
                SetAppState(AppState.Starting);
                server.IpAddress = ip;
                server.Port = port;
                // 启动服务
                if (server.Start())
                {
                    this.Text = string.Format("{2} - ({0}:{1})", ip, port, title);
                    SetAppState(AppState.Started);
                    AddMsg(string.Format("$Server Start OK -> ({0}:{1})", ip, port));
                }
                else
                {
                    SetAppState(AppState.Stoped);
                    throw new Exception(string.Format("$Server Start Error -> {0}({1})", server.ErrorMessage, server.ErrorCode));
                }
            }
            catch (Exception ex)
            {
                AddMsg(ex.Message);
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            SetAppState(AppState.Stoping);

            // 停止服务
            AddMsg("$Server Stop");
            if (server.Stop())
            {
                this.Text = title;
                SetAppState(AppState.Stoped);
            }
            else
            {
                AddMsg(string.Format("$Stop Error -> {0}({1})", server.ErrorMessage, server.ErrorCode));
            }
        }

        private void btnDisconn_Click(object sender, EventArgs e)
        {
            try
            {
                IntPtr connId = (IntPtr)Convert.ToInt32(this.txtDisConn.Text.Trim());
                // 断开指定客户
                if (server.Disconnect(connId, true))
                {
                    AddMsg(string.Format("$({0}) Disconnect OK", connId));
                }
                else
                {
                    throw new Exception(string.Format("Disconnect({0}) Error", connId));
                }
            }
            catch (Exception ex)
            {
                AddMsg(ex.Message);
            }
        }


        HandleResult OnPrepareListen(IServer sender, IntPtr soListen)
        {
            // 监听事件到达了,一般没什么用吧?

            return HandleResult.Ok;
        }

        HandleResult OnAccept(IServer sender, IntPtr connId, IntPtr pClient)
        {
            // 客户进入了


            // 获取客户端ip和端口
            string ip = string.Empty;
            ushort port = 0;
            if (server.GetRemoteAddress(connId, ref ip, ref port))
            {
                AddMsg(string.Format(" > [{0},OnAccept] -> PASS({1}:{2})", connId, ip.ToString(), port));
            }
            else
            {
                AddMsg(string.Format(" > [{0},OnAccept] -> Server_GetClientAddress() Error", connId));
            }


            // 设置附加数据
            ClientInfo clientInfo = new ClientInfo();
            clientInfo.ConnId = connId;
            clientInfo.IpAddress = ip;
            clientInfo.Port = port;
            if (extra.Set(connId, clientInfo) == false)
            {
                AddMsg(string.Format(" > [{0},OnAccept] -> SetConnectionExtra fail", connId));
            }

            return HandleResult.Ok;
        }

        HandleResult OnSend(IServer sender, IntPtr connId, byte[] bytes)
        {
            // 服务器发数据了


            AddMsg(string.Format(" > [{0},OnSend] -> ({1} bytes)", connId, bytes.Length));

            return HandleResult.Ok;
        }

        HandleResult OnReceive(IServer sender, IntPtr connId, byte[] bytes)
        {
            // 数据到达了
            try
            {
                // 获取附加数据
                ClientInfo clientInfo = extra.Get(connId);
                if (clientInfo != null)
                {
                    // clientInfo 就是accept里传入的附加数据了
                    AddMsg(string.Format(" > [{0},OnReceive] -> {1}:{2} ({3} bytes)", clientInfo.ConnId, clientInfo.IpAddress, clientInfo.Port, bytes.Length));
                }
                else
                {
                    AddMsg(string.Format(" > [{0},OnReceive] -> ({1} bytes)", connId, bytes.Length));
                }

                if (server.Send(connId, bytes, bytes.Length))
                {
                    return HandleResult.Ok;
                }

                return HandleResult.Error;
            }
            catch (Exception)
            {

                return HandleResult.Ignore;
            }
        }

        HandleResult OnClose(IServer sender, IntPtr connId, SocketOperation enOperation, int errorCode)
        {
            if(errorCode == 0)
                AddMsg(string.Format(" > [{0},OnClose]", connId));
            else
                AddMsg(string.Format(" > [{0},OnError] -> OP:{1},CODE:{2}", connId, enOperation, errorCode));

            if (extra.Remove(connId) == false)
            {
                AddMsg(string.Format(" > [{0},OnClose] -> SetConnectionExtra({0}, null) fail", connId));
            }

            return HandleResult.Ok;
        }

        HandleResult OnShutdown(IServer sender)
        {
            // 服务关闭了

            AddMsg(" > [OnShutdown]");
            return HandleResult.Ok;
        }

        HandleResult OnHandShake(IServer sender, IntPtr connId)
        {
            // 握手了
            AddMsg(string.Format(" > [{0},OnHandShake])", connId));

            return HandleResult.Ok;
        }


        /// <summary>
        /// 设置程序状态
        /// </summary>
        /// <param name="state"></param>
        void SetAppState(AppState state)
        {
            appState = state;
            this.btnStart.Enabled = (appState == AppState.Stoped);
            this.btnStop.Enabled = (appState == AppState.Started);
            this.txtIpAddress.Enabled = (appState == AppState.Stoped);
            this.txtPort.Enabled = (appState == AppState.Stoped);
            this.txtDisConn.Enabled = (appState == AppState.Started);
            this.btnDisconn.Enabled = (appState == AppState.Started && this.txtDisConn.Text.Length > 0);
        }

        /// <summary>
        /// 往listbox加一条项目
        /// </summary>
        /// <param name="msg"></param>
        void AddMsg(string msg)
        {
            if (this.lbxMsg.InvokeRequired)
            {
                // 很帅的调自己
                this.lbxMsg.Invoke(AddMsgDelegate, msg);
            }
            else
            {
                if (this.lbxMsg.Items.Count > 100)
                {
                    this.lbxMsg.Items.RemoveAt(0);
                }
                this.lbxMsg.Items.Add(msg);
                this.lbxMsg.TopIndex = this.lbxMsg.Items.Count - (int)(this.lbxMsg.Height / this.lbxMsg.ItemHeight);
            }
        }

        private void txtDisConn_TextChanged(object sender, EventArgs e)
        {
            // CONNID框被改变事件
            this.btnDisconn.Enabled = (appState == AppState.Started && this.txtDisConn.Text.Length > 0);
        }

        private void lbxMsg_KeyPress(object sender, KeyPressEventArgs e)
        {
            // 清理listbox
            if (e.KeyChar == 'c' || e.KeyChar == 'C')
            {
                this.lbxMsg.Items.Clear();
            }
        }

        private void frmServer_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (server != null)
            {
                // 反初始化ssl环境
                server.UnInitialize();

                server.Destroy();
            }
            
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class ClientInfo
    {
        public IntPtr ConnId { get; set; }
        public string IpAddress { get; set; }
        public ushort Port { get; set; }
    }
}
