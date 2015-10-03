using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media;
using HidLibrary;

namespace ControllerTester.Backend {

    internal class Dualshock4 {
        private static Dualshock4Controller[] _controllers;
        private static int _index;

        public static Dualshock4Controller[] GetControllers() {
            var ret = new List<Dualshock4Controller>();
            try {
                var devs = HidDevices.Enumerate(0x054C, 0x05C4, 0x05C5, 0x06C4);
                foreach(var dev in devs) {
                    dev.OpenDevice(false);
                    if(!dev.IsOpen)
                        continue;
                    var isFound = false;
                    var dp = dev.DevicePath;
                    if(_controllers != null) {
                        foreach(var ds4 in _controllers.Where(ds4 => ds4.DevicePath == dp)) {
                            isFound = true;
                            if(ds4.IsConnected)
                                ret.Add(ds4);
                            break;
                        }
                    }
                    if(isFound)
                        continue;
                    ret.Add(new Dualshock4Controller(dev, _index++));
                    if(!ret[ret.Count - 1].IsConnected)
                        ret.RemoveAt(ret.Count - 1);
                }
            }
            catch(Exception ex) {
                Console.WriteLine(ex);
            }
            return _controllers = ret.ToArray();
        }

        public class Dualshock4Controller {
            private readonly string _addr;
            private readonly BacklightLedColor _backlightLedColor = new BacklightLedColor(0, 0, 0xFF);
            private readonly int _id;
            private readonly bool _isUsb;
            private readonly byte[] _outputBuffer;
            public readonly HidDevice Device;
            private bool _isDirty = true, _disconnecting;
            private ControllerState _lastState;
            private RumbleStrength _rumble;
            public EventHandler<Ds4UpdateEventArgs> Updated;

            public Dualshock4Controller(HidDevice device, int id) {
                _rumble = new RumbleStrength();
                Device = device;
                Device.MonitorDeviceEvents = true;
                _id = id;
                _isUsb = Device.Capabilities.InputReportByteLength == 64;
                _addr = Device.ReadSerial();
                _outputBuffer = _isUsb ? new byte[Device.Capabilities.OutputReportByteLength] : new byte[78];
                Run();
            }

            public Color BacklightColor {
                get { return _backlightLedColor.Color; }
                set {
                    _backlightLedColor.Color = value;
                    _isDirty = true;
                }
            }

            public RumbleStrength Rumble {
                get { return _rumble; }
                set {
                    _rumble = value;
                    _isDirty = true;
                }
            }

            public string DevicePath { get { return Device.DevicePath; } }

            public bool IsConnected {
                get {
                    if(_isUsb)
                        return Device.IsConnected;
                    return Device.IsConnected && !_disconnecting;
                }
            }

            private void Run() {
                if(!Device.IsConnected)
                    return;
                var bw = new BackgroundWorker();
                bw.DoWork += WorkerThread;
                bw.RunWorkerCompleted += (sender, args) => {
                                             Device.CloseDevice();
                                             if(!_isUsb)
                                                 DisconnectBt();
                                         };
                SendOutputReport();
                bw.RunWorkerAsync();
            }

            private void WorkerThread(object sender, DoWorkEventArgs doWorkEventArgs) {
                while(Device.IsConnected && !Device.IsTimedOut) {
                    try {
                        SendOutputReport();
                        var handler = Updated;
                        var data = new byte[Device.Capabilities.InputReportByteLength];
                        if(Device.ReadFile(data) != HidDevice.ReadStatus.Success)
                            continue;
                        Device.FlushQueue();
                        if(handler != null)
                            handler(this, new Ds4UpdateEventArgs(_lastState = new ControllerState(data, _isUsb)));
                        CheckQuickDisconnect();
                    }
                    catch(Exception ex) {
                        Console.WriteLine(ex);
                    }
                }
            }

            private void CheckQuickDisconnect() {
                if(_lastState == null || _disconnecting || _isUsb || !_lastState.Options || !_lastState.PlaystationButton)
                    return;
                DisconnectBt();
            }

            private void DisconnectBt() {
                if(_addr == null || _disconnecting)
                    return;
                _disconnecting = true;
                Console.WriteLine("Disconnecting BT device: {0}", _addr);
                var btaddr = new byte[8];
                var sbytes = _addr.Split(':');
                for(var i = 0; i < 6; i++)
                    btaddr[5 - i] = Convert.ToByte(sbytes[i], 16);
                var lbtAddr = BitConverter.ToInt64(btaddr, 0);
                var p = new Bluetooth.BluetoothFindRadioParams {
                                                                   dwSize = Marshal.SizeOf(typeof(Bluetooth.BluetoothFindRadioParams))
                                                               };
                var btHandle = IntPtr.Zero;
                var searchHandle = Bluetooth.BluetoothFindFirstRadio(ref p, ref btHandle);
                var success = false;
                while(!success && btHandle != IntPtr.Zero) {
                    var bytesReturned = 0;
                    success = Bluetooth.DeviceIoControl(btHandle, Bluetooth.IoctlBthDisconnectDevice, ref lbtAddr, 8, IntPtr.Zero, 0, ref bytesReturned, IntPtr.Zero);
                    Bluetooth.CloseHandle(btHandle);
                    if(!success && !Bluetooth.BluetoothFindNextRadio(searchHandle, ref btHandle))
                        btHandle = IntPtr.Zero;
                }
                Bluetooth.BluetoothFindRadioClose(searchHandle);
                Console.WriteLine("Success? {0}", success);
            }

