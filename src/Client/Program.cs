using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using ICSharpCode.SharpZipLib.Checksums;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;

namespace Client
{
	using Properties;

	static class Global
	{
		public static String ServiceUrl = "";
		public static String ServerVersion = "";
		public static String ResPath = "";
		public static String AppPath = "";
		public static String ResUrl = "";

		public static ContextMenu TrayIconMenu = null;
		public static NotifyIcon TrayIcon = null;
		public static AppWnd Desktop = null;

		public static string ChatWith = "";
		public static bool CreateTempAccount = false;
		public static int EmbedCode = 0;
		public static Dictionary<String, String> CmdLineParams;
	}

	static class Program
	{
		/// <summary>
		/// 应用程序的主入口点。
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			Global.CmdLineParams = GetCmdLineParams(args);

			Global.ChatWith = Global.CmdLineParams.ContainsKey("chatwith") ? Global.CmdLineParams["chatwith"] : "";
			Global.CreateTempAccount = Global.CmdLineParams.ContainsKey("createaccount");
			Global.EmbedCode = Global.CmdLineParams.ContainsKey("embedcode") ? Int32.Parse(Global.CmdLineParams["embedcode"]) : 0;

			SettingConf.Instance.Load();
			Global.ServiceUrl = SettingConf.Instance.ServiceUrl;
			Global.ResPath = SettingConf.Instance.ResPath;
			Global.AppPath = SettingConf.Instance.AppPath;
			Global.ResUrl = Global.ServiceUrl + Global.AppPath;
			if (!Global.ResUrl.EndsWith("/")) Global.ResUrl += "/";
			Global.ResUrl += Global.ResPath;
			
#			if !DEBUG
			if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Native.dll")))
			{
				LoadPackages(SettingConf.Instance.ServiceUrl);
			}
#			endif

			bool isUpdate = false;

			try
			{
				isUpdate = CheckUpdate();
			}
			catch (Exception ex)
			{
				MessageBox.Show(String.Format("检测更新时发生错误：{0}", ex.Message), "错误");
				return;
			}

			if (!isUpdate)
			{
				AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
				Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);

				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);

				Global.TrayIconMenu = new ContextMenu();
				MenuItem menuExitItem = new MenuItem();
				menuExitItem.Text = "退出";
				menuExitItem.Click += new EventHandler(menuExitItem_Click);
				Global.TrayIconMenu.MenuItems.AddRange(new MenuItem[] { menuExitItem });

				Global.TrayIcon = new NotifyIcon();
				Global.TrayIcon.Icon = Properties.Resources.TrayGred;
				Global.TrayIcon.Visible = false;
				Global.TrayIcon.ContextMenu = Global.TrayIconMenu;
				Global.TrayIcon.Text = "";
				Global.TrayIcon.MouseClick += new MouseEventHandler(TrayIcon_Click);
				Global.TrayIcon.MouseMove += new MouseEventHandler(TrayIcon_MouseMove);

				Global.Desktop = new AppWnd();
				Global.Desktop.FormClosed += new FormClosedEventHandler(Desktop_FormClosed);
				Global.Desktop.ShowInTaskbar = false;
				Global.Desktop.PageLoad += new AppWnd.PageLoadDelegate(Desktop_PageLoad);
				Global.Desktop.HandleCreated += new EventHandler(Desktop_HandleCreated);
				Global.Desktop.HandleDestroyed += new EventHandler(Desktop_HandleDestroyed);
                //MessageBox.Show(Global.CmdLineParams.Count.ToString());
                if (Global.CmdLineParams.Count == 1 && Global.CmdLineParams.ContainsKey("token"))
                {
                    Global.Desktop.LoadPage(Global.ResUrl + string.Format("/Client.htm?token={0}", Global.CmdLineParams["token"]));
                }
				else
                {
                    Global.Desktop.LoadPage(Global.ResUrl + string.Format("/Client.htm?token=abcde"));
                    //Global.Desktop.LoadPage(Global.ResUrl + "/Client.htm");
                }

                Global.TrayIcon.Visible = true;

				HotKeyUtil.RegisterAllHotKey();

