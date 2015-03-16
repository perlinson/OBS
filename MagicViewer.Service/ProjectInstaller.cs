using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.Management;

namespace MagicViewer.Service
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }

        protected override void OnCommitted(System.Collections.IDictionary savedState)
        {
            base.OnCommitted(savedState);
            //将服务更改为允许桌面交互模式
            ConnectionOptions coOptions = new ConnectionOptions();
            coOptions.Impersonation = ImpersonationLevel.Impersonate;
            ManagementScope mgmtScope = new System.Management.ManagementScope(@"root\CIMV2", coOptions);
            mgmtScope.Connect();
            ManagementObject wmiService;
            wmiService = new ManagementObject("Win32_Service.Name='这里是当前服务名'");
            ManagementBaseObject InParam = wmiService.GetMethodParameters("Change");
            InParam["DesktopInteract"] = true;
            ManagementBaseObject OutParam = wmiService.InvokeMethod("Change", InParam, null);
        }
    }
}
