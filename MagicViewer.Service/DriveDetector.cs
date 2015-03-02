using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;             // required for Message
using System.Runtime.InteropServices;   // required for Marshal
using System.IO;
using Microsoft.Win32.SafeHandles;     
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
            this.label1.Size = new System.Drawing.Size(314, 13);
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
         const Int64 INVALID_HANDLE_VALUE = -1;
        const int BUFFER_SIZE = 1024;
        //IntPtr deviceEventHandle;
        /// <summary>
        /// Events signalized to the client app.
        /// Add handlers for these events in your form to be notified of removable device events 
        /// </summary>
        public event DriveDetectorEventHandler DeviceArrived;
        public event DriveDetectorEventHandler DeviceRemoved;
        public event DriveDetectorEventHandler QueryRemove;

        /// <summary>
        /// The easiest way to use DriveDetector. 
        /// It will create hidden form for processing Windows messages about USB drives
        /// You do not need to override WndProc in your form.
        /// </summary>
        public DriveDetector()
        {
            DetectorForm  frm = new DetectorForm(this);
            frm.Show(); // will be hidden immediatelly
            Init(frm, null);
        }

        /// <summary>
        /// Alternate constructor.
        /// Pass in your Form and DriveDetector will not create hidden form.
        /// </summary>
        /// <param name="control">object which will receive Windows messages. 
        /// Pass "this" as this argument from your form class.</param>
        public DriveDetector(Control control)
        {
            Init(control, null);
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
            Init(control, FileToOpen);
        }

        /// <summary>
        /// init the DriveDetector object
        /// </summary>
        /// <param name="intPtr"></param>
        private void Init(Control control, string fileToOpen)
        {
            mFileToOpen = fileToOpen;
            mFileOnFlash = null;
            mDeviceNotifyHandle = IntPtr.Zero;
            mRecipientHandle = control.Handle;
            mDirHandle = IntPtr.Zero;   // handle to the root directory of the flash drive which we open 
            mCurrentDrive = "";
        }

        /// <summary>
        /// Gets the value indicating whether the query remove event will be fired.
        /// </summary>
	    public bool IsQueryHooked
	    {
		    get
            {
                if (mDeviceNotifyHandle == IntPtr.Zero)
                    return false;
                else
                    return true;
            }
	    }

        /// <summary>
        /// Gets letter of drive which is currently hooked. Empty string if none.
        /// See also IsQueryHooked.
        /// </summary>
        public string HookedDrive
        {
            get
            {
                return mCurrentDrive;
            }
        }

        /// <summary>
        /// Gets the file stream for file which this class opened on a drive to be notified
        /// about it's removal. 
        /// This will be null unless you specified a file to open (DriveDetector opens root directory of the flash drive) 
        /// </summary>
        public FileStream OpenedFile
        {
            get
            {
                return mFileOnFlash;
            }
        }

        /// <summary>
        /// Hooks specified drive to receive a message when it is being removed.  
        /// This can be achieved also by setting e.HookQueryRemove to true in your 
        /// DeviceArrived event handler. 
        /// By default DriveDetector will open the root directory of the flash drive to obtain notification handle
        /// from Windows (to learn when the drive is about to be removed). 
        /// </summary>
        /// <param name="fileOnDrive">Drive letter or relative path to a file on the drive which should be 
        /// used to get a handle - required for registering to receive query remove messages.
        /// If only drive letter is specified (e.g. "D:\\", root directory of the drive will be opened.</param>
        /// <returns>true if hooked ok, false otherwise</returns>
        public bool EnableQueryRemove(string fileOnDrive)
        {
            if (fileOnDrive == null || fileOnDrive.Length == 0)
                throw new ArgumentException("Drive path must be supplied to register for Query remove.");
            
            if ( fileOnDrive.Length == 2 && fileOnDrive[1] == ':' )
                fileOnDrive += '\\';        // append "\\" if only drive letter with ":" was passed in.

            if (mDeviceNotifyHandle != IntPtr.Zero)
            {
                // Unregister first...
                RegisterForDeviceChange(false, null);
            }

            if (Path.GetFileName(fileOnDrive).Length == 0 ||!File.Exists(fileOnDrive))
                mFileToOpen = null;     // use root directory...
            else
                mFileToOpen = fileOnDrive;

            RegisterQuery(Path.GetPathRoot(fileOnDrive));
            if (mDeviceNotifyHandle == IntPtr.Zero)
                return false;   // failed to register

            return true;
        }

        /// <summary>
        /// Unhooks any currently hooked drive so that the query remove 
        /// message is not generated for it.
        /// </summary>
        public void DisableQueryRemove()
        {
            if (mDeviceNotifyHandle != IntPtr.Zero)
            {
                RegisterForDeviceChange(false, null);
            }
        }


        /// <summary>
        /// Unregister and close the file we may have opened on the removable drive. 
        /// Garbage collector will call this method.
        /// </summary>
        public void Dispose()
        {
            RegisterForDeviceChange(false, null);
        }


        #region WindowProc
        /// <summary>
        /// Message handler which must be called from client form.
        /// Processes Windows messages and calls event handlers. 
        /// </summary>
        /// <param name="m"></param>
        public void WndProc(ref Message m)
        {
            int devType;
            char c;

            if (m.Msg == WM_DEVICECHANGE)
            {
                // WM_DEVICECHANGE can have several meanings depending on the WParam value...
                switch (m.WParam.ToInt32())
                {

                        //
                        // New device has just arrived
                        //
                    case DBT_DEVICEARRIVAL:

                        break;



                        //
                        // Device is about to be removed
                        // Any application can cancel the removal
                        //
                    case DBT_DEVICEQUERYREMOVE:

                        devType = Marshal.ReadInt32(m.LParam, 4);
                        if (devType == DBT_DEVTYP_HANDLE)
                        {
                            // TODO: we could get the handle for which this message is sent 
                            // from vol.dbch_handle and compare it against a list of handles for 
                            // which we have registered the query remove message (?)                                                 
                            //DEV_BROADCAST_HANDLE vol;
                            //vol = (DEV_BROADCAST_HANDLE)
                            //   Marshal.PtrToStructure(m.LParam, typeof(DEV_BROADCAST_HANDLE));
                            // if ( vol.dbch_handle ....
                            
  
                            //
                            // Call the event handler in client
                            //
                            DriveDetectorEventHandler tempQuery = QueryRemove;
                            if (tempQuery != null)
                            {
                                DriveDetectorEventArgs e = new DriveDetectorEventArgs();
                                e.Drive = mCurrentDrive;        // drive which is hooked
                                tempQuery(this, e);

                                // If the client wants to cancel, let Windows know
                                if (e.Cancel)
                                {                                    
                                    m.Result = (IntPtr)BROADCAST_QUERY_DENY;
                                }
                                else
                                {
                                    // Change 28.10.2007: Unregister the notification, this will
                                    // close the handle to file or root directory also. 
                                    // We have to close it anyway to allow the removal so
                                    // even if some other app cancels the removal we would not know about it...                                    
                                    RegisterForDeviceChange(false, null);   // will also close the mFileOnFlash
                                }

                           }                          
                        }
                        break;


                        //
                        // Device has been removed
                        //
                    case DBT_DEVICEREMOVECOMPLETE:

                        devType = Marshal.ReadInt32(m.LParam, 4);
                        if (devType == DBT_DEVTYP_VOLUME)
                        {
                            devType = Marshal.ReadInt32(m.LParam, 4);
                            if (devType == DBT_DEVTYP_VOLUME)
                            {
                                DEV_BROADCAST_VOLUME vol;
                                vol = (DEV_BROADCAST_VOLUME)
                                    Marshal.PtrToStructure(m.LParam, typeof(DEV_BROADCAST_VOLUME));
                                c = DriveMaskToLetter(vol.dbcv_unitmask);

                                //
                                // Call the client event handler
                                //
                                DriveDetectorEventHandler tempDeviceRemoved = DeviceRemoved;
                                if (tempDeviceRemoved != null)
                                {
                                    DriveDetectorEventArgs e = new DriveDetectorEventArgs();
                                    e.Drive = c + ":\\";
                                    tempDeviceRemoved(this, e);
                                }

                                // TODO: we could unregister the notify handle here if we knew it is the
                                // right drive which has been just removed
                                //RegisterForDeviceChange(false, null);
                            }
                        }
                        break;
                }

            }

        }
        #endregion



        #region  Private Area

        /// <summary>
        /// New: 28.10.2007 - handle to root directory of flash drive which is opened
        /// for device notification
        /// </summary>
        private IntPtr mDirHandle = IntPtr.Zero;

        /// <summary>
        /// Class which contains also handle to the file opened on the flash drive
        /// </summary>
        private FileStream mFileOnFlash = null;

        /// <summary>
        /// Name of the file to try to open on the removable drive for query remove registration
        /// </summary>
        private string mFileToOpen;

        /// <summary>
        /// Handle to file which we keep opened on the drive if query remove message is required by the client
        /// </summary>       
        private IntPtr mDeviceNotifyHandle;

        /// <summary>
        /// Handle of the window which receives messages from Windows. This will be a form.
        /// </summary>
        private IntPtr mRecipientHandle;

        /// <summary>
        /// Drive which is currently hooked for query remove
        /// </summary>
        private string mCurrentDrive;   


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
        /// Registers for receiving the query remove message for a given drive.
        /// We need to open a handle on that drive and register with this handle. 
        /// Client can specify this file in mFileToOpen or we will open root directory of the drive
        /// </summary>
        /// <param name="drive">drive for which to register. </param>
        private void RegisterQuery(string drive)
        {
            bool register = true;

            if (mFileToOpen == null)
            {
                // Change 28.10.2007 - Open the root directory if no file specified - leave mFileToOpen null 
                // If client gave us no file, let's pick one on the drive... 
                //mFileToOpen = GetAnyFile(drive);
                //if (mFileToOpen.Length == 0)
                //    return;     // no file found on the flash drive                
            }
            else
            {
                // Make sure the path in mFileToOpen contains valid drive
                // If there is a drive letter in the path, it may be different from the  actual
                // letter assigned to the drive now. We will cut it off and merge the actual drive 
                // with the rest of the path.
                if (mFileToOpen.Contains(":"))
                {
                    string tmp = mFileToOpen.Substring(3);
                    string root = Path.GetPathRoot(drive);
                    mFileToOpen = Path.Combine(root, tmp);
                }
                else
                    mFileToOpen = Path.Combine(drive, mFileToOpen);
            }


            try
            {
                //mFileOnFlash = new FileStream(mFileToOpen, FileMode.Open);
                // Change 28.10.2007 - Open the root directory 
                if (mFileToOpen == null)  // open root directory
                    mFileOnFlash = null;
                else
                    mFileOnFlash = new FileStream(mFileToOpen, FileMode.Open);
            }
            catch (Exception)
            {
                // just do not register if the file could not be opened
                register = false;
            }


            if (register)
            {
                //RegisterForDeviceChange(true, mFileOnFlash.SafeFileHandle);
                //mCurrentDrive = drive;
                // Change 28.10.2007 - Open the root directory 
                if (mFileOnFlash == null)
                    RegisterForDeviceChange(drive);
                else
                    // old version
                    RegisterForDeviceChange(true, mFileOnFlash.SafeFileHandle);

                mCurrentDrive = drive;
            }


        }


        /// <summary>
        /// New version which gets the handle automatically for specified directory
        /// Only for registering! Unregister with the old version of this function...
        /// </summary>
        /// <param name="register"></param>
        /// <param name="dirPath">e.g. C:\\dir</param>
        private void RegisterForDeviceChange(string dirPath)
        {
            IntPtr handle = Win32Wrapper.OpenDirectory(dirPath);
            if (handle == IntPtr.Zero)
            {
                mDeviceNotifyHandle = IntPtr.Zero;
                return;
            }
            else
                mDirHandle = handle;    // save handle for closing it when unregistering

            // Register for handle
            DEV_BROADCAST_HANDLE data = new DEV_BROADCAST_HANDLE();
            data.dbch_devicetype = DBT_DEVTYP_HANDLE;
            data.dbch_reserved = 0;
            data.dbch_nameoffset = 0;
            //data.dbch_data = null;
            //data.dbch_eventguid = 0;
            data.dbch_handle = handle;
            data.dbch_hdevnotify = (IntPtr)0;
            int size = Marshal.SizeOf(data);
            data.dbch_size = size;
            IntPtr buffer = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(data, buffer, true);

            mDeviceNotifyHandle = Win32Wrapper.RegisterDeviceNotification(mRecipientHandle, buffer, 0);

        }

        /// <summary>
        /// Registers to be notified when the volume is about to be removed
        /// This is requierd if you want to get the QUERY REMOVE messages
        /// </summary>
        /// <param name="register">true to register, false to unregister</param>
        /// <param name="fileHandle">handle of a file opened on the removable drive</param>
        private void RegisterForDeviceChange(bool register, SafeFileHandle fileHandle)
        {
            if (register)
            {
                // Register for handle
                DEV_BROADCAST_HANDLE data = new DEV_BROADCAST_HANDLE();
                data.dbch_devicetype = DBT_DEVTYP_HANDLE;
                data.dbch_reserved = 0;
                data.dbch_nameoffset = 0;
                //data.dbch_data = null;
                //data.dbch_eventguid = 0;
                data.dbch_handle = fileHandle.DangerousGetHandle(); //Marshal. fileHandle; 
                data.dbch_hdevnotify = (IntPtr)0;
                int size = Marshal.SizeOf(data);
                data.dbch_size = size;
                IntPtr buffer = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(data, buffer, true);

                mDeviceNotifyHandle = Win32Wrapper.RegisterDeviceNotification(mRecipientHandle, buffer, 0);
            }
            else
            {
                // close the directory handle
                if (mDirHandle != IntPtr.Zero)
                {
                    Win32Wrapper.CloseDirectoryHandle(mDirHandle);
                    //    string er = Marshal.GetLastWin32Error().ToString();
                }

                // unregister
                if (mDeviceNotifyHandle != IntPtr.Zero)
                {
                    Win32Wrapper.UnregisterDeviceNotification(mDeviceNotifyHandle);                                        
                }
                

                mDeviceNotifyHandle = IntPtr.Zero;
                mDirHandle = IntPtr.Zero;
               
                mCurrentDrive = "";
                if (mFileOnFlash != null)
                {
                    mFileOnFlash.Close();
                    mFileOnFlash = null;
                }
            }

        }

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


        /// WinAPI functions
        /// </summary>        
        private class Win32Wrapper
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
            //http://msdn.microsoft.com/en-us/library/bb663138.aspx
            /// <summary>
            /// Device Interface GUIDs.
            /// </summary>
            public struct GUID_DEVINTERFACE
            {
                public const string DISK = "53f56307-b6bf-11d0-94f2-00a0c91efb8b";
                public const string HUBCONTROLLER = "3abf6f2d-71c4-462a-8a92-1e6861e6af27";
                public const string MODEM = "2C7089AA-2E0E-11D1-B114-00C04FC2AAE4";
                public const string SERENUM_BUS_ENUMERATOR = "4D36E978-E325-11CE-BFC1-08002BE10318";
                public const string COMPORT = "86E0D1E0-8089-11D0-9CE4-08003E301F73";
                public const string PARALLEL = "97F76EF0-F883-11D0-AF1F-0000F800845C";
            }
            /*public const string GUID_DEVINTERFACE_DISK = "53f56307-b6bf-11d0-94f2-00a0c91efb8b";
            public const string GUID_DEVINTERFACE_HUBCONTROLLER = "3abf6f2d-71c4-462a-8a92-1e6861e6af27";
            public const string GUID_DEVINTERFACE_MODEM = "2C7089AA-2E0E-11D1-B114-00C04FC2AAE4";
            public const string GUID_DEVINTERFACE_SERENUM_BUS_ENUMERATOR = "4D36E978-E325-11CE-BFC1-08002BE10318";
            public const string GUID_DEVINTERFACE_COMPORT = "86E0D1E0-8089-11D0-9CE4-08003E301F73";
            public const string GUID_DEVINTERFACE_PARALLEL = "97F76EF0-F883-11D0-AF1F-0000F800845C";*/
            // Win32 constants
            //private const int BROADCAST_QUERY_DENY = 0x424D5144;
            //public const int WM_DEVICECHANGE = 0x0219;

            [Flags]
            public enum DEVICE_NOTIFY : uint
            {
                DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000,
                DEVICE_NOTIFY_SERVICE_HANDLE = 0x00000001,
                DEVICE_NOTIFY_ALL_INTERFACE_CLASSES = 0x00000004
            }
            
            public enum DBTDEVICE : uint
            {
                DBT_DEVICEARRIVAL = 0x8000,                 //A device has been inserted and is now available. 
                DBT_DEVICEQUERYREMOVE = 0x8001,             //Permission to remove a device is requested. Any application can deny this request and cancel the removal.
                DBT_DEVICEQUERYREMOVEFAILED = 0x8002,       //Request to remove a device has been canceled.
                DBT_DEVICEREMOVEPENDING = 0x8003,           //Device is about to be removed. Cannot be denied.
                DBT_DEVICEREMOVECOMPLETE = 0x8004,          //Device has been removed.
                DBT_DEVICETYPESPECIFIC = 0x8005,            //Device-specific event.
                DBT_CUSTOMEVENT = 0x8006                    //User-defined event
            }

            public enum DBTDEVTYP: uint
            {
                DBT_DEVTYP_OEM = 0x00000000,                //OEM-defined device type
                DBT_DEVTYP_DEVNODE = 0x00000001,            //Devnode number
                DBT_DEVTYP_VOLUME = 0x00000002,             //Logical volume
                DBT_DEVTYP_PORT = 0x00000003,               //Serial, parallel
                DBT_DEVTYP_NET = 0x00000004,                //Network resource
                DBT_DEVTYP_DEVICEINTERFACE = 0x00000005,    //Device interface class
                DBT_DEVTYP_HANDLE = 0x00000006              //File system handle
            }

            /// <summary>
            /// Access rights for registry key objects.
            /// </summary>
            public enum REGKEYSECURITY: uint
            {
                /// <summary>
                /// Combines the STANDARD_RIGHTS_REQUIRED, KEY_QUERY_VALUE, KEY_SET_VALUE, KEY_CREATE_SUB_KEY, KEY_ENUMERATE_SUB_KEYS, KEY_NOTIFY, and KEY_CREATE_LINK access rights.
                /// </summary>
                KEY_ALL_ACCESS = 0xF003F,

                /// <summary>
                /// Reserved for system use.
                /// </summary>
                KEY_CREATE_LINK = 0x0020,

                /// <summary>
                /// Required to create a subkey of a registry key.
                /// </summary>
                KEY_CREATE_SUB_KEY = 0x0004,

                /// <summary>
                /// Required to enumerate the subkeys of a registry key.
                /// </summary>
                KEY_ENUMERATE_SUB_KEYS = 0x0008,

                /// <summary>
                /// Equivalent to KEY_READ.
                /// </summary>
                KEY_EXECUTE = 0x20019,

                /// <summary>
                /// Required to request change notifications for a registry key or for subkeys of a registry key.
                /// </summary>
                KEY_NOTIFY = 0x0010,

                /// <summary>
                /// Required to query the values of a registry key.
                /// </summary>
                KEY_QUERY_VALUE = 0x0001,

                /// <summary>
                /// Combines the STANDARD_RIGHTS_READ, KEY_QUERY_VALUE, KEY_ENUMERATE_SUB_KEYS, and KEY_NOTIFY values.
                /// </summary>
                KEY_READ = 0x20019,

                /// <summary>
                /// Required to create, delete, or set a registry value.
                /// </summary>
                KEY_SET_VALUE = 0x0002,

                /// <summary>
                /// Indicates that an application on 64-bit Windows should operate on the 32-bit registry view. For more information, see Accessing an Alternate Registry View. This flag must be combined using the OR operator with the other flags in this table that either query or access registry values. Windows 2000:  This flag is not supported.
                /// </summary>
                KEY_WOW64_32KEY = 0x0200,

                /// <summary>
                /// Indicates that an application on 64-bit Windows should operate on the 64-bit registry view. For more information, see Accessing an Alternate Registry View. This flag must be combined using the OR operator with the other flags in this table that either query or access registry values. Windows 2000:  This flag is not supported.
                /// </summary>
                KEY_WOW64_64KEY = 0x0100,

                /// <summary>
                /// Combines the STANDARD_RIGHTS_WRITE, KEY_SET_VALUE, and KEY_CREATE_SUB_KEY access rights.
                /// </summary>
                KEY_WRITE = 0x20006
            }
            
            
            /// <summary>
            /// Flags controlling what is included in the device information set built by SetupDiGetClassDevs
            /// </summary>
            [Flags]
            public enum DIGCF : int
            {
                DIGCF_DEFAULT = 0x00000001,    // only valid with DIGCF_DEVICEINTERFACE
                DIGCF_PRESENT = 0x00000002,
                DIGCF_ALLCLASSES = 0x00000004,
                DIGCF_PROFILE = 0x00000008,
                DIGCF_DEVICEINTERFACE = 0x00000010,
            }

            /// <summary>
            /// Values specifying the scope of a device property change.
            /// </summary>
            public enum DICS_FLAG : uint
            {
                /// <summary>
                /// Make change in all hardware profiles
                /// </summary>
                DICS_FLAG_GLOBAL = 0x00000001,

                /// <summary>
                /// Make change in specified profile only
                /// </summary>
                DICS_FLAG_CONFIGSPECIFIC = 0x00000002,

                /// <summary>
                /// 1 or more hardware profile-specific
                /// </summary>
                DICS_FLAG_CONFIGGENERAL = 0x00000004,
            }

            /// <summary>
            /// KeyType values for SetupDiCreateDevRegKey, SetupDiOpenDevRegKey, and SetupDiDeleteDevRegKey.
            /// </summary>
            public enum DIREG : uint
            {
                /// <summary>
                /// Open/Create/Delete device key
                /// </summary>
                DIREG_DEV = 0x00000001,

                /// <summary>
                /// Open/Create/Delete driver key
                /// </summary>
                DIREG_DRV = 0x00000002,

                /// <summary>
                /// Delete both driver and Device key
                /// </summary>
                DIREG_BOTH = 0x00000004,
            }

            public enum WinErrors : long
            {
                ERROR_SUCCESS = 0,
                ERROR_INVALID_FUNCTION = 1,
                ERROR_FILE_NOT_FOUND = 2,
                ERROR_PATH_NOT_FOUND = 3,
                ERROR_TOO_MANY_OPEN_FILES = 4,
                ERROR_ACCESS_DENIED = 5,
                ERROR_INVALID_HANDLE = 6,
                ERROR_ARENA_TRASHED = 7,
                ERROR_NOT_ENOUGH_MEMORY = 8,
                ERROR_INVALID_BLOCK = 9,
                ERROR_BAD_ENVIRONMENT = 10,
                ERROR_BAD_FORMAT = 11,
                ERROR_INVALID_ACCESS = 12,
                ERROR_INVALID_DATA = 13,
                ERROR_OUTOFMEMORY = 14,
                ERROR_INSUFFICIENT_BUFFER = 122,
                ERROR_MORE_DATA = 234,
                ERROR_NO_MORE_ITEMS = 259,
                ERROR_SERVICE_SPECIFIC_ERROR = 1066,
                ERROR_INVALID_USER_BUFFER = 1784
            }

            public enum CRErrorCodes
            {
                CR_SUCCESS = 0,
                CR_DEFAULT,
                CR_OUT_OF_MEMORY,
                CR_INVALID_POINTER,
                CR_INVALID_FLAG,
                CR_INVALID_DEVNODE,
                CR_INVALID_RES_DES,
                CR_INVALID_LOG_CONF,
                CR_INVALID_ARBITRATOR,
                CR_INVALID_NODELIST,
                CR_DEVNODE_HAS_REQS,
                CR_INVALID_RESOURCEID,
                CR_DLVXD_NOT_FOUND, //WIN 95 ONLY
                CR_NO_SUCH_DEVNODE,
                CR_NO_MORE_LOG_CONF,
                CR_NO_MORE_RES_DES,
                CR_ALREADY_SUCH_DEVNODE,
                CR_INVALID_RANGE_LIST,
                CR_INVALID_RANGE,
                CR_FAILURE,
                CR_NO_SUCH_LOGICAL_DEV,
                CR_CREATE_BLOCKED,
                CR_NOT_SYSTEM_VM, //WIN 95 ONLY
                CR_REMOVE_VETOED,
                CR_APM_VETOED,
                CR_INVALID_LOAD_TYPE,
                CR_BUFFER_SMALL,
                CR_NO_ARBITRATOR,
                CR_NO_REGISTRY_HANDLE,
                CR_REGISTRY_ERROR,
                CR_INVALID_DEVICE_ID,
                CR_INVALID_DATA,
                CR_INVALID_API,
                CR_DEVLOADER_NOT_READY,
                CR_NEED_RESTART,
                CR_NO_MORE_HW_PROFILES,
                CR_DEVICE_NOT_THERE,
                CR_NO_SUCH_VALUE,
                CR_WRONG_TYPE,
                CR_INVALID_PRIORITY,
                CR_NOT_DISABLEABLE,
                CR_FREE_RESOURCES,
                CR_QUERY_VETOED,
                CR_CANT_SHARE_IRQ,
                CR_NO_DEPENDENT,
                CR_SAME_RESOURCES,
                CR_NO_SUCH_REGISTRY_KEY,
                CR_INVALID_MACHINENAME, //NT ONLY
                CR_REMOTE_COMM_FAILURE, //NT ONLY
                CR_MACHINE_UNAVAILABLE, //NT ONLY
                CR_NO_CM_SERVICES, //NT ONLY
                CR_ACCESS_DENIED, //NT ONLY
                CR_CALL_NOT_IMPLEMENTED,
                CR_INVALID_PROPERTY,
                CR_DEVICE_INTERFACE_ACTIVE,
                CR_NO_SUCH_DEVICE_INTERFACE,
                CR_INVALID_REFERENCE_STRING,
                CR_INVALID_CONFLICT_LIST,
                CR_INVALID_INDEX,
                CR_INVALID_STRUCTURE_SIZE,
                NUM_CR_RESULTS
             }

            /// <summary>
            /// Device registry property codes
            /// </summary>
            public enum SPDRP : int
            {
                /// <summary>
                /// DeviceDesc (R/W)
                /// </summary>
                SPDRP_DEVICEDESC = 0x00000000,

                /// <summary>
                /// HardwareID (R/W)
                /// </summary>
                SPDRP_HARDWAREID = 0x00000001,

                /// <summary>
                /// CompatibleIDs (R/W)
                /// </summary>
                SPDRP_COMPATIBLEIDS = 0x00000002,

                /// <summary>
                /// unused
                /// </summary>
                SPDRP_UNUSED0 = 0x00000003,

                /// <summary>
                /// Service (R/W)
                /// </summary>
                SPDRP_SERVICE = 0x00000004,

                /// <summary>
                /// unused
                /// </summary>
                SPDRP_UNUSED1 = 0x00000005,

                /// <summary>
                /// unused
                /// </summary>
                SPDRP_UNUSED2 = 0x00000006,

                /// <summary>
                /// Class (R--tied to ClassGUID)
                /// </summary>
                SPDRP_CLASS = 0x00000007,

                /// <summary>
                /// ClassGUID (R/W)
                /// </summary>
                SPDRP_CLASSGUID = 0x00000008,

                /// <summary>
                /// Driver (R/W)
                /// </summary>
                SPDRP_DRIVER = 0x00000009,

                /// <summary>
                /// ConfigFlags (R/W)
                /// </summary>
                SPDRP_CONFIGFLAGS = 0x0000000A,

                /// <summary>
                /// Mfg (R/W)
                /// </summary>
                SPDRP_MFG = 0x0000000B,

                /// <summary>
                /// FriendlyName (R/W)
                /// </summary>
                SPDRP_FRIENDLYNAME = 0x0000000C,

                /// <summary>
                /// LocationInformation (R/W)
                /// </summary>
                SPDRP_LOCATION_INFORMATION = 0x0000000D,

                /// <summary>
                /// PhysicalDeviceObjectName (R)
                /// </summary>
                SPDRP_PHYSICAL_DEVICE_OBJECT_NAME = 0x0000000E,

                /// <summary>
                /// Capabilities (R)
                /// </summary>
                SPDRP_CAPABILITIES = 0x0000000F,

                /// <summary>
                /// UiNumber (R)
                /// </summary>
                SPDRP_UI_NUMBER = 0x00000010,

                /// <summary>
                /// UpperFilters (R/W)
                /// </summary>
                SPDRP_UPPERFILTERS = 0x00000011,

                /// <summary>
                /// LowerFilters (R/W)
                /// </summary>
                SPDRP_LOWERFILTERS = 0x00000012,

                /// <summary>
                /// BusTypeGUID (R)
                /// </summary>
                SPDRP_BUSTYPEGUID = 0x00000013,

                /// <summary>
                /// LegacyBusType (R)
                /// </summary>
                SPDRP_LEGACYBUSTYPE = 0x00000014,

                /// <summary>
                /// BusNumber (R)
                /// </summary>
                SPDRP_BUSNUMBER = 0x00000015,

                /// <summary>
                /// Enumerator Name (R)
                /// </summary>
                SPDRP_ENUMERATOR_NAME = 0x00000016,

                /// <summary>
                /// Security (R/W, binary form)
                /// </summary>
                SPDRP_SECURITY = 0x00000017,

                /// <summary>
                /// Security (W, SDS form)
                /// </summary>
                SPDRP_SECURITY_SDS = 0x00000018,

                /// <summary>
                /// Device Type (R/W)
                /// </summary>
                SPDRP_DEVTYPE = 0x00000019,

                /// <summary>
                /// Device is exclusive-access (R/W)
                /// </summary>
                SPDRP_EXCLUSIVE = 0x0000001A,

                /// <summary>
                /// Device Characteristics (R/W)
                /// </summary>
                SPDRP_CHARACTERISTICS = 0x0000001B,

                /// <summary>
                /// Device Address (R)
                /// </summary>
                SPDRP_ADDRESS = 0x0000001C,

                /// <summary>
                /// UiNumberDescFormat (R/W)
                /// </summary>
                SPDRP_UI_NUMBER_DESC_FORMAT = 0X0000001D,

                /// <summary>
                /// Device Power Data (R)
                /// </summary>
                SPDRP_DEVICE_POWER_DATA = 0x0000001E,

                /// <summary>
                /// Removal Policy (R)
                /// </summary>
                SPDRP_REMOVAL_POLICY = 0x0000001F,

                /// <summary>
                /// Hardware Removal Policy (R)
                /// </summary>
                SPDRP_REMOVAL_POLICY_HW_DEFAULT = 0x00000020,

                /// <summary>
                /// Removal Policy Override (RW)
                /// </summary>
                SPDRP_REMOVAL_POLICY_OVERRIDE = 0x00000021,

                /// <summary>
                /// Device Install State (R)
                /// </summary>
                SPDRP_INSTALL_STATE = 0x00000022,

                /// <summary>
                /// Device Location Paths (R)
                /// </summary>
                SPDRP_LOCATION_PATHS = 0x00000023,
            }            

            //pack=8 for 64 bit.
            [StructLayout(LayoutKind.Sequential, Pack=1)]
            public struct SP_DEVINFO_DATA
            {
                public UInt32 cbSize;
                public Guid ClassGuid;
                public UInt32 DevInst;
                public IntPtr Reserved;
            }

            [StructLayout(LayoutKind.Sequential, Pack=1)]
            public struct SP_DEVICE_INTERFACE_DATA
            {
                public  UInt32 cbSize;
                public  Guid interfaceClassGuid;
                public  UInt32 flags;
                private IntPtr reserved;
            }            

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct SP_DEVICE_INTERFACE_DETAIL_DATA
            {
                public UInt32 cbSize;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
                public string devicePath;
            }

            [StructLayout(LayoutKind.Explicit)]
            private struct DevBroadcastDeviceInterfaceBuffer
            {
                public DevBroadcastDeviceInterfaceBuffer(Int32 deviceType)
                {
                    dbch_size = Marshal.SizeOf(typeof(DevBroadcastDeviceInterfaceBuffer));
                    dbch_devicetype = deviceType;
                    dbch_reserved = 0;
                }

                [FieldOffset(0)]
                public Int32 dbch_size;
                [FieldOffset(4)]
                public Int32 dbch_devicetype;
                [FieldOffset(8)]
                public Int32 dbch_reserved;
            }

            //Structure with information for RegisterDeviceNotification.
            //DEV_BROADCAST_HDR Structure
            /*typedef struct _DEV_BROADCAST_HDR {
              DWORD dbch_size;
              DWORD dbch_devicetype;
              DWORD dbch_reserved;
            }DEV_BROADCAST_HDR, *PDEV_BROADCAST_HDR;*/
            [StructLayout(LayoutKind.Sequential)]
	        public struct DEV_BROADCAST_HDR
	        {
		        public int dbcc_size;
		        public int dbcc_devicetype;
		        public int dbcc_reserved;
	        }

            //DEV_BROADCAST_HANDLE Structure
                /*typedef struct _DEV_BROADCAST_HANDLE {
                  DWORD      dbch_size;
                  DWORD      dbch_devicetype;
                  DWORD      dbch_reserved;
                  HANDLE     dbch_handle;
                  HDEVNOTIFY dbch_hdevnotify;
                  GUID       dbch_eventguid;
                  LONG       dbch_nameoffset;
                  BYTE       dbch_data[1];
                }DEV_BROADCAST_HANDLE *PDEV_BROADCAST_HANDLE;*/
            [StructLayout(LayoutKind.Sequential)]
            public struct DEV_BROADCAST_HANDLE
            {
                public Int32 dbch_size;
                public Int32 dbch_devicetype;
                public Int32 dbch_reserved;
                public IntPtr dbch_handle;
                public IntPtr dbch_hdevnotify;
                public Guid dbch_eventguid;
                public long dbch_nameoffset;
                public byte dbch_data;
                public byte dbch_data1;
            }

            //DEV_BROADCAST_DEVICEINTERFACE Structure
		        /*typedef struct _DEV_BROADCAST_DEVICEINTERFACE {
                  DWORD dbcc_size;
                  DWORD dbcc_devicetype;
                  DWORD dbcc_reserved;
                  GUID  dbcc_classguid;
                  TCHAR dbcc_name[1];
                }DEV_BROADCAST_DEVICEINTERFACE *PDEV_BROADCAST_DEVICEINTERFACE;*/
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		    public struct DEV_BROADCAST_DEVICEINTERFACE
		    {
			    public Int32 dbcc_size;
			    public Int32 dbcc_devicetype;
			    public Int32 dbcc_reserved;
			    [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 16)]
			    public byte[] dbcc_classguid;
			    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
			    public char[] dbcc_name;
		    }

            //DEV_BROADCAST_VOLUME Structure
                /*typedef struct _DEV_BROADCAST_VOLUME {
                  DWORD dbcv_size;
                  DWORD dbcv_devicetype;
                  DWORD dbcv_reserved;
                  DWORD dbcv_unitmask;
                  WORD  dbcv_flags;
                }DEV_BROADCAST_VOLUME, *PDEV_BROADCAST_VOLUME;*/
            [StructLayout(LayoutKind.Sequential)]
            public struct DEV_BROADCAST_VOLUME
            {
                public Int32 dbcv_size;
                public Int32 dbcv_devicetype;
                public Int32 dbcv_reserved;
                public Int32 dbcv_unitmask;
                public Int16 dbcv_flags;
            }

            //DEV_BROADCAST_PORT Structure
                /*typedef struct _DEV_BROADCAST_PORT {
                  DWORD dbcp_size;
                  DWORD dbcp_devicetype;
                  DWORD dbcp_reserved;
                  TCHAR dbcp_name[1];
                }DEV_BROADCAST_PORT *PDEV_BROADCAST_PORT;*/
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct DEV_BROADCAST_PORT
            {
                public Int32 dbcp_size;
                public Int32 dbcp_devicetype;
                public Int32 dbcp_reserved;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
                public char[] dbcp_name;
            }

            //DEV_BROADCAST_OEM Structure
                /*typedef struct _DEV_BROADCAST_OEM {
                  DWORD dbco_size;
                  DWORD dbco_devicetype;
                  DWORD dbco_reserved;
                  DWORD dbco_identifier;
                  DWORD dbco_suppfunc;
                }DEV_BROADCAST_OEM, *PDEV_BROADCAST_OEM;*/
            [StructLayout(LayoutKind.Sequential)]
            public struct DEV_BROADCAST_OEM
            {
                public Int32 dbco_size;
                public Int32 dbco_devicetype;
                public Int32 dbco_reserved;
                public Int32 dbco_identifier;
                public Int32 dbco_suppfunc;
            }

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr RegisterDeviceNotification(IntPtr hRecipient, IntPtr NotificationFilter, Int32 Flags);
            
            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool UnregisterDeviceNotification(IntPtr hHandle);

            /// <summary>
            /// The SetupDiEnumDeviceInfo function retrieves a context structure for a device information element of the specified
            /// device information set. Each call returns information about one device. The function can be called repeatedly
            /// to get information about several devices.
            /// </summary>
            /// <param name="DeviceInfoSet">A handle to the device information set for which to return an SP_DEVINFO_DATA structure that represents a device information element.</param>
            /// <param name="MemberIndex">A zero-based index of the device information element to retrieve.</param>
            /// <param name="DeviceInfoData">A pointer to an SP_DEVINFO_DATA structure to receive information about an enumerated device information element. The caller must set DeviceInfoData.cbSize to sizeof(SP_DEVINFO_DATA).</param>
            /// <returns></returns>
            [DllImport("setupapi.dll", SetLastError = true)]
            public static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, UInt32 MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

            /// <summary>
            /// A call to SetupDiEnumDeviceInterfaces retrieves a pointer to a structure that identifies a specific device interface
            /// in the previously retrieved DeviceInfoSet array. The call specifies a device interface by passing an array index.
            /// To retrieve information about all of the device interfaces, an application can loop through the array,
            /// incrementing the array index until the function returns zero, indicating that there are no more interfaces.
            /// The GetLastError API function then returns No more data is available.
            /// </summary>
            /// <param name="hDevInfo">Input: Give it the HDEVINFO we got from SetupDiGetClassDevs()</param>
            /// <param name="devInfo">Input (optional)</param>
            /// <param name="interfaceClassGuid">Input</param>
            /// <param name="memberIndex">Input: "Index" of the device you are interested in getting the path for.</param>
            /// <param name="deviceInterfaceData">Output: This function fills in an "SP_DEVICE_INTERFACE_DATA" structure.</param>
            /// <returns></returns>
            [DllImport(@"setupapi.dll", CharSet=CharSet.Auto, SetLastError = true)]
            public static extern Boolean SetupDiEnumDeviceInterfaces(
               IntPtr hDevInfo,                                         
               IntPtr devInfo,                             
               ref Guid interfaceClassGuid, //ref
               UInt32 memberIndex,                                      
               ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData         
            );
            
            /// <summary>
            /// Gives us a device path, which is needed before CreateFile() can be used.
            /// </summary>
            /// <param name="hDevInfo">Input: Wants HDEVINFO which can be obtained from SetupDiGetClassDevs()</param>
            /// <param name="deviceInterfaceData">Input: Pointer to a structure which defines the device interface.</param>
            /// <param name="deviceInterfaceDetailData">Output: Pointer to a structure, which will contain the device path.</param>
            /// <param name="deviceInterfaceDetailDataSize">Input: Number of bytes to retrieve.</param>
            /// <param name="requiredSize">Output (optional): The number of bytes needed to hold the entire struct</param>
            /// <param name="deviceInfoData">Output</param>
            /// <returns></returns>
            [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern Boolean SetupDiGetDeviceInterfaceDetail(
               IntPtr hDevInfo, 
               ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, //ref
               ref SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData,
               UInt32 deviceInterfaceDetailDataSize,                         
               out UInt32 requiredSize,                                      
               ref SP_DEVINFO_DATA deviceInfoData                            
            );
            
            /// <summary>
            /// Frees up memory by destroying a DeviceInfoList
            /// </summary>
            /// <param name="hDevInfo"></param>
            /// <returns></returns>
            [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern bool SetupDiDestroyDeviceInfoList(IntPtr hDevInfo); //Input: Give it a handle to a device info list to deallocate from RAM.

            /// <summary>
            /// Returns a HDEVINFO type for a device information set.
            /// We will need the HDEVINFO as in input parameter for calling many of the other SetupDixxx() functions.
            /// </summary>
            /// <param name="ClassGuid"></param>
            /// <param name="Enumerator"></param>
            /// <param name="hwndParent"></param>
            /// <param name="Flags"></param>
            /// <returns></returns>
            [DllImport("setupapi.dll", CharSet = CharSet.Auto)]     // 1st form using a ClassGUID
            public static extern IntPtr SetupDiGetClassDevs(
               ref Guid ClassGuid, //ref
               IntPtr Enumerator,
               IntPtr hwndParent,
               UInt32 Flags
            );
            [DllImport("setupapi.dll", CharSet = CharSet.Auto)]     // 2nd form uses an Enumerator
            public static extern IntPtr SetupDiGetClassDevs(
               IntPtr ClassGuid,
               string Enumerator,
               IntPtr hwndParent,
               int Flags
            );            
            /// <summary>
            /// The SetupDiGetDeviceRegistryProperty function retrieves the specified device property.
            /// This handle is typically returned by the SetupDiGetClassDevs or SetupDiGetClassDevsEx function.
            /// </summary>
            /// <param Name="DeviceInfoSet">Handle to the device information set that contains the interface and its underlying device.</param>
            /// <param Name="DeviceInfoData">Pointer to an SP_DEVINFO_DATA structure that defines the device instance.</param>
            /// <param Name="Property">Device property to be retrieved. SEE MSDN</param>
            /// <param Name="PropertyRegDataType">Pointer to a variable that receives the registry data Type. This parameter can be NULL.</param>
            /// <param Name="PropertyBuffer">Pointer to a buffer that receives the requested device property.</param>
            /// <param Name="PropertyBufferSize">Size of the buffer, in bytes.</param>
            /// <param Name="RequiredSize">Pointer to a variable that receives the required buffer size, in bytes. This parameter can be NULL.</param>
            /// <returns>If the function succeeds, the return value is nonzero.</returns>
            [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern bool SetupDiGetDeviceRegistryProperty(
                IntPtr DeviceInfoSet,
                ref SP_DEVINFO_DATA DeviceInfoData, //ref
                UInt32 Property,
                ref UInt32 PropertyRegDataType,
                IntPtr PropertyBuffer,
                UInt32 PropertyBufferSize,
                ref UInt32 RequiredSize
            );

            /// <summary>
            /// The CM_Get_Parent function obtains a device instance handle to the parent node of a specified device node, in the local machine's device tree.
            /// </summary>
            /// <param name="pdnDevInst">Caller-supplied pointer to the device instance handle to the parent node that this function retrieves. The retrieved handle is bound to the local machine.</param>
            /// <param name="dnDevInst">Caller-supplied device instance handle that is bound to the local machine.</param>
            /// <param name="ulFlags">Not used, must be zero.</param>
            /// <returns>If the operation succeeds, the function returns CR_SUCCESS. Otherwise, it returns one of the CR_-prefixed error codes defined in cfgmgr32.h.</returns>
            [DllImport("setupapi.dll")]
            public static extern int CM_Get_Parent(
               out UInt32 pdnDevInst,
               UInt32 dnDevInst,
               int ulFlags
            );

            /// <summary>
            /// The CM_Get_Device_ID function retrieves the device instance ID for a specified device instance, on the local machine.
            /// </summary>
            /// <param name="dnDevInst">Caller-supplied device instance handle that is bound to the local machine.</param>
            /// <param name="Buffer">Address of a buffer to receive a device instance ID string. The required buffer size can be obtained by calling CM_Get_Device_ID_Size, then incrementing the received value to allow room for the string's terminating NULL.</param>
            /// <param name="BufferLen">Caller-supplied length, in characters, of the buffer specified by Buffer.</param>
            /// <param name="ulFlags">Not used, must be zero.</param>
            /// <returns>If the operation succeeds, the function returns CR_SUCCESS. Otherwise, it returns one of the CR_-prefixed error codes defined in cfgmgr32.h.</returns>
            [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
            public static extern int CM_Get_Device_ID(
               UInt32 dnDevInst,
               IntPtr Buffer,
               int BufferLen,
               int ulFlags
            );

            /// <summary>
            /// The SetupDiOpenDevRegKey function opens a registry key for device-specific configuration information.
            /// </summary>
            /// <param name="hDeviceInfoSet">A handle to the device information set that contains a device information element that represents the device for which to open a registry key.</param>
            /// <param name="DeviceInfoData">A pointer to an SP_DEVINFO_DATA structure that specifies the device information element in DeviceInfoSet.</param>
            /// <param name="Scope">The scope of the registry key to open. The scope determines where the information is stored. The scope can be global or specific to a hardware profile. The scope is specified by one of the following values:
            /// DICS_FLAG_GLOBAL Open a key to store global configuration information. This information is not specific to a particular hardware profile. For NT-based operating systems this opens a key that is rooted at HKEY_LOCAL_MACHINE. The exact key opened depends on the value of the KeyType parameter.
            /// DICS_FLAG_CONFIGSPECIFIC Open a key to store hardware profile-specific configuration information. This key is rooted at one of the hardware-profile specific branches, instead of HKEY_LOCAL_MACHINE. The exact key opened depends on the value of the KeyType parameter.</param>
            /// <param name="HwProfile">A hardware profile value, which is set as follows:
            /// If Scope is set to DICS_FLAG_CONFIGSPECIFIC, HwProfile specifies the hardware profile of the key that is to be opened.
            /// If HwProfile is 0, the key for the current hardware profile is opened.
            /// If Scope is DICS_FLAG_GLOBAL, HwProfile is ignored.</param>
            /// <param name="KeyType">The type of registry storage key to open, which can be one of the following values:
            /// DIREG_DEV Open a hardware key for the device.
            /// DIREG_DRV Open a software key for the device.
            /// For more information about a device's hardware and software keys, see Driver Information in the Registry.</param>
            /// <param name="samDesired">The registry security access that is required for the requested key. For information about registry security access values of type REGSAM, see the Microsoft Windows SDK documentation.</param>
            /// <returns>If the function is successful, it returns a handle to an opened registry key where private configuration data pertaining to this device instance can be stored/retrieved.
            /// If the function fails, it returns INVALID_HANDLE_VALUE. To get extended error information, call GetLastError.</returns>
            [DllImport("Setupapi", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr SetupDiOpenDevRegKey(
                IntPtr hDeviceInfoSet,
                ref SP_DEVINFO_DATA DeviceInfoData,
                UInt32 Scope,
                UInt32 HwProfile,
                UInt32 KeyType,
                UInt32 samDesired);

            /// <summary>
            /// Retrieves the type and data for the specified value name associated with an open registry key.
            /// </summary>
            /// <param name="hKey">A handle to an open registry key. The key must have been opened with the KEY_QUERY_VALUE access right.</param>
            /// <param name="lpValueName">The name of the registry value.
            /// If lpValueName is NULL or an empty string, "", the function retrieves the type and data for the key's unnamed or default value, if any.
            /// If lpValueName specifies a key that is not in the registry, the function returns ERROR_FILE_NOT_FOUND.</param>
            /// <param name="lpReserved">This parameter is reserved and must be NULL.</param>
            /// <param name="lpType">A pointer to a variable that receives a code indicating the type of data stored in the specified value. The lpType parameter can be NULL if the type code is not required.</param>
            /// <param name="lpData">A pointer to a buffer that receives the value's data. This parameter can be NULL if the data is not required.</param>
            /// <param name="lpcbData">A pointer to a variable that specifies the size of the buffer pointed to by the lpData parameter, in bytes. When the function returns, this variable contains the size of the data copied to lpData.
            /// The lpcbData parameter can be NULL only if lpData is NULL.
            /// If the data has the REG_SZ, REG_MULTI_SZ or REG_EXPAND_SZ type, this size includes any terminating null character or characters unless the data was stored without them. For more information, see Remarks.
            /// If the buffer specified by lpData parameter is not large enough to hold the data, the function returns ERROR_MORE_DATA and stores the required buffer size in the variable pointed to by lpcbData. In this case, the contents of the lpData buffer are undefined.
            /// If lpData is NULL, and lpcbData is non-NULL, the function returns ERROR_SUCCESS and stores the size of the data, in bytes, in the variable pointed to by lpcbData. This enables an application to determine the best way to allocate a buffer for the value's data.If hKey specifies HKEY_PERFORMANCE_DATA and the lpData buffer is not large enough to contain all of the returned data, RegQueryValueEx returns ERROR_MORE_DATA and the value returned through the lpcbData parameter is undefined. This is because the size of the performance data can change from one call to the next. In this case, you must increase the buffer size and call RegQueryValueEx again passing the updated buffer size in the lpcbData parameter. Repeat this until the function succeeds. You need to maintain a separate variable to keep track of the buffer size, because the value returned by lpcbData is unpredictable.
            /// If the lpValueName registry value does not exist, RegQueryValueEx returns ERROR_FILE_NOT_FOUND and the value returned through the lpcbData parameter is undefined.</param>
            /// <returns>If the function succeeds, the return value is ERROR_SUCCESS.
            /// If the function fails, the return value is a system error code.
            /// If the lpData buffer is too small to receive the data, the function returns ERROR_MORE_DATA.
            /// If the lpValueName registry value does not exist, the function returns ERROR_FILE_NOT_FOUND.</returns>
            [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegQueryValueExW", SetLastError = true)]
            public static extern int RegQueryValueEx(
                IntPtr hKey,
                string lpValueName,
                UInt32 lpReserved,
                out UInt32 lpType,
                System.Text.StringBuilder lpData,
                ref UInt32 lpcbData);

            /// <summary>
            /// Closes a handle to the specified registry key.
            /// </summary>
            /// <param name="hKey">A handle to the open key to be closed.</param>
            /// <returns>If the function succeeds, the return value is ERROR_SUCCESS.
            /// If the function fails, the return value is a nonzero error code defined in Winerror.h.</returns>
            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern int RegCloseKey(
                IntPtr hKey);


        }

        
}
