//
// Bluetooth.cs
// ControllerTester
//
// Created by Swizzy 17/08/2015
// Copyright (c) 2015 Swizzy. All rights reserved.

using System;
using System.Runtime.InteropServices;

namespace ControllerTester.Backend {

    internal static class Bluetooth {
        public const int IoctlBthDisconnectDevice = 0x41000c;

        [StructLayout(LayoutKind.Sequential)] public struct BluetoothFindRadioParams {
            [MarshalAs(UnmanagedType.U4)] public int dwSize;
        }

        #region Imports

        [DllImport("bthprops.cpl", CharSet = CharSet.Auto)] public static extern IntPtr BluetoothFindFirstRadio(
            ref BluetoothFindRadioParams pbtfrp, ref IntPtr phRadio);

        [DllImport("bthprops.cpl", CharSet = CharSet.Auto)] public static extern bool BluetoothFindNextRadio(
            IntPtr hFind, ref IntPtr phRadio);

        [DllImport("bthprops.cpl", CharSet = CharSet.Auto)] public static extern bool BluetoothFindRadioClose(
            IntPtr hFind);

        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool DeviceIoControl(
            IntPtr deviceHandle, int ioControlCode, ref long inBuffer, int inBufferSize, IntPtr outBuffer,
            int outBufferSize, ref int bytesReturned, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Auto)] public static extern bool CloseHandle(IntPtr hObject);
    }

    #endregion
}