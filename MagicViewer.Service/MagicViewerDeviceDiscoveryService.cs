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
            driveDetector.DeviceArrived += new DriveDetectorEventHandler(OnDriveArrived);
            driveDetector.DeviceRemoved += new DriveDetectorEventHandler(OnDriveRemoved);




        }



        void OnDriveRemoved(object sender, DriveDetectorEventArgs e)
        {
            //Debugger.Log(0, null, "DeviceRemoved");
        }

        void OnDriveArrived(object sender, DriveDetectorEventArgs e)
        {



            //Debugger.Log(0, null, "DeviceArrived");
        }

        protected override void OnStart(string[] args)
        {
        }

        protected override void OnStop()
        {
           
        }
    }
}
