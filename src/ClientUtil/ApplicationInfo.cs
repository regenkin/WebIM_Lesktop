using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Security;

namespace Custom
{
	public class ApplicationInfo
	{
		static ApplicationInfo m_Instance = new ApplicationInfo();

		public static ApplicationInfo Instance
		{
			get { return m_Instance; }
		}

		private ApplicationInfo()
		{
		}

		public const String AssemblyTitle = "君飞即时通讯软件";

		public const String AssemblyCompany = "君飞";

		public const String AssemblyProduct = "君飞即时通讯软件";

		public const String AssemblyCopyright = "Copyright © kinfar.net 2018";

		public const String ReleaseVersion = "8.0.0.0";

		public const bool FilterHtml = true;

		public const bool CheckLoginName = false;
	}
}