            private void SendOutputReport() {
                if(!_isDirty)
                    return;
                var offset = 4;
                if(!_isUsb)
                    offset = 6;
                _outputBuffer[offset] = Rumble.SmallMotor;
                _outputBuffer[offset + 1] = Rumble.BigMotor;
                _outputBuffer[offset + 2] = _backlightLedColor.Red;
                _outputBuffer[offset + 3] = _backlightLedColor.Green;
                _outputBuffer[offset + 4] = _backlightLedColor.Blue;
                //_outputBuffer[offset + 5] = 0x00; // Flash On
                //_outputBuffer[offset + 6] = 0x00; // Flash Off
                if(_isUsb) {
                    _outputBuffer[0] = 0x05;
                    _outputBuffer[1] = 0xFF;
                    if(Device.WriteOutputReportViaInterrupt(_outputBuffer, 8))
                        _isDirty = false;
                }
                else {
                    _outputBuffer[0] = 0x11;
                    _outputBuffer[1] = 0x80;
                    _outputBuffer[3] = 0x03;
                    if(Device.WriteOutputReportViaControl(_outputBuffer))
                        _isDirty = false;
                }
            }

            public override string ToString() { return string.Format("Controller {0} ({1}) MAC: {2}", _id, _isUsb ? "USB" : "BT", _addr); }
        }

        public class RumbleStrength {
            public byte BigMotor;
            public byte SmallMotor;

            public RumbleStrength() {
                SmallMotor = 0;
                BigMotor = 0;
            }

            public RumbleStrength(byte small, byte big) {
                SmallMotor = small;
                BigMotor = big;
            }
        }

        public class BacklightLedColor {
            public byte Blue;
            public byte Green;
            public byte Red;

            public BacklightLedColor(byte r, byte g, byte b) {
                Red = r;
                Green = g;
                Blue = b;
            }

            public Color Color {
                get { return Color.FromArgb(255, Red, Green, Blue); }
                set {
                    Red = value.R;
                    Green = value.G;
                    Blue = value.B;
                }
            }
        }

        public class ControllerState {
            public ControllerState(byte[] data, bool isUsb) {
                IsOk = false;
                var offset = 0;
                if(!isUsb)
                    offset += 2;
                LeftX = data[offset + 1];
                LeftY = data[offset + 2];
                RightX = data[offset + 3];
                RightY = data[offset + 4];
                var main = (MainButtons)data[offset + 5];
                Triangle = (main & MainButtons.Triangle) == MainButtons.Triangle;
                Circle = (main & MainButtons.Circle) == MainButtons.Circle;
                Cross = (main & MainButtons.Cross) == MainButtons.Cross;
                Square = (main & MainButtons.Square) == MainButtons.Square;
                DpadUp = DpadDown = DpadLeft = DpadRight = false; // Default all dpad buttons to false
                switch((DpadButtons)(data[offset + 5] & 0x0F)) {
                    case DpadButtons.Up:
                        DpadUp = true;
                        break;
                    case DpadButtons.Down:
                        DpadDown = true;
                        break;
                    case DpadButtons.Right:
                        DpadRight = true;
                        break;
                    case DpadButtons.Left:
                        DpadLeft = true;
                        break;
                    case DpadButtons.UpRight:
                        DpadUp = true;
                        DpadRight = true;
                        break;
                    case DpadButtons.UpLeft:
                        DpadUp = true;
                        DpadLeft = true;
                        break;
                    case DpadButtons.DownRight:
                        DpadDown = true;
                        DpadRight = true;
                        break;
                    case DpadButtons.DownLeft:
                        DpadDown = true;
                        DpadLeft = true;
                        break;
                }
                var secondary = (SecondaryButtons)data[offset + 6];
                R3 = (secondary & SecondaryButtons.R3) == SecondaryButtons.R3;
                L3 = (secondary & SecondaryButtons.L3) == SecondaryButtons.L3;
                Options = (secondary & SecondaryButtons.Options) == SecondaryButtons.Options;
                Share = (secondary & SecondaryButtons.Share) == SecondaryButtons.Share;
                R1 = (secondary & SecondaryButtons.R1) == SecondaryButtons.R1;
                L1 = (secondary & SecondaryButtons.L1) == SecondaryButtons.L1;
                PlaystationButton = (data[offset + 7] & 0x1) != 0;
                TouchPadButton = (data[offset + 7] & 0x2) != 0;
                L2State = data[offset + 8];
                R2State = data[offset + 9];
                AccelX = BitConverter.ToInt16(data, offset + 13);
                AccelY = BitConverter.ToInt16(data, offset + 15);
                AccelZ = BitConverter.ToInt16(data, offset + 17);
                GyroX = BitConverter.ToInt16(data, offset + 19);
                GyroY = BitConverter.ToInt16(data, offset + 21);
                GyroZ = BitConverter.ToInt16(data, offset + 23);
                if(isUsb)
                    BatteryLevel = (data[offset + 30] - 16) * 10;
                else
                    BatteryLevel = (data[offset + 30] + 1) * 10;
                if(BatteryLevel > 100)
                    BatteryLevel = 100;
                else if(BatteryLevel < 0)
                    BatteryLevel = 0;
                TouchPad = new TouchPadData(data, isUsb);
                IsOk = TouchPad.IsOk;
            }

