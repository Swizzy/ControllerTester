using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace HidLibrary {

    public class HidDevice : IDisposable {
        public delegate void InsertedEventHandler();

        public delegate void RemovedEventHandler();

        public enum ReadStatus {
            Success = 0,
            WaitTimedOut = 1,
            WaitFail = 2,
            NoDataRead = 3,
            ReadError = 4,
            NotConnected = 5
        }

        private readonly string _description;
        private readonly HidDeviceAttributes _deviceAttributes;
        private readonly HidDeviceCapabilities _deviceCapabilities;
        private readonly HidDeviceEventMonitor _deviceEventMonitor;
        private readonly string _devicePath;
        private bool _monitorDeviceEvents;
        private byte _idleTicks;

        internal HidDevice(string devicePath, string description = null) {
            _deviceEventMonitor = new HidDeviceEventMonitor(this);
            _deviceEventMonitor.Removed += DeviceEventMonitorRemoved;

            _devicePath = devicePath;
            _description = description;

            try {
                var hidHandle = OpenHandle(_devicePath, false);

                _deviceAttributes = GetDeviceAttributes(hidHandle);
                _deviceCapabilities = GetDeviceCapabilities(hidHandle);

                hidHandle.Close();
            }
            catch(Exception exception) {
                Console.WriteLine(exception.Message);
                throw new Exception(string.Format("Error querying HID device '{0}'.", devicePath), exception);
            }
        }

        public SafeFileHandle SafeReadHandle { get; private set; }
        public FileStream FileStream { get; private set; }
        public bool IsOpen { get; private set; }
        public bool IsConnected { get { return HidDevices.IsConnected(_devicePath); } }
        public bool IsTimedOut { get { return _idleTicks > 5; } }
        public string Description { get { return _description; } }
        public HidDeviceCapabilities Capabilities { get { return _deviceCapabilities; } }
        public HidDeviceAttributes Attributes { get { return _deviceAttributes; } }
        public string DevicePath { get { return _devicePath; } }

        public bool MonitorDeviceEvents {
            get { return _monitorDeviceEvents; }
            set {
                if(value & _monitorDeviceEvents == false)
                    _deviceEventMonitor.Init();
                _monitorDeviceEvents = value;
            }
        }

        public void Dispose() {
            if(MonitorDeviceEvents)
                MonitorDeviceEvents = false;
            if(IsOpen)
                CloseDevice();
        }

        public event RemovedEventHandler Removed;
        public event EventHandler<EventArgs> Remove;

        public override string ToString() {
            return string.Format(
                                 "VendorID={0}, ProductID={1}, Version={2}, DevicePath={3}",
                                 _deviceAttributes.VendorHexId, _deviceAttributes.ProductHexId,
                                 _deviceAttributes.Version, _devicePath);
        }

        public void OpenDevice(bool isExclusive) {
            if(IsOpen)
                return;
            try {
                if(SafeReadHandle == null)
                    SafeReadHandle = OpenHandle(_devicePath, isExclusive);
            }
            catch(Exception exception) {
                IsOpen = false;
                throw new Exception("Error opening HID device.", exception);
            }

            IsOpen = !SafeReadHandle.IsInvalid;
        }

        public void CloseDevice() {
            if(!IsOpen)
                return;
            CloseFileStreamIo();

            IsOpen = false;
        }

        public bool ReadInputReport(byte[] data) {
            if(SafeReadHandle == null)
                SafeReadHandle = OpenHandle(_devicePath, true);
            return NativeMethods.HidD_GetInputReport(SafeReadHandle, data, data.Length);
        }

        private static HidDeviceAttributes GetDeviceAttributes(SafeFileHandle hidHandle) {
            var deviceAttributes = default(NativeMethods.HiddAttributes);
            deviceAttributes.Size = Marshal.SizeOf(deviceAttributes);
            NativeMethods.HidD_GetAttributes(hidHandle, ref deviceAttributes);
            return new HidDeviceAttributes(deviceAttributes);
        }

        private static HidDeviceCapabilities GetDeviceCapabilities(SafeFileHandle hidHandle) {
            var capabilities = default(NativeMethods.HidpCaps);
            var preparsedDataPointer = default(IntPtr);

            if(!NativeMethods.HidD_GetPreparsedData(hidHandle, ref preparsedDataPointer))
                return new HidDeviceCapabilities(capabilities);
            NativeMethods.HidP_GetCaps(preparsedDataPointer, ref capabilities);
            NativeMethods.HidD_FreePreparsedData(preparsedDataPointer);
            return new HidDeviceCapabilities(capabilities);
        }

        private void CloseFileStreamIo() {
            if(FileStream != null)
                FileStream.Close();
            FileStream = null;
            if(SafeReadHandle != null && !SafeReadHandle.IsInvalid)
                SafeReadHandle.Close();
            SafeReadHandle = null;
        }

        private void DeviceEventMonitorRemoved() {
            if(IsOpen) {
                lock(this)
                    _idleTicks = 100;
                MonitorDeviceEvents = false;
                NativeMethods.CancelIoEx(SafeReadHandle, IntPtr.Zero);
                CloseDevice();
            }
            if(Removed != null)
                Removed();
            if(Remove != null)
                Remove(this, new EventArgs());
        }

        public void Tick() {
            lock(this) {
                _idleTicks++;
            }
        }

        public void FlushQueue() {
            if(SafeReadHandle != null)
                NativeMethods.HidD_FlushQueue(SafeReadHandle);
        }

        private ReadStatus ReadWithFileStreamTask(byte[] inputBuffer) {
            try {
                if(FileStream.Read(inputBuffer, 0, inputBuffer.Length) > 0)
                    return ReadStatus.Success;
                return ReadStatus.NoDataRead;
            }
            catch(Exception) {
                return ReadStatus.ReadError;
            }
        }

        public ReadStatus ReadFile(byte[] inputBuffer) {
            if(SafeReadHandle == null)
                SafeReadHandle = OpenHandle(_devicePath, true);
            try {
                uint bytesRead;
                lock(this) {
                    _idleTicks = 0;
                }
                if(NativeMethods.ReadFile(
                                          SafeReadHandle, inputBuffer, (uint)inputBuffer.Length,
                                          out bytesRead, IntPtr.Zero))
                    return ReadStatus.Success;
                return ReadStatus.NoDataRead;
            }
            catch(Exception) {
                return ReadStatus.ReadError;
            }
        }

        public ReadStatus ReadWithFileStream(byte[] inputBuffer, int timeout) {
            try {
                if(SafeReadHandle == null)
                    SafeReadHandle = OpenHandle(_devicePath, true);
                if(FileStream == null && !SafeReadHandle.IsInvalid)
                    FileStream = new FileStream(SafeReadHandle, FileAccess.ReadWrite, inputBuffer.Length, false);
                if(!SafeReadHandle.IsInvalid && FileStream.CanRead) {
                    var readFileTask = new Task<ReadStatus>(() => ReadWithFileStreamTask(inputBuffer));
                    readFileTask.Start();
                    var success = readFileTask.Wait(timeout);
                    if(success) {
                        if(readFileTask.Result == ReadStatus.Success)
                            return ReadStatus.Success;
                        if(readFileTask.Result == ReadStatus.ReadError)
                            return ReadStatus.ReadError;
                        if(readFileTask.Result == ReadStatus.NoDataRead)
                            return ReadStatus.NoDataRead;
                    }
                    else
                        return ReadStatus.WaitTimedOut;
                }
            }
            catch(Exception e) {
                if(e is AggregateException) {
                    Console.WriteLine(e.Message);
                    return ReadStatus.WaitFail;
                }
                return ReadStatus.ReadError;
            }

            return ReadStatus.ReadError;
        }

        public bool WriteOutputReportViaControl(byte[] outputBuffer) {
            if(SafeReadHandle == null)
                SafeReadHandle = OpenHandle(_devicePath, true);

            if(NativeMethods.HidD_SetOutputReport(SafeReadHandle, outputBuffer, outputBuffer.Length))
                return true;
            return false;
        }

        public bool WriteOutputReportViaInterrupt(byte[] outputBuffer, int timeout) {
            try {
                if(SafeReadHandle == null)
                    SafeReadHandle = OpenHandle(_devicePath, true);
                if(FileStream == null && !SafeReadHandle.IsInvalid)
                    FileStream = new FileStream(SafeReadHandle, FileAccess.ReadWrite, outputBuffer.Length, false);
                if(FileStream == null || !FileStream.CanWrite || SafeReadHandle.IsInvalid)
                    return false;
                FileStream.Write(outputBuffer, 0, outputBuffer.Length);
                return true;
            }
            catch(Exception) {
                return false;
            }
        }

        private SafeFileHandle OpenHandle(string devicePathName, bool isExclusive) {
            return isExclusive
                       ? NativeMethods.CreateFile(
                                                  devicePathName,
                                                  NativeMethods.GenericRead | NativeMethods.GenericWrite, 0,
                                                  IntPtr.Zero, NativeMethods.OpenExisting, 0, 0)
                       : NativeMethods.CreateFile(
                                                  devicePathName,
                                                  NativeMethods.GenericRead | NativeMethods.GenericWrite,
                                                  NativeMethods.FileShareRead | NativeMethods.FileShareWrite,
                                                  IntPtr.Zero, NativeMethods.OpenExisting, 0, 0);
        }

        public bool ReadFeatureData(byte[] inputBuffer) {
            return NativeMethods.HidD_GetFeature(SafeReadHandle, inputBuffer, inputBuffer.Length);
        }

        public string ReadSerial() {
            if(Capabilities.InputReportByteLength == 64) {
                var buffer = new byte[16];
                buffer[0] = 18;
                ReadFeatureData(buffer);
                return string.Format(
                                     "{0:X02}:{1:X02}:{2:X02}:{3:X02}:{4:X02}:{5:X02}", buffer[6], buffer[5], buffer[4],
                                     buffer[3], buffer[2], buffer[1]);
            }
            else {
                var buffer = new byte[126];
                NativeMethods.HidD_GetSerialNumberString(SafeReadHandle, buffer, (ulong)buffer.Length);
                var macAddr = Encoding.Unicode.GetString(buffer).Replace("\0", string.Empty).ToUpper();
                macAddr = string.Format(
                                        "{0}{1}:{2}{3}:{4}{5}:{6}{7}:{8}{9}:{10}{11}", macAddr[0], macAddr[1],
                                        macAddr[2], macAddr[3], macAddr[4], macAddr[5], macAddr[6], macAddr[7],
                                        macAddr[8], macAddr[9], macAddr[10], macAddr[11]);
                return macAddr;
            }
        }
    }

}