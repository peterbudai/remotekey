using System;
using System.ComponentModel;
using System.Configuration;
using System.Media;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace RemoteKey.Server
{
    public class Program : Form
    {
        private CheckBox ctrlBox;
        private CheckBox altBox;
        private CheckBox shiftBox;
        private TextBox keyBox;

        private string HotKey
        {
            get => $"{(this.ctrlBox.Checked ? "^" : "")}{(this.altBox.Checked ? "%" : "")}{(this.shiftBox.Checked ? "+" : "")}{this.keyBox.Text.ToUpper()}";

            set
            {
                this.ctrlBox.Checked = value.Contains('^');
                this.altBox.Checked = value.Contains('%');
                this.shiftBox.Checked = value.Contains('+');
                this.keyBox.Text = value.Trim('^', '%', '+');
            }
        }

        private TextBox authCodeBox;

        private string AuthCode {
            get => this.authCodeBox.Text;
            set {
                this.authCodeBox.Text = value;
            }
        }

        private void OnConfigChange(object sender, EventArgs e) {
            this.SaveConfig();
        }

        private TextBox addressBox;

        private TextBox logBox;

        private void Log(String msg) {
            this.logBox.AppendText("[" + DateTime.Now.ToLongTimeString() + "] " + msg + "\r\n");
        }

        private void InitControls() {
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(400, 450);
            this.Text = $"{Application.ProductName}Server";
            this.Padding = new Padding(5);

            var mainPanel = new TableLayoutPanel();
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.RowCount = 3;
            mainPanel.ColumnCount = 2;
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            this.Controls.Add(mainPanel);

            var hotKeyGroup = new GroupBox();
            hotKeyGroup.Text = "Hotkey to execute";
            hotKeyGroup.Dock = DockStyle.Fill;
            hotKeyGroup.Padding = new Padding(10,2,10,5);
            mainPanel.Controls.Add(hotKeyGroup);
            mainPanel.SetColumnSpan(hotKeyGroup, 2);

            var hotKeyPanel = new TableLayoutPanel();
            hotKeyPanel.RowCount = 1;
            hotKeyPanel.ColumnCount = 4;
            hotKeyPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            hotKeyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
            hotKeyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
            hotKeyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
            hotKeyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            hotKeyPanel.Dock = DockStyle.Fill;
            hotKeyGroup.Controls.Add(hotKeyPanel);

            this.ctrlBox = new CheckBox();
            this.ctrlBox.Text = "Ctrl";
            hotKeyPanel.Controls.Add(this.ctrlBox);
            this.altBox = new CheckBox();
            this.altBox.Text = "Alt";
            hotKeyPanel.Controls.Add(this.altBox);
            this.shiftBox = new CheckBox();
            this.shiftBox.Text = "Shift";
            hotKeyPanel.Controls.Add(this.shiftBox);
            this.keyBox = new TextBox();
            this.keyBox.Dock = DockStyle.Fill;
            hotKeyPanel.Controls.Add(this.keyBox);

            var authCodeGroup = new GroupBox();
            authCodeGroup.Text = "Auth code";
            authCodeGroup.Dock = DockStyle.Fill;
            authCodeGroup.Padding = new Padding(10,5,10,5);
            mainPanel.Controls.Add(authCodeGroup);

            this.authCodeBox = new TextBox();
            this.authCodeBox.Dock = DockStyle.Fill;
            authCodeGroup.Controls.Add(this.authCodeBox);

            var addressGroup = new GroupBox();
            addressGroup.Text = "Connection address";
            addressGroup.Dock = DockStyle.Fill;
            addressGroup.Padding = new Padding(10,5,10,5);
            mainPanel.Controls.Add(addressGroup);

            this.addressBox = new TextBox();
            this.addressBox.Dock = DockStyle.Fill;
            this.addressBox.ReadOnly = true;
            addressGroup.Controls.Add(this.addressBox);

            this.logBox = new TextBox();
            this.logBox.Multiline = true;
            this.logBox.ReadOnly = true;
            this.logBox.Dock = DockStyle.Fill;
            mainPanel.Controls.Add(this.logBox);
            mainPanel.SetColumnSpan(this.logBox, 2);
        }

        private static readonly string HotKeyConfigKey = "HotKey";
        private static readonly string AuthCodeConfigKey = "AuthCode";

        private void LoadConfig() {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            bool changed = false;

            var hotKeyConfig = config.AppSettings.Settings[HotKeyConfigKey];
            if(hotKeyConfig == null) {
                this.HotKey = "^%+A";
                changed = true;
            } else {
                this.HotKey = hotKeyConfig.Value;
            }

            var passwordConfig = config.AppSettings.Settings[AuthCodeConfigKey];
            if(passwordConfig == null) {
                using(var rng = new RNGCryptoServiceProvider()) {
                    var passwordBytes = new byte[8];
                    rng.GetBytes(passwordBytes);
                    this.AuthCode = Convert.ToBase64String(passwordBytes);
                }
                changed = true;
            } else {
                this.AuthCode = passwordConfig.Value;
            }

            this.Log("Configuration loaded");

            if(changed) {
                SaveConfig();
            }
        }

        private void SaveConfig() {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings.Remove(HotKeyConfigKey);
            config.AppSettings.Settings.Add(new KeyValueConfigurationElement(HotKeyConfigKey, this.HotKey));
            config.AppSettings.Settings.Remove(AuthCodeConfigKey);
            config.AppSettings.Settings.Add(new KeyValueConfigurationElement(AuthCodeConfigKey, this.AuthCode));
            config.Save();

            this.Log("Configuration saved");
        }

        private readonly IPEndPoint listenAddress = new IPEndPoint(IPAddress.Any, 6883);

        private Socket listenSocket;

        private BackgroundWorker listenWorker;

        private void DoObtainLocalAddress(object sender, DoWorkEventArgs e) {
            this.listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.listenSocket.Connect("8.8.8.8", 65530);
            e.Result = ((this.listenSocket.LocalEndPoint as IPEndPoint) ?? this.listenAddress).Address;
            this.listenSocket.Dispose();
        }

        private void OnLocalAddressObtained(object sender, RunWorkerCompletedEventArgs e) {
            var address = new IPEndPoint((IPAddress)e.Result, this.listenAddress.Port).ToString();
            this.addressBox.Text = address;
            this.Log($"Listening on {address}");
            
            this.listenWorker.DoWork -= this.DoObtainLocalAddress;
            this.listenWorker.DoWork += this.DoReceiveMessages;
            this.listenWorker.RunWorkerCompleted -= this.OnLocalAddressObtained;
            this.listenWorker.ProgressChanged += this.OnMessageReceived;
            this.listenWorker.WorkerSupportsCancellation = true;
            this.listenWorker.WorkerReportsProgress = true;
            this.listenWorker.RunWorkerAsync();
        }

        private class Message {
            public string Text;
            public IPAddress Address;
        }

        private void DoReceiveMessages(object sender, DoWorkEventArgs e) {
            var worker = (BackgroundWorker)sender;
            var buffer = new byte[65536];
            var addr = (EndPoint)new IPEndPoint(IPAddress.Any, 0);

            this.listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.listenSocket.Bind(this.listenAddress);
            while(!worker.CancellationPending) {
                var len = this.listenSocket.ReceiveFrom(buffer, SocketFlags.None, ref addr);
                worker.ReportProgress(0, new Message { 
                    Text = Encoding.UTF8.GetString(buffer, 0, len), 
                    Address = ((IPEndPoint)addr).Address
                });
            }
        }

        private void OnMessageReceived(object sender, ProgressChangedEventArgs e) {
            var message = (Message)e.UserState;
            if(message.Text.Equals(AuthCode)) {
                SendKeys.Send(this.HotKey);
                SystemSounds.Beep.Play();
                this.Log($"Hotkey request from {message.Address}, sending {this.HotKey}");
            } else {
                this.Log($"Invalid command from {message.Address}");
            }
        }

        public Program()
        {
            this.InitControls();
            this.LoadConfig();

            this.ctrlBox.CheckedChanged += this.OnConfigChange;
            this.altBox.CheckedChanged += this.OnConfigChange;
            this.shiftBox.CheckedChanged += this.OnConfigChange;
            this.keyBox.TextChanged += this.OnConfigChange;
            this.authCodeBox.TextChanged += this.OnConfigChange;

            this.listenWorker = new BackgroundWorker();
            this.listenWorker.DoWork += this.DoObtainLocalAddress;
            this.listenWorker.RunWorkerCompleted += this.OnLocalAddressObtained;
            this.listenWorker.RunWorkerAsync();
        }

        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Program());
        }
    }
}
