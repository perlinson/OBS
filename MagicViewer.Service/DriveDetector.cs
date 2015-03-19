using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;             // required for Message
using System.Runtime.InteropServices;   // required for Marshal
using System.IO;
using Microsoft.Win32.SafeHandles;
using USBClassLibrary;
using System.Configuration;
using System.Diagnostics;
// DriveDetector - rev. 1, Oct. 31 2007

namespace Dolinay
{
    /// <summary>
    /// Hidden Form which we use to receive Windows messages about flash drives
    /// </summary>
    internal class DetectorForm : Form
    {
        private Label label1;
        private DriveDetector mDetector = null;

        /// <summary>
        /// Set up the hidden form. 
        /// </summary>
        /// <param name="detector">DriveDetector object which will receive notification about USB drives, see WndProc</param>
        public DetectorForm(DriveDetector detector)
        {
            mDetector = detector;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ShowInTaskbar = false;
            this.ShowIcon = false;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Load += new System.EventHandler(this.Load_Form);
            this.Activated += new EventHandler(this.Form_Activated);
        }

        private void Load_Form(object sender, EventArgs e)
        {
            // We don't really need this, just to display the label in designer ...
            InitializeComponent();

            // Create really small form, invisible anyway.
            this.Size = new System.Drawing.Size(5, 5);

            mDetector.mUSBPort.RegisterForDeviceChange(true, this.Handle);


        }

        private void Form_Activated(object sender, EventArgs e)
        {
            this.Visible = false;
        }