				ResponsesHandler.Instance.SetState("Status", "Online");

				Application.Run();
			}
			else
			{
				System.Diagnostics.Process.Start(AppDomain.CurrentDomain.BaseDirectory + "Update.exe");
			}
		}
        /// <summary>
        /// 获取CMD执行参数
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
		static Dictionary<String, String> GetCmdLineParams(string[] args)
		{
			Dictionary<String, String> ps = new Dictionary<String, String>();
			string opt = null;
			foreach (string arg in args)
			{
				if (arg.StartsWith("/") || arg.StartsWith("-"))
				{
					opt = arg.Substring(1);
					ps[opt] = "";
				}
				else if(opt != null)
				{
					ps[opt] = arg;
				}
			}
			return ps;
		}
        /// <summary>
        /// 创建桌面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		static public void Desktop_HandleCreated(object sender, EventArgs e)
		{
		}
        /// <summary>
        /// 销毁桌面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		static public void Desktop_HandleDestroyed(object sender, EventArgs e)
		{
		}

		static bool m_IsLeave = false;
        /// <summary>
        /// 是否离开状态
        /// </summary>
		public static bool IsLeave
		{
			get { return m_IsLeave; }
		}
        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="content"></param>
		static void WriteCrash(string content)
		{
			try
			{
				if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "crash"))
				{
					Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "crash");
				}
				File.WriteAllText(
					String.Format("{0}crash/{1:yyyyMMddHHmmss}.txt", AppDomain.CurrentDomain.BaseDirectory, DateTime.Now),
					content
				);
			}
			catch
			{
			}
		}
        /// <summary>
        /// 应用进程异常
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
		{
			try
			{
				String log = String.Format(
					"{0}:\r\n   {2}\r\n\r\nStackTrace:\r\n{1}\r\n\r\nModules:\r\n{3}\r\nProcesses:\r\n{4}\r\n系统信息:\r\n{5}",
					e.Exception.GetType().Name,
					e.Exception.StackTrace,
					e.Exception.Message,
					GetModules(),
					GetProcesses(),
					GetSystemInfo()
				);
				WriteCrash(log);
			}
			catch
			{
			}
		}
        /// <summary>
        /// 当前运行异常
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			try
			{
				Exception ex = e.ExceptionObject as Exception;
				if (ex != null)
				{
					String log = String.Format(
						"{0}:\r\n   {2}\r\n\r\nStackTrace:\r\n{1}\r\n\r\nModules:\r\n{3}\r\nProcesses:\r\n{4}\r\n系统信息:\r\n{5}",
						ex.GetType().Name,
						ex.StackTrace,
						ex.Message,
						GetModules(),
						GetProcesses(),
						GetSystemInfo()
					);
					WriteCrash(log);
				}
				else
				{
					String log = String.Format(
						"UnhandledException: {0}\r\nModules:\r\n{1}\r\nProcesses:\r\n{2}\r\n系统信息:\r\n{3}",
						e.ExceptionObject.GetType().FullName,
						GetModules(),
						GetProcesses(),
						GetSystemInfo()
					);
					WriteCrash(log);
				}
			}
			catch
			{
			}
		}
        /// <summary>
        /// 获取当前进程名称
        /// </summary>
        /// <returns></returns>
		private static String GetProcesses()
		{
			try
			{
				StringBuilder processesStr = new StringBuilder();
				Process[] processes = Process.GetProcesses();
				foreach (Process process in processes)
				{
					try
					{
						processesStr.AppendFormat("   {0}\r\n", process.MainModule.FileName);
					}
					catch
					{
					}
				}
				return processesStr.ToString();
			}
			catch
			{
				return String.Empty;
			}
		}
        /// <summary>
        /// 获取当前进行进程模块
        /// </summary>
        /// <returns></returns>
		private static String GetModules()
		{
			try
			{
				StringBuilder modules = new StringBuilder();
				Process cur = Process.GetCurrentProcess();
				for (int i = 0; i < cur.Modules.Count; i++)
				{
					try
					{
						ProcessModule m = cur.Modules[i];
						modules.AppendFormat("   {0}\r\n", m.FileName);
					}
					catch
					{
					}
				}
				return modules.ToString();
			}
			catch
			{
				return String.Empty;
			}
		}
        /// <summary>
        /// 获取当前系统信息
        /// </summary>
        /// <returns></returns>
		private static String GetSystemInfo()
		{
			try
			{
				Hashtable hash_info = new Hashtable();
				StringBuilder infos = new StringBuilder();

				ManagementClass mClass = new ManagementClass("Win32_OperatingSystem");
				ManagementObjectCollection moCollection = mClass.GetInstances();
				if (moCollection.Count > 0)
				{
					foreach (ManagementObject mObject in moCollection)
					{
						if (infos.Length == 0)
						{
							infos.AppendFormat("    操作系统名称：{0}\r\n", mObject["Caption"]);
							infos.AppendFormat("    操作系统版本：{0}\r\n", Environment.OSVersion);
							infos.AppendFormat("    总的物理内存：{0:#0.00}M\r\n", Convert.ToDouble(mObject["TotalVisibleMemorySize"]) / 1024);
							infos.AppendFormat("    可用物理内存：{0:#0.00}M\r\n", Convert.ToDouble(mObject["FreePhysicalMemory"]) / 1024);
							infos.AppendFormat("    总的虚拟内存：{0:#0.00}M\r\n", Convert.ToDouble(mObject["TotalVirtualMemorySize"]) / 1024);
							infos.AppendFormat("    可用虚拟内存：{0:#0.00}M\r\n", Convert.ToDouble(mObject["FreeVirtualMemory"]) / 1024);
							infos.AppendFormat("    页面文件大小：{0:#0.00}M\r\n", Convert.ToDouble(mObject["SizeStoredInPagingFiles"]) / 1024);
							infos.AppendFormat("    系统目录：    {0}\r\n", mObject["SystemDirectory"]);
							infos.AppendFormat("    Windows目录： {0}\r\n", mObject["WindowsDirectory"]);
						}
					}
				}
				return infos.ToString();
			}
			catch
			{
				return String.Empty;
			}
		}
        /// <summary>
        /// 检查更新
        /// </summary>
        /// <returns></returns>
		private static bool CheckUpdate()
		{
#			if DEBUG
			return false;
#			else
			String localUpdateConfigFile = AppDomain.CurrentDomain.BaseDirectory + "latest.xml";
			WebClient client = new WebClient();
			String url = Global.ServiceUrl + Global.AppPath;
			if (!url.EndsWith("/")) url += "/";
			url += "update/latest.xml?" + Math.Abs(DateTime.Now.ToBinary()).ToString();
			client.DownloadFile(url, localUpdateConfigFile);
			XmlDocument updataConfig = new XmlDocument();
			updataConfig.Load(localUpdateConfigFile);
			Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
			XmlElement updataNode = updataConfig.GetElementsByTagName("Update")[0] as XmlElement;
			Version latestVersion = new Version(updataNode.GetAttribute("Latest"));

			return currentVersion < latestVersion;
#			endif
		}
        /// <summary>
        /// 桌面关闭事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		static void Desktop_FormClosed(object sender, FormClosedEventArgs e)
		{
			ResponsesHandler.Instance.Stop();
			Global.TrayIcon.Visible = false;
			Application.Exit();
		}
        /// <summary>
        /// 桌面载入事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		static void Desktop_PageLoad(object sender, AppWnd.PageLoadEventArgs e)
		{
			if (System.IO.Path.GetFileName(e.Url).ToLower() != "login.htm")
			{
				if (!e.Result)
				{
					MessageBox.Show("连接服务器错误!", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
					Global.Desktop.Close();
					Global.TrayIcon.Visible = false;
					Application.Exit();
				}
			}
		}
        /// <summary>
        /// 初始化客户端配制
        /// </summary>
        /// <param name="success"></param>
		public static void InitClient(bool success)
		{
			if (success)
			{
				object core = Global.Desktop.Core;
				object config = Utility.GetProperty(core, "Config");
				Client.Global.ServerVersion = Utility.GetProperty(config, "Version").ToString();
			}
		}
        /// <summary>
        /// 初始化完成后事件
        /// </summary>
		public static void AfterInitService()
		{
			object core = Global.Desktop.Core;
			try
			{
				if (Global.ChatWith != String.Empty)
				{
					Utility.InvokeMethod(core, "ChatWith", new object[] { Global.ChatWith });
				}
			}
			catch
			{
			}
			object menu = Utility.InvokeMethod(core, "CreateMainMenu", new object[] { DBNull.Value });
			ContextMenu menuCtrl = (ContextMenu)Utility.GetProperty(menu, "MenuCtrl");
			menuCtrl.Popup += new EventHandler(menuCtrl_Popup);

			MenuItem[] clientMenuItems = new MenuItem[7];

			clientMenuItems[0] = new MenuItem();
			clientMenuItems[0].Text = "-";
			menuCtrl.MenuItems.Add(menuCtrl.MenuItems.Count - 1, clientMenuItems[0]);

			_menuSilence = new MenuItem();
			_menuSilence.Text = "关闭所有声音";
			_menuSilence.Checked = Config.Instance.GetValue("Slience") == "true";
			_menuSilence.Click += new EventHandler(menuSilence_Click);
			menuCtrl.MenuItems.Add(menuCtrl.MenuItems.Count - 1, _menuSilence);
			clientMenuItems[1] = _menuSilence;

			_menuUseMsgBox = new MenuItem();
			_menuUseMsgBox.Text = "使用消息盒子";
			_menuUseMsgBox.Checked = Config.Instance.GetValue("UseMsgBox") == "true";
			_menuUseMsgBox.Click += new EventHandler(menuUseMsgBox_Click); 
			menuCtrl.MenuItems.Add(menuCtrl.MenuItems.Count - 1, _menuUseMsgBox);
			clientMenuItems[2] = _menuUseMsgBox;

			clientMenuItems[3] = new MenuItem();
			clientMenuItems[3].Text = "-";
			menuCtrl.MenuItems.Add(menuCtrl.MenuItems.Count - 1, clientMenuItems[3]);

            _menuAutoLogin = new MenuItem();
            _menuAutoLogin.Text = "禁止自动登录";
            _menuAutoLogin.Checked = string.IsNullOrEmpty(Config.Instance.GetValue("AutoLogin"));
            _menuAutoLogin.Enabled = !_menuAutoLogin.Checked;
            _menuAutoLogin.Click += new EventHandler(menuAutoLogin_Click);
            menuCtrl.MenuItems.Add(menuCtrl.MenuItems.Count - 1, _menuAutoLogin);
            clientMenuItems[4] = _menuAutoLogin;

            _menuAutoStart = new MenuItem();
            _menuAutoStart.Text = "开机启动";
            _menuAutoStart.Checked = Config.Instance.GetValue("AutoStart") == "true";
            _menuAutoStart.Click += new EventHandler(menuAutoStart_Click);
            menuCtrl.MenuItems.Add(menuCtrl.MenuItems.Count - 1, _menuAutoStart);
            clientMenuItems[5] = _menuAutoLogin;

            clientMenuItems[6] = new MenuItem();
            clientMenuItems[6].Text = "-";
            menuCtrl.MenuItems.Add(menuCtrl.MenuItems.Count - 1, clientMenuItems[6]);

            Global.TrayIcon.ContextMenu = menuCtrl;
		}

		static MenuItem _menuSilence = null, _menuUseMsgBox = null,_menuAutoLogin=null, _menuAutoStart=null;
        /// <summary>
        /// 判断是否静音
        /// </summary>
		public static bool Silence
		{
			get { return _menuSilence.Checked; }
		}
        /// <summary>
        /// 判断是否启用消息合子
        /// </summary>
		public static bool UseMsgBox
		{
			get { return _menuUseMsgBox.Checked; }
		}
        /// <summary>
        /// 弹出窗面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		static void menuCtrl_Popup(object sender, EventArgs e)
		{
			if (_menuSilence != null) _menuSilence.Checked = Config.Instance.GetValue("Slience") == "true";
			if (_menuUseMsgBox != null) _menuUseMsgBox.Checked = Config.Instance.GetValue("UseMsgBox") == "true";
		}
        /// <summary>
        /// 静音菜单事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		static void menuSilence_Click(object sender, EventArgs e)
		{
			_menuSilence.Checked = !_menuSilence.Checked;
			Config.Instance.SetValue("Slience", _menuSilence.Checked ? "true" : "false");
		}
        /// <summary>
        /// 消息盒子菜单事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		static void menuUseMsgBox_Click(object sender, EventArgs e)
		{
			_menuUseMsgBox.Checked = !_menuUseMsgBox.Checked;
			Config.Instance.SetValue("UseMsgBox", _menuUseMsgBox.Checked ? "true" : "false");
		}
        /// <summary>
        /// 开机启动
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		static void menuAutoStart_Click(object sender, EventArgs e)
		{
            _menuAutoStart.Checked = !_menuAutoStart.Checked;
			Config.Instance.SetValue("AutoStart", _menuAutoStart.Checked ? "true" : "false");
		}
        /// <summary>
        /// 禁止自动登录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		static void menuAutoLogin_Click(object sender, EventArgs e)
		{
            _menuAutoLogin.Checked = true;
            Config.Instance.SetValue("AutoLogin", "");
            _menuAutoLogin.Enabled = false;
        }
        /// <summary>
        /// 退出事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		static void menuExitItem_Click(object sender, EventArgs e)
		{
			Global.Desktop.Close();
			ResponsesHandler.Instance.Stop();
			Global.TrayIcon.Visible = false;
			Application.Exit();
		}
        /// <summary>
        /// 获取未读消息事件
        /// </summary>
        /// <returns></returns>
		public static int GetUnreadMsgCount()
		{
			object impl = Utility.GetProperty(Global.Desktop.Core, "UnreadMsgBoxImpl");
			return (int)Utility.InvokeMethod(impl, "GetUnreadMsgCount", new object[] { });
		}
        /// <summary>
        /// 判断消息盒子是否可见
        /// </summary>
        /// <returns></returns>
		public static bool IsUnreadMsgBoxVisible()
		{
			object impl = Utility.GetProperty(Global.Desktop.Core, "UnreadMsgBoxImpl");
			return (bool)Utility.InvokeMethod(impl, "IsUnreadMsgBoxVisible", new object[] { });
		}
        /// <summary>
        /// 刷新任务栏图标
        /// </summary>
		public static void RefreshTrayIcon()
		{
			if (Program.GetUnreadMsgCount() > 0)
			{
				Global.TrayIcon.Icon = Resources.TrayMsg;
				Global.TrayIcon.Text = String.Format("您有 {0} 条未读消息", Program.GetUnreadMsgCount());
			}
			else
			{
				string status = ResponsesHandler.Instance.GetState("Status").ToString();
				if (status == "Offline") Global.TrayIcon.Icon = Resources.TrayGred;
				else Global.TrayIcon.Icon = Resources.Tray;

				object userinfo = SessionImpl.Instance.GetUserInfo();
				string nickname = Utility.GetProperty(userinfo, "Nickname").ToString();
				string name = Utility.GetProperty(userinfo, "Name").ToString();
				Client.Global.TrayIcon.Text = String.Format("{0}({1})", nickname, name);
			}
		}
        /// <summary>
        /// 任务栏图标鼠标划过事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		static void TrayIcon_MouseMove(object sender, MouseEventArgs e)
		{
		}
        /// <summary>
        /// 单击任务单图标
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		static void TrayIcon_Click(object sender, MouseEventArgs e)
		{
			if (SessionImpl.Instance.GetSessionID() != "" && e.Button == MouseButtons.Left)
			{
				if (GetUnreadMsgCount() > 0 && !Program.IsUnreadMsgBoxVisible())
				{
					object impl = Utility.GetProperty(Global.Desktop.Core, "UnreadMsgBoxImpl");
					Utility.InvokeMethod(impl, "ShowUnreadMsgBox", new object[] { 0 });
				}
				else
				{
					object singletonForm = SessionImpl.Instance.GetGlobal("SingletonForm");
					Utility.InvokeMethod(singletonForm, "ShowMainForm");
				}
			}
		}
	}
}