            public int BatteryLevel { get; private set; }
            public short GyroX { get; private set; }
            public short GyroY { get; private set; }
            public short GyroZ { get; private set; }
            public short AccelX { get; private set; }
            public short AccelY { get; private set; }
            public short AccelZ { get; private set; }
            public bool Circle { get; private set; }
            public bool Cross { get; private set; }
            public bool L1 { get; private set; }
            public byte L2State { get; private set; }
            public bool L3 { get; private set; }
            public byte LeftX { get; private set; }
            public byte LeftY { get; private set; }
            public bool Options { get; private set; }
            public bool R1 { get; private set; }
            public byte R2State { get; private set; }
            public bool R3 { get; private set; }
            public byte RightX { get; private set; }
            public byte RightY { get; private set; }
            public bool Share { get; private set; }
            public bool Square { get; private set; }
            public bool TouchPadButton { get; private set; }
            public bool Triangle { get; private set; }
            public bool L2 { get { return L2State > 0; } }
            public bool R2 { get { return R2State > 0; } }
            public bool PlaystationButton { get; private set; }
            public bool DpadUp { get; private set; }
            public bool DpadDown { get; private set; }
            public bool DpadLeft { get; private set; }
            public bool DpadRight { get; private set; }
            public TouchPadData TouchPad { get; private set; }
            public bool IsOk { get; private set; }

            [Flags] private enum MainButtons {
                Triangle = 128,
                Circle = 64,
                Cross = 32,
                Square = 16
            }

            private enum DpadButtons {
                Up = 0,
                UpRight = 1,
                Right = 2,
                DownRight = 3,
                Down = 4,
                DownLeft = 5,
                Left = 6,
                UpLeft = 7
            }

            [Flags] private enum SecondaryButtons {
                R3 = 128,
                L3 = 64,
                Options = 32,
                Share = 16,
                //R2 = 8, // I don't use this as it's also part of the state, the state is always > 1 when this is set
                //L2 = 4, // I don't use this as it's also part of the state, the state is always > 1 when this is set
                R1 = 2,
                L1 = 1
            }

            public class TouchPadData {
                public TouchPadData(IList<byte> data, bool isUsb) {
                    IsOk = false;
                    var offset = 35;
                    if(!isUsb)
                        offset += 2;
                    if(data.Count < offset + 7)
                        return;
                    IsP1Active = data[offset] >> 7 == 0;
                    IsP2Active = data[offset + 4] >> 7 == 0;
                    TouchId1 = (byte)(data[offset] & 0x7F);
                    TouchId2 = (byte)(data[offset + 4] & 0x7F);
                    TouchX1 = data[offset + 1] + ((data[2 + offset] & 0xF) * 0xFF);
                    TouchY1 = ((data[offset + 2] & 0xF0) >> 4) + (data[3 + offset] * 0x10);
                    TouchX2 = data[offset + 5] + ((data[6 + offset] & 0xF) * 0xFF);
                    TouchY2 = ((data[offset + 6] & 0xF0) >> 4) + (data[7 + offset] * 0x10);
                    IsOk = true;
                }

                public bool IsOk { get; private set; }
                public bool IsP1Active { get; private set; }
                public bool IsP2Active { get; private set; }
                public byte TouchId1 { get; private set; }
                public byte TouchId2 { get; private set; }
                public int TouchX1 { get; private set; }
                public int TouchY1 { get; private set; }
                public int TouchX2 { get; private set; }
                public int TouchY2 { get; private set; }
            }
        }
    }

    internal class Ds4UpdateEventArgs : EventArgs {
        public readonly Dualshock4.ControllerState State;

        public Ds4UpdateEventArgs(Dualshock4.ControllerState state) {
            State = state;
        }
    }

}