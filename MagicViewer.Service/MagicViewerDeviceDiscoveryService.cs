using Dolinay;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Windows.Forms;
using USBClassLibrary;

namespace MagicViewer.Service
{
    public partial class MagicViewerDeviceDiscoveryService : ServiceBase
    {
//        private DetectorForm mFrm;
        private DriveDetector driveDetector = null;
        public MagicViewerDeviceDiscoveryService()
        {
            InitializeComponent();
            driveDetector = new DriveDetector();
            driveDetector.mUSBPort.USBDeviceAttached += new USBClass.USBDeviceEventHandler(OnDriveArrived);
            driveDetector.mUSBPort.USBDeviceRemoved += new USBClass.USBDeviceEventHandler(OnDriveRemoved);
        }



        void OnDriveRemoved(object sender, USBClass.USBDeviceEventArgs e)
        {
            Debugger.Log(0, null, "DeviceRemoved");
        }

        void OnDriveArrived(object sender, USBClass.USBDeviceEventArgs e)
        {
            Debugger.Log(0, null, "DeviceArrived");
        }

        protected override void OnStart(string[] args)
        {
        }

        protected override void OnStop()
        {
           
        }
    }
}