        /// <summary>
        /// This function receives all the windows messages for this window (form).
        /// We call the DriveDetector from here so that is can pick up the messages about
        /// drives arrived and removed.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (mDetector != null)
            {
                mDetector.WndProc(ref m);
            }
        }

        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 30);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(593, 18);
            this.label1.TabIndex = 0;
            this.label1.Text = "This is invisible form. To see DriveDetector code click View Code";
            // 
            // DetectorForm
            // 
            this.ClientSize = new System.Drawing.Size(360, 80);
            this.Controls.Add(this.label1);
            this.Name = "DetectorForm";
            this.ResumeLayout(false);
            this.PerformLayout();

        }
    }   // class DetectorForm


    // Delegate for event handler to handle the device events 
    public delegate void DriveDetectorEventHandler(Object sender, DriveDetectorEventArgs e);
    
    /// <summary>
    /// Our class for passing in custom arguments to our event handlers 
    /// 
    /// </summary>
    public class DriveDetectorEventArgs : EventArgs 
    {


        public DriveDetectorEventArgs()
        {
            Cancel = false;
            Drive = "";
            HookQueryRemove = false;
        }

        /// <summary>
        /// Get/Set the value indicating that the event should be cancelled 
        /// Only in QueryRemove handler.
        /// </summary>
        public bool Cancel;

        /// <summary>
        /// Drive letter for the device which caused this event 
        /// </summary>
        public string Drive;

        /// <summary>
        /// Set to true in your DeviceArrived event handler if you wish to receive the 
        /// QueryRemove event for this drive. 
        /// </summary>
        public bool HookQueryRemove;

    }

    
    /// <summary>
    /// Detects insertion or removal of removable drives.
    /// Use it in 1 or 2 steps:
    /// 1) Create instance of this class in your project and add handlers for the
    /// DeviceArrived, DeviceRemoved and QueryRemove events.
    /// AND (if you do not want drive detector to creaate a hidden form))
    /// 2) Override WndProc in your form and call DriveDetector's WndProc from there. 
    /// If you do not want to do step 2, just use the DriveDetector constructor without arguments and
    /// it will create its own invisible form to receive messages from Windows.
    /// </summary>
    class DriveDetector : IDisposable 
    {
        public USBClass mUSBPort = new USBClass();
        private Process process1 = new Process();
        private uint mDevPID = 0;
        private uint mDevVID = 0;

        /// <summary>
        /// The easiest way to use DriveDetector. 
        /// It will create hidden form for processing Windows messages about USB drives
        /// You do not need to override WndProc in your form.
        /// </summary>
        public DriveDetector()
        {
            mDevPID = uint.Parse(ConfigurationManager.AppSettings["DevicePID"], System.Globalization.NumberStyles.AllowHexSpecifier);
            mDevVID= uint.Parse(ConfigurationManager.AppSettings["DeviceVID"], System.Globalization.NumberStyles.AllowHexSpecifier);

            

            if (mDevPID == 0 || mDevVID== 0)
            {
                Debugger.Log(0, null, "vaild PID or VID");
                return;
            }

            ListOfUSBDeviceProperties = new List<USBClass.DeviceProperties>();

            DetectorForm  frm = new DetectorForm(this);
            frm.Show(); // will be hidden immediatelly

            // 
            // process1
            // 
            this.process1.StartInfo.Domain = "";
            this.process1.StartInfo.LoadUserProfile = false;
            this.process1.StartInfo.Password = null;
            this.process1.StartInfo.StandardErrorEncoding = null;
            this.process1.StartInfo.StandardOutputEncoding = null;
            this.process1.StartInfo.UserName = "";
            this.process1.SynchronizingObject = frm;
            this.process1.StartInfo.FileName= @"D:\program\C++\OBS\rundir\MagicViewer.exe";

            bDeviceConectting = USBTryMyDeviceConnection();
        }

        private void Connect()
        {
            if (bDeviceConectting)
            {
                return;
            }
            this.process1.Start();
        }

        private void Disconnect()
        {
            //TO DO: Insert your disconnection code here
            if (!bDeviceConectting)
            {
                return;
            }
            this.process1.Kill();
        }

        public bool bDeviceConectting { get; set; }


        private System.Collections.Generic.List<USBClassLibrary.USBClass.DeviceProperties> ListOfUSBDeviceProperties;

        public bool USBTryMyDeviceConnection()
        {
            if (USBClass.GetUSBDevice(mDevPID, mDevVID, ref ListOfUSBDeviceProperties, false))
            {
                //My Device is attached
                
                for (int i = 0; i < ListOfUSBDeviceProperties.Count; i++)
                {
                    //FoundDevicesComboBox.Items.Add("Device " + i.ToString());
                }
                //FoundDevicesComboBox.Enabled = (ListOfUSBDeviceProperties.Count > 1);
                //FoundDevicesComboBox.SelectedIndex = 0;

                Connect();

                return true;
            }
            else
            {
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// Alternate constructor.
        /// Pass in your Form and DriveDetector will not create hidden form.
        /// </summary>
        /// <param name="control">object which will receive Windows messages. 
        /// Pass "this" as this argument from your form class.</param>
        public DriveDetector(Control control)
        {
        }

        /// <summary>
        /// Consructs DriveDetector object setting also path to file which should be opened
        /// when registering for query remove.  
        /// </summary>
        ///<param name="control">object which will receive Windows messages. 
        /// Pass "this" as this argument from your form class.</param>
        /// <param name="FileToOpen">Optional. Name of a file on the removable drive which should be opened. 
        /// If null, root directory of the drive will be opened. Opening a file is needed for us 
        /// to be able to register for the query remove message. TIP: For files use relative path without drive letter.
        /// e.g. "SomeFolder\file_on_flash.txt"</param>
        public DriveDetector(Control control, string FileToOpen)
        {
        }


        /// <summary>
        /// Unregister and close the file we may have opened on the removable drive. 
        /// Garbage collector will call this method.
        /// </summary>
        public void Dispose()
        {
            mUSBPort.RegisterForDeviceChange(false, IntPtr.Zero);
        }


        #region WindowProc
        /// <summary>
        /// Message handler which must be called from client form.
        /// Processes Windows messages and calls event handlers. 
        /// </summary>
        /// <param name="m"></param>
        public void WndProc(ref Message m)
        {
            bool bHandled   =  false;
            mUSBPort.ProcessWindowsMessage(m.Msg, m.WParam, m.LParam, ref bHandled);

            //int devType;
            //char c;

            //if (m.Msg == WM_DEVICECHANGE)
            //{
            //    // WM_DEVICECHANGE can have several meanings depending on the WParam value...
            //    switch (m.WParam.ToInt32())
            //    {

            //            //
            //            // New device has just arrived
            //            //
            //        case DBT_DEVICEARRIVAL:

            //            devType = Marshal.ReadInt32(m.LParam, 4);
            //            if (devType == DBT_DEVTYP_VOLUME)
            //            {
            //                DEV_BROADCAST_VOLUME vol;
            //                vol = (DEV_BROADCAST_VOLUME)
            //                    Marshal.PtrToStructure(m.LParam, typeof(DEV_BROADCAST_VOLUME));

            //                // Get the drive letter 
            //                c = DriveMaskToLetter(vol.dbcv_unitmask);


            //                //
            //                // Call the client event handler
            //                //
            //                // We should create copy of the event before testing it and
            //                // calling the delegate - if any
            //                DriveDetectorEventHandler tempDeviceArrived = DeviceArrived;
            //                if ( tempDeviceArrived != null )
            //                {
            //                    DriveDetectorEventArgs e = new DriveDetectorEventArgs();
            //                    e.Drive = c + ":\\";
            //                    tempDeviceArrived(this, e);
                                
            //                    // Register for query remove if requested
            //                    if (e.HookQueryRemove)
            //                    {
            //                        // If something is already hooked, unhook it now
            //                        if (mDeviceNotifyHandle != IntPtr.Zero)
            //                        {
            //                            RegisterForDeviceChange(false, null);
            //                        }
                                        
            //                       RegisterQuery(c + ":\\");
            //                    }
            //                }     // if  has event handler

                            
            //            }
            //            break;



            //            //
            //            // Device is about to be removed
            //            // Any application can cancel the removal
            //            //
            //        case DBT_DEVICEQUERYREMOVE:

            //            devType = Marshal.ReadInt32(m.LParam, 4);
            //            if (devType == DBT_DEVTYP_HANDLE)
            //            {
            //                // TODO: we could get the handle for which this message is sent 
            //                // from vol.dbch_handle and compare it against a list of handles for 
            //                // which we have registered the query remove message (?)                                                 
            //                //DEV_BROADCAST_HANDLE vol;
            //                //vol = (DEV_BROADCAST_HANDLE)
            //                //   Marshal.PtrToStructure(m.LParam, typeof(DEV_BROADCAST_HANDLE));
            //                // if ( vol.dbch_handle ....
                            
  
            //                //
            //                // Call the event handler in client
            //                //
            //                DriveDetectorEventHandler tempQuery = QueryRemove;
            //                if (tempQuery != null)
            //                {
            //                    DriveDetectorEventArgs e = new DriveDetectorEventArgs();
            //                    e.Drive = mCurrentDrive;        // drive which is hooked
            //                    tempQuery(this, e);

            //                    // If the client wants to cancel, let Windows know
            //                    if (e.Cancel)
            //                    {                                    
            //                        m.Result = (IntPtr)BROADCAST_QUERY_DENY;
            //                    }
            //                    else
            //                    {
            //                        // Change 28.10.2007: Unregister the notification, this will
            //                        // close the handle to file or root directory also. 
            //                        // We have to close it anyway to allow the removal so
            //                        // even if some other app cancels the removal we would not know about it...                                    
            //                        RegisterForDeviceChange(false, null);   // will also close the mFileOnFlash
            //                    }

            //               }                          
            //            }
            //            break;


            //            //
            //            // Device has been removed
            //            //
            //        case DBT_DEVICEREMOVECOMPLETE:

            //            devType = Marshal.ReadInt32(m.LParam, 4);
            //            if (devType == DBT_DEVTYP_VOLUME)
            //            {
            //                devType = Marshal.ReadInt32(m.LParam, 4);
            //                if (devType == DBT_DEVTYP_VOLUME)
            //                {
            //                    DEV_BROADCAST_VOLUME vol;
            //                    vol = (DEV_BROADCAST_VOLUME)
            //                        Marshal.PtrToStructure(m.LParam, typeof(DEV_BROADCAST_VOLUME));
            //                    c = DriveMaskToLetter(vol.dbcv_unitmask);

            //                    //
            //                    // Call the client event handler
            //                    //
            //                    DriveDetectorEventHandler tempDeviceRemoved = DeviceRemoved;
            //                    if (tempDeviceRemoved != null)
            //                    {
            //                        DriveDetectorEventArgs e = new DriveDetectorEventArgs();
            //                        e.Drive = c + ":\\";
            //                        tempDeviceRemoved(this, e);
            //                    }

            //                    // TODO: we could unregister the notify handle here if we knew it is the
            //                    // right drive which has been just removed
            //                    //RegisterForDeviceChange(false, null);
            //                }
            //            }
            //            break;
            //    }

            //}

        }
        #endregion



        #region  Private Area

        /// <summary>
        /// New: 28.10.2007 - handle to root directory of flash drive which is opened
        /// for device notification
        /// </summary>
        private IntPtr mDirHandle = IntPtr.Zero;

        // Win32 constants
        private const int DBT_DEVTYP_DEVICEINTERFACE = 5;
        private const int DBT_DEVTYP_HANDLE = 6;
        private const int BROADCAST_QUERY_DENY = 0x424D5144;
        private const int WM_DEVICECHANGE = 0x0219;
        private const int DBT_DEVICEARRIVAL = 0x8000; // system detected a new device
        private const int DBT_DEVICEQUERYREMOVE = 0x8001;   // Preparing to remove (any program can disable the removal)
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004; // removed 
        private const int DBT_DEVTYP_VOLUME = 0x00000002; // drive type is logical volume

        /// <summary>
        /// Gets drive letter from a bit mask where bit 0 = A, bit 1 = B etc.
        /// There can actually be more than one drive in the mask but we 
        /// just use the last one in this case.
        /// </summary>
        /// <param name="mask"></param>
        /// <returns></returns>
        private static char DriveMaskToLetter(int mask)
        {
            char letter;
            string drives = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            // 1 = A
            // 2 = B
            // 4 = C...
            int cnt = 0;
            int pom = mask / 2;     
            while (pom != 0)
            {
                // while there is any bit set in the mask
                // shift it to the righ...                
                pom = pom / 2;
                cnt++;
            }

            if (cnt < drives.Length)
                letter = drives[cnt];
            else
                letter = '?';

            return letter;
        }

        /* 28.10.2007 - no longer needed
        /// <summary>
        /// Searches for any file in a given path and returns its full path
        /// </summary>
        /// <param name="drive">drive to search</param>
        /// <returns>path of the file or empty string</returns>
        private string GetAnyFile(string drive)
        {
            string file = "";
            // First try files in the root
            string[] files = Directory.GetFiles(drive);
            if (files.Length == 0)
            {
                // if no file in the root, search whole drive
                files = Directory.GetFiles(drive, "*.*", SearchOption.AllDirectories);
            }
                
            if (files.Length > 0)
                file = files[0];        // get the first file

            // return empty string if no file found
            return file;
        }*/
        #endregion


        #region Native Win32 API
        /// <summary>
        /// WinAPI functions
        /// </summary>        
        private class Native
        {
            //   HDEVNOTIFY RegisterDeviceNotification(HANDLE hRecipient,LPVOID NotificationFilter,DWORD Flags);
            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr RegisterDeviceNotification(IntPtr hRecipient, IntPtr NotificationFilter, uint Flags);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern uint UnregisterDeviceNotification(IntPtr hHandle);

            //
            // CreateFile  - MSDN
            const uint GENERIC_READ = 0x80000000;
            const uint OPEN_EXISTING = 3;
            const uint FILE_SHARE_READ = 0x00000001;
            const uint FILE_SHARE_WRITE = 0x00000002;
            const uint FILE_ATTRIBUTE_NORMAL = 128;
            const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
            static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);


            // should be "static extern unsafe"
            [DllImport("kernel32", SetLastError = true)]
            static extern IntPtr CreateFile(
                  string FileName,                    // file name
                  uint DesiredAccess,                 // access mode
                  uint ShareMode,                     // share mode
                  uint SecurityAttributes,            // Security Attributes
                  uint CreationDisposition,           // how to create
                  uint FlagsAndAttributes,            // file attributes
                  int hTemplateFile                   // handle to template file
                  );


            [DllImport("kernel32", SetLastError = true)]
            static extern bool CloseHandle(
                  IntPtr hObject   // handle to object
                  );

            /// <summary>
            /// Opens a directory, returns it's handle or zero.
            /// </summary>
            /// <param name="dirPath">path to the directory, e.g. "C:\\dir"</param>
            /// <returns>handle to the directory. Close it with CloseHandle().</returns>
            static public IntPtr OpenDirectory(string dirPath)
            {
                // open the existing file for reading          
                IntPtr handle = CreateFile(
                      dirPath,
                      GENERIC_READ,
                      FILE_SHARE_READ | FILE_SHARE_WRITE,
                      0,
                      OPEN_EXISTING,
                      FILE_FLAG_BACKUP_SEMANTICS | FILE_ATTRIBUTE_NORMAL,
                      0);

                if ( handle == INVALID_HANDLE_VALUE)
                    return IntPtr.Zero;
                else
                    return handle;
            }


            public static bool CloseDirectoryHandle(IntPtr handle)
            {
                return CloseHandle(handle);
            }
        }

        
        // Structure with information for RegisterDeviceNotification.
        [StructLayout(LayoutKind.Sequential)]
        public struct DEV_BROADCAST_HANDLE
        {
            public int dbch_size;
            public int dbch_devicetype;
            public int dbch_reserved;
            public IntPtr dbch_handle;
            public IntPtr dbch_hdevnotify;
            public Guid dbch_eventguid;
            public long dbch_nameoffset;
            //public byte[] dbch_data[1]; // = new byte[1];
            public byte dbch_data;
            public byte dbch_data1; 
        }

        // Struct for parameters of the WM_DEVICECHANGE message
        [StructLayout(LayoutKind.Sequential)]
        public struct DEV_BROADCAST_VOLUME
        {
            public int dbcv_size;
            public int dbcv_devicetype;
            public int dbcv_reserved;
            public int dbcv_unitmask;
        }
        #endregion

    }
}
