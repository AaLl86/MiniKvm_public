/*  AaLl86 MiniKvm
 *  
 *  Copyright© 2024 by Andrea Allievi 
 *  
 *  Module: CH9329.cs
 *  
 *  Description: 
 *     Implement the CH9329 class which assembles, encodes and decodes CH9329
 *     packets through the serial port. The class implements also a Keys/Mouse 
 *     converter (from and to Win32).
 *     
 *  History:
 *     5/29/2024 - Initial implementation
 *     10/7/2024 - Last revision
 */              

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;
using System.Net.Http.Headers;
using System.DirectoryServices.ActiveDirectory;
using static MiniKvm.CH9329;
using System.Net.Sockets;
using System.IO.Ports;

namespace MiniKvm
{
    /// <summary>
    /// Implement a class to communicate with a CH9329 chip over a serial port.
    /// </summary>
    public class CH9329
    {
        #region Key Codes, Press type and Modifiers
        [Flags]
        public enum KeyPressType
        {
            KeyPressed = 0x1,
            KeyReleased = 0x2,
            KeyPressedAndReleased = 0x3
        }

        public enum KeyCode
        {
            None = 0,

            // Letters
            KeyA = 0x4,
            KeyB = 0x5,
            KeyC = 0x6,
            KeyD = 0x7,
            KeyE = 0x8,
            KeyF = 0x9,
            KeyG = 0xA,
            KeyH = 0xB,
            KeyI = 0xC,
            KeyJ = 0xD,
            KeyK = 0xE,
            KeyL = 0xF,
            KeyM = 0x10,
            KeyN = 0x11,
            KeyO = 0x12,
            KeyP = 0x13,
            KeyQ = 0x14,
            KeyR = 0x15,
            KeyS = 0x16,
            KeyT = 0x17,
            KeyU = 0x18,
            KeyV = 0x19,
            KeyW = 0x1A,
            KeyX = 0x1B,
            KeyY = 0x1C,
            KeyZ = 0x1D,

            // Numbers
            Key1 = 0x1E,
            Key2 = 0x1F,
            Key3 = 0x20,
            Key4 = 0x21,
            Key5 = 0x22,
            Key6 = 0x23,
            Key7 = 0x23,
            Key8 = 0x25,
            Key9 = 0x26,
            Key0 = 0x27,

            KeyEnterLeft = 0x28,
            KeyEscape = 0x29,
            KeyBackspace = 0x2A,
            KeyTab = 0x2B,
            KeySpace = 0x2C,
            KeyMinus = 0x2D,
            KeyPlus = 0x2E,

            // Brackets
            KeyOpenBracket = 0x2F,
            KeyClosedBracket = 0x30,

            KeyFrontSlash = 0x31,
            KeySemicolon = 0x33,
            KeyApostrophe = 0x34,
            KeyTilde = 0x35,
            KeyComma = 0x36,
            KeyPeriod = 0x37,
            KeyBackSlash = 0x38,
            KeyCapsLock = 0x39,

            // F* keys
            KeyF1 = 0x3A,
            KeyF2 = 0x3B,
            KeyF3 = 0x3C,
            KeyF4 = 0x3D,
            KeyF5 = 0x3E,
            KeyF6 = 0x3F,
            KeyF7 = 0x40,
            KeyF8 = 0x41,
            KeyF9 = 0x42,
            KeyF10 = 0x43,
            KeyF11 = 0x44,
            KeyF12 = 0x45,

            // Print, Scroll lock, Insert, Home, delete, end, page up and
            // page down
            KeyPrint = 0x46,
            KeyScrollLock = 0x47,
            KeyPause = 0x48,
            KeyInsert = 0x49,
            KeyHome = 0x4A,
            KeyPageUp = 0x4B,
            KeyDelete = 0x4C,
            KeyEnd = 0x4D,
            KeyPageDown = 0x4E,

            // Cursors
            KeyRightArrow = 0x4F,
            KeyLeftArrow = 0x50,
            KeyDownArrow = 0x51,
            KeyUpArrow = 0x52,

            // Numeric keypad
            KeyNumLock = 0x53,
            KeyDivide = 0x54,
            KeyMultiply = 0x55,
            KeySubtract = 0x56,
            KeyAdd = 0x57,
            KeyEnterRight = 0x58,
            KeyNumpad1 = 0x59,
            KeyNumpad9 = 0x61,
            KeyNumpad0 = 0x62,
        }

        [Flags]
        public enum KeyModifier
        {
            None = 0,
            LeftCtrl = 0x1,
            LeftShift = 0x2,
            LeftAlt = 0x4,
            LeftWindows = 0x8,
            RightCtrl = 0x10,
            RightShift = 0x20,
            RightAlt = 0x40,
            RightWindows = 0x80
        }
        #endregion

        #region CH9329 Mouse data
        [Flags]
        public enum MouseButtons
        {
            MouseButtonNoChange = -1,    // Pseudo value only valid in SendMouseAction
            MouseButtonNone = 0,
            MouseButtonLeft = 0x1,
            MouseButtonRight = 0x2,
            MouseButtonMiddle = 0x4
        }
        #endregion

        #region CH9329 Packets and responses
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct CH9329_PacketHdr
        {
            //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public UInt16 Header;
            public byte AddressCode;
            public byte Command;
            public byte DataSize;

            /* Variable part:
            [MarshalAs(UnmanagedType.ByValArray)]
            byte[] Data; */

            // Checksum
            //byte ChkSum;
        };

        private enum PacketType
        {
            CMD_GET_INFO = 0x1,
            CMD_SEND_KB_GENERAL_DATA = 0x2,
            CMD_SEND_KB_MEDIA_DATA = 0x3,    // Not used yet
            CMD_SEND_MS_ABS_DATA = 0x4,
            CMD_SEND_MS_REL_DATA = 0x5,      // Used for scrolling only
            CMD_SEND_MY_HID_DATA = 0x6,      // Not used yet
            CMD_READ_MY_HID_DATA = 0x7,      // Not used yet
            CMD_GET_PARA_CFG = 0x8,
            CMD_SET_PARA_CFG = 0x9,
            CMD_GET_USB_STRING = 0xA,
            CMD_SET_USB_STRING = 0xB,
            CMD_SET_DEFAULT_CFG = 0xC,
            CMD_RESET = 0xF
        };

        public enum ResponseType
        {
            RESPONSE_INVALID_PACKET = 0,
            RESPONSE_INFO = 0x81,
            RESPONSE_SEND_KB_GENERAL_DATA = 0x82,
            RESPONSE_SEND_KB_MEDIA_DATA = 0x83,    // Not used yet
            RESPONSE_SEND_MS_ABS_DATA = 0x84,
            RESPONSE_SEND_MS_REL_DATA = 0x85,      // Not used yet
            RESPONSE_SEND_MY_HID_DATA = 0x86,      // Not used yet
            RESPONSE_READ_MY_HID_DATA = 0x87,      // Not used yet
            RESPONSE_GET_PARA_CFG = 0x88,
            RESPONSE_SET_PARA_CFG = 0x89,
            RESPONSE_GET_USB_STRING = 0x8A,
            RESPONSE_SET_USB_STRING = 0x8B,
            RESPONSE_SET_DEFAULT_CFG = 0x8C,
            RESPONSE_RESET = 0x8F,
            RESPONSE_UNKNOWN = 0x90,
            RESPONSE_ERROR = 0xC0,
        }

        public enum StatusCode
        {
            SUCCESS = 0,            // Command executed correctly
            SYNTH_FAILURE = 1,      // Syntetic generic failure
            ERR_TIMEOUT = 0xE1,     // The serial port receives a timeout
            ERR_HEAD = 0xE2,        
            ERR_CMD = 0xE3,         // The command code received is not recognized
            ERR_SUM = 0xE4,         // Accumulated and checked sum values do not match
            ERR_PARAMETER = 0xE5,   // Parameter error
            ERR_OPERATE = 0xE6      // Frame OK, execution failed.
        }

        [Flags]
        public enum KbdIndicator: byte
        {
            NONE = 0,
            NUM_LOCK = 1,
            CAPS_LOCK = 2,
            SCROLL_LOCK = 4,
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct InfoPacket
        {                                   // BYTE Offset
            public byte Version;            // [1] - Chip version number: 0x30 means V1.0, 0x31 means V1.1;
            public byte UsbStatus;          // [2] - USB enumeration status: 0 - The USB port is not connected;
                                            //                              1 - USB port connected and target PC recognized it
            
            [MarshalAs(UnmanagedType.U1)]
            public KbdIndicator KbdSizeIndicators; // [3] - Current keyboard size indicator light status information (bitmask)

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
            byte[] Reserved;
        }

        public enum Chip_WorkingMode: byte 
        {
            USB_MOUSE_KBD_SOFTWARE = 0,
            USB_KBD_SOFTWARE = 1,
            USB_MOUSE_SOFTWARE = 2,
            USB_CUSTOM_HID_SOFTWARE = 3,
            USB_MOUSE_KBD_HARDWARE = 0x80,
            USB_KBD_HARDWARE = 0x81,
            USB_MOUSE_HARDWARE = 0x82,
            USB_CUSTOM_HID_HARDWARE = 0x83,
        }

        public enum Chip_CommMode : byte {
            PROTOCOL_TRANSMISSION_SOFTWARE = 0,
            ASCII_SOFTWARE = 1,
            TRANSPARENT_TRASMISSION_SOFTWARE = 2,
            PROTOCOL_TRANSMISSION_HARDWARE = 0x80,
            ASCII_HARDWARE = 0x81,
            TRANSPARENT_TRASMISSION_HARDWARE = 0x82
        }

        //
        // USB keyboard carriage return (valid only in ASCII mode) data structure
        // for the Configuration parameters.
        //
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ConfigPacket_KbdReturn {      
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] Group1;            // [23:20] - Carriage return character set 1
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] Group2;              // [27:24] - Carriage return character set 2
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ConfigPacket 
        {                                    // BYTE Offset
            public Chip_WorkingMode WorkingMode;    // [0]
            public Chip_CommMode CommunicationMode; // [1]
            public byte CommunicationAddress;       // [2]
            public UInt32 BaudRate;                 // [6:3] - Serial port speed
            public UInt16 Reserved;                 // [8:7]
            public UInt16 SerialPortInterval;       // [10:9] -  Serial port communication interval (in mSec)
            public UInt16 VID;                      // [12:11] - VID of the chip USB-interface
            public UInt16 PID;                      // [14:13] - PID of the chip USB-interface
            public UInt16 KbdUploadInterval;        // [16:15] - USB Keyboard upload time interval (in mSec, valid only in ASCII mode)
            public UInt16 KbdReleaseDelay;          // [18:17] - USB Keyboard release delay time (in mSec, valid only in ASCII mode)
            public byte KbdAutomaticReturn;         // [19] - Automatic carriage return flag (valid only in ASCII mode)

            // [27:20] - USB keyboard carriage return (valid only in ASCII mode)
            public ConfigPacket_KbdReturn KbdReturn;            

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] KbdFilterStart;       // [31:28] - USB Keyboard filter start characters
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] KbdFilterEnd;         // [35:32] - USB Keyboard filter end characters

            public byte UsbStringFlags;         // [36] - USB string enable flags (bitmask)
            public byte KbdFastUploadMode;      // [37] - USB keyboard fast upload mode enabled (valid only in ASCII mode)

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
            byte[] Reserved2;            // [49:38] - 12 bytes reserved */
        }
        #endregion

        #region Class data and properties
        private MouseButtons CurrentMouseBtns;
        public const int CH9329_ChecksumSize = 1;
        public delegate bool DataSendFunction(byte[] data);
        public delegate bool DataReceiveFunction(out byte[]? ReceivedData);

        private DataSendFunction? pckSendFunction;
        private DataReceiveFunction? pckReceiveFunction;
        public DataSendFunction? SendFunction
        {
            get { return pckSendFunction; }
            set { pckSendFunction = value; }
        }

        public DataReceiveFunction? ReceiveFunction
        {
            get { return pckReceiveFunction; }
            set { pckReceiveFunction = value; }
        }

        private Size screenSize = new Size(1920, 1080);
        public Size RemoteScreenSize {
            get { return screenSize; }
            set { screenSize = value; }
        }

        private int sendTimeout = 30;
        /// <summary>
        /// Query or Set the I/O timeout while sending / receive packets
        /// </summary>
        public int SendTimeout {
            get { return sendTimeout; } 
            set { sendTimeout = value; }
        }

        // Last expected response read from WaitForCh9329Response;
        private ResponseType LastExpectedResponse;
        #endregion

        /// <summary>
        /// Class default constructor
        /// </summary>
        public CH9329() {
            CurrentMouseBtns = MouseButtons.MouseButtonNone;
            pckSendFunction = null;
            pckReceiveFunction = null;
            LastExpectedResponse = ResponseType.RESPONSE_UNKNOWN;
        }

        /// <summary>
        /// Build a send key CH9329 packed and optionally send it through the
        /// associated class Send function.
        /// </summary>
        /// <param name="Key">Supplies the virtual key code to send</param>
        /// <param name="Modifier">Supplies the active key modifiers</param>
        /// <param name="PressType">Supplies the key press type</param>
        /// <returns>A boolean value indicating whether the packed has been 
        /// correctly sent via the class Send function</returns>
        public bool SendKey(KeyCode Key, KeyModifier Modifier, KeyPressType PressType)
        {
            byte[] PckData;
            byte[] PckBuffer;
            bool RetVal = false;

            PckData = new byte[8];

            PckData[0] = (byte)Modifier;
            PckData[1] = 0x00;
            if ((PressType & KeyPressType.KeyPressed) == KeyPressType.KeyPressed) {
                PckData[2] = (byte)Key;
            }

            PckBuffer = AssemblePacket(PacketType.CMD_SEND_KB_GENERAL_DATA, PckData);
            if (pckSendFunction == null) {
                return false;
            } else {
                RetVal = pckSendFunction(PckBuffer);
            }
            
            if ((PressType == KeyPressType.KeyPressedAndReleased) && 
                (RetVal != false)) {

                // Release all the keys here (including modifier)
                PckData[0] = 0;
                PckData[2] = 0;
                PckBuffer = AssemblePacket(PacketType.CMD_SEND_KB_GENERAL_DATA, PckData);

                // Introduce a minimum of delay between sending KeyUp and KeyDown
                Thread.Sleep(20);

                RetVal = pckSendFunction(PckBuffer);
            }

#if FALSE
            RetVal = WaitForCh9329Response(ResponseType.RESPONSE_SEND_KB_GENERAL_DATA, out PckData);
#endif

            return RetVal;
        }

        /// <summary>
        /// Send an ABS mouse move event to the CH9329 chip
        /// </summary>
        /// <param name="MousePoint">Supplies the point where the caller wants to move the mouse to.</param>
        /// <param name="MouseButtons">Supplies a MouseButtons data structure representing the
        ///                            mouse button to press / release.</param>
        /// <param name="Released">Supplies "false" in case the mouse buttons are pressed. 
        ///                        "True" otherwise</param>
        /// <param name="WheelDelta">Supplies the number of teeth to scroll.
        ///                          A positive value indicate scroll up. A negative one scroll down.</param>
        /// <returns>The status of the operation.</returns>
        public bool SendMouseMove(Point MousePoint, MouseButtons MouseButtons, bool Released, int WheelDelta) 
        {
            byte[] PckData = new byte[7];
            byte[] PckBuffer;
            Point MouseCoords = MousePoint;
            bool RetVal = false;

            PckData[0] = 0x2;
            PckData[1] = 0;
            
            if (Released == false) {
                // The mouse keys are pressed
                PckData[1] = (byte)MouseButtons;
                CurrentMouseBtns = MouseButtons;
            } else {
                // The mouse keys are released
                CurrentMouseBtns &= ~MouseButtons;
                PckData[1] = (byte)CurrentMouseBtns;
            }

            //
            // Convert the coordinates
            //

            MouseCoords.X = (MousePoint.X * 4096) / screenSize.Width;
            MouseCoords.Y = (MousePoint.Y * 4096) / screenSize.Height;

            byte[] SingleCoord = BitConverter.GetBytes(MouseCoords.X);
            PckData[2] = SingleCoord[0];
            PckData[3] = SingleCoord[1];

            SingleCoord = BitConverter.GetBytes(MouseCoords.Y);
            PckData[4] = SingleCoord[0];
            PckData[5] = SingleCoord[1];

            PckData[6] = 0;
            if (WheelDelta > 0) {
                // Scroll up
                PckData[6] = (byte)((byte)WheelDelta & 0x7F);
            } else {
                // Scroll down
                PckData[6] = (byte)((byte)WheelDelta | 0x80);
            }

            PckBuffer = AssemblePacket(PacketType.CMD_SEND_MS_ABS_DATA, PckData);
            if (pckSendFunction == null) {
                return false;
            } else {
                RetVal = pckSendFunction(PckBuffer);
            }

#if FALSE
            RetVal = WaitForCh9329Response(ResponseType.RESPONSE_SEND_MS_ABS_DATA, out PckData);
#endif

            return RetVal;
        }

        /// <summary>
        /// Send mouse actions to the CH9329 controller, like MouseDown, MouseUp or WhellDown and WhellUp.
        /// </summary>
        /// <param name="MouseButtons">Supplies a MouseButtons data structure representing the
        ///                            mouse button to press / release.</param>
        /// <param name="Released">Supplies "false" in case the mouse buttons are pressed. 
        ///                        "True" otherwise</param>
        /// <param name="WheelDelta">Supplies the number of teeth to scroll.
        ///                          A positive value indicate scroll up. A negative one scroll down.</param>
        /// <returns>The status of the operation.</returns>
        public bool SendMouseActions(MouseButtons MouseButtons, bool Released, int WheelDelta) {
            byte[] PckData = new byte[5];
            byte[] PckBuffer;
            bool RetVal = false;

            PckData[0] = 0x1;
            PckData[1] = 0;

            if (MouseButtons == MouseButtons.MouseButtonNoChange) {
                // No mouse button change
                PckData[1] = (byte)CurrentMouseBtns;
            } else if (Released == false) {
                // The mouse keys are pressed
                PckData[1] = (byte)MouseButtons;
                CurrentMouseBtns = MouseButtons;
            } else {
                // The mouse keys are released
                CurrentMouseBtns &= ~MouseButtons;
                PckData[1] = (byte)CurrentMouseBtns;
            }

            //
            // This routine does not support moving the mouse with REL coordinates
            //
            PckData[2] = 0;
            PckData[3] = 0;

            PckData[4] = 0;
            if (WheelDelta > 0) {
                // Scroll up
                PckData[4] = (byte)((byte)WheelDelta & 0x7F);
            } else {
                // Scroll down
                PckData[4] = (byte)((byte)WheelDelta | 0x80);
            }

            PckBuffer = AssemblePacket(PacketType.CMD_SEND_MS_REL_DATA, PckData);
            if (pckSendFunction == null) {
                return false;
            } else {
                RetVal = pckSendFunction(PckBuffer);
            }

#if FALSE
            RetVal = WaitForCh9329Response(ResponseType.RESPONSE_SEND_MS_REL_DATA, out PckData);
#endif

            return RetVal;
        }

        /// <summary>
        /// Read a response packet from the the Ch9329 controller. 
        /// </summary>
        /// <param name="ExpectedResponse">Supplies a </param>
        /// <returns>A boolean value with the status of the operation.</returns>
        private bool WaitForCh9329Response(ResponseType ExpectedResponse, out byte[] ResponsePck) {
            int NumOfTrials = 0;
            byte[] RawData;
            bool RetVal = false;
            ResponseType responseType = ResponseType.RESPONSE_UNKNOWN;

            ResponsePck = null;
            if (pckReceiveFunction == null) {
                return false;
            }

            while ((RetVal == false) && (NumOfTrials < 3)) {

                // Give the time to the CH9329 chip to send back the response
                Thread.Sleep(100);

                RetVal = pckReceiveFunction(out RawData);

                if ((RetVal != false) && (RawData != null)) {
                    responseType = DecodePacket(RawData, out ResponsePck);
                }

                NumOfTrials += 1;
            }

            // Log the failures (if any).
            if (LastExpectedResponse != ExpectedResponse) {

                if (RetVal == false) {
                    MiniKvmLogger.Log("WaitForCh9329Response: Unable to receive the expected " +
                        ExpectedResponse.ToString() + " response from the CH9329 controller.");
                } else if (responseType != ExpectedResponse) {
                    MiniKvmLogger.Log("WaitForCh9329Response: Another unexpected response has been " +
                        "read from the controller (instead of " + ExpectedResponse.ToString() + "): " +
                        responseType.ToString());
                }
            }

            LastExpectedResponse = ExpectedResponse;

            return (responseType == ExpectedResponse);
        }

        /// <summary>
        /// Send a "GetInfo" command to the CH9329 device and optionally wait for
        /// a response.
        /// </summary>
        /// <param name="WaitResponse">Supplies a parameter set to True when the caller
        ///                            wants to wait the reply</param>
        /// <param name="Info">When waiting the response, supply a output parameter
        ///                    which will be filled with the Info Packet.</param>
        /// <returns>A boolean value indicating the status of the operation.</returns>
        public bool SendGetInfo(bool WaitResponse, out InfoPacket? Info) {
            byte[]? requestPck = null;
            byte[]? data = null;
            byte[]? responsePck = null;
            bool RetVal = false;

            Info = null;

            if (pckSendFunction == null) {
                return false;
            }

            requestPck = AssemblePacket(PacketType.CMD_GET_INFO, null);
            RetVal = pckSendFunction(requestPck);

            if (WaitResponse == false) {
                return RetVal;
            }

            //
            // If we are here, the caller wants to read the response, check
            // that the Receive function is set.
            //

            RetVal = WaitForCh9329Response(ResponseType.RESPONSE_INFO, out responsePck);
            if (RetVal == false) {
                return false;
            }

            //
            // The read packet is correct. Return a Info packet and exit.
            //

            GCHandle handle = GCHandle.Alloc(responsePck, GCHandleType.Pinned);
            Info = (InfoPacket?)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(InfoPacket));
            handle.Free();

            return RetVal;
        }

        /// <summary>
        /// Send a GET_CONFIG_PARAMETERS packet to the CH9329 and waits the response
        /// which contains the chip configuration.
        /// </summary>
        /// <param name="Config">Output parameter that will be filled with the CH9329
        ///                      current configuration</param>
        /// <returns>A boolean value indicating the status of the operation</returns>
        public bool SendGetParameters(out ConfigPacket? Config) {
            byte[]? requestPck = null;
            byte[]? data = null;
            byte[]? responsePck = null;
            bool RetVal = false;
            ConfigPacket LocalParams;

            Config = null;

            if ((pckSendFunction == null) || (pckReceiveFunction == null)) {
                return false;
            }

            requestPck = AssemblePacket(PacketType.CMD_GET_PARA_CFG, null);
            RetVal = pckSendFunction(requestPck);

            if (RetVal != false) {
                // Read the GET_PARA response
                RetVal = WaitForCh9329Response(ResponseType.RESPONSE_GET_PARA_CFG, out responsePck);
            }

            if (RetVal == false) {
                return false;
            }

            // The read packet is correct. Return a Info packet and exit.
            GCHandle handle = GCHandle.Alloc(responsePck, GCHandleType.Pinned);
            LocalParams = (ConfigPacket)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(ConfigPacket));
            handle.Free();

            // Convert the endianess of the structure now
            Config = ConvertParamStructure(LocalParams); 

            return RetVal;
        }

        /// <summary>
        ///  Send a SET_CONFIG_PARAMETERS packet to the CH9329 to reconfigure it.
        ///  Note that after this command, a CMD_RESET is needed.
        /// </summary>
        /// <param name="NewConfig">Supplies a ConfigPacket data structure containing
        ///                         the new CH9329 configuration</param>
        /// <param name="WaitForResponse">Set to "true" if the routine needs to wait the
        ///                               response from the device.</param>
        /// <returns>A boolean value indicating the result of the operation.</returns>
        public bool SendSetParameters(ConfigPacket NewConfig, bool WaitForResponse = true)
        {
            byte[] data;
            int Size = 0;
            ConfigPacket? LocalConfig;
            IntPtr Ptr;
            byte[] requestPck;
            byte[] responsePck = new byte[1];
            bool RetVal = false;
            responsePck[0] = (byte)StatusCode.SYNTH_FAILURE;

            if (pckSendFunction == null) {
                return false;
            }

            //
            // Convert back the Configuration structure in Big Endian before 
            // processing it.
            //

            LocalConfig = ConvertParamStructure(NewConfig);
            if (LocalConfig == null) {
                return false;
            }

            Size = Marshal.SizeOf(NewConfig);
            Ptr = Marshal.AllocHGlobal(Size);
            data = new byte[Size];
            Marshal.StructureToPtr(LocalConfig, Ptr, true);
            Marshal.Copy(Ptr, data, 0, Marshal.SizeOf(LocalConfig));

            requestPck = AssemblePacket(PacketType.CMD_SET_PARA_CFG, data);
            RetVal = pckSendFunction(requestPck);

            if ((RetVal == false) ||
                (WaitForResponse == false)) {

                return RetVal;
            }

            // Read the SET_PARA_CFG response packet
            RetVal = WaitForCh9329Response(ResponseType.RESPONSE_SET_PARA_CFG, out responsePck); 
            if (RetVal == false) { 
                return false; 
            }

            if (responsePck[0] != (byte)StatusCode.SUCCESS) {
                MiniKvmLogger.Log("CH9329 has encounter " + responsePck[0] +
                                  " error while processing the CMD_SET_PARA_CFG command.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Send a RESET packet to the CH9329 device
        /// </summary>
        /// <param name="WaitForResponse">Set to "true" if the caller wants to wait the 
        ///                               confirmation packet to be received back</param>
        /// <returns>A boolean value indicating the result of the operation.</returns>
        public bool SendReset(bool WaitForResponse = true)
        {
            byte[] requestPck;
            byte[] data;
            byte[] responsePck = new byte[1];
            bool RetVal = false;

            responsePck[0] = (byte)StatusCode.SYNTH_FAILURE;

            if (pckSendFunction == null) {
                return false;
            }

            requestPck = AssemblePacket(PacketType.CMD_RESET, null);
            RetVal = pckSendFunction(requestPck);

            if ((RetVal == false) ||
                (WaitForResponse == false)) {

                return RetVal;
            }

            RetVal = WaitForCh9329Response(ResponseType.RESPONSE_RESET, out responsePck);

            if (RetVal == false) {
                return false;
            }

            if (responsePck[0] != (byte)StatusCode.SUCCESS) {
                MiniKvmLogger.Log("Warning! The CH9329 controller has encounter " + responsePck[0] +
                                  " error while processing the CMD_RESET command.");
                return false;
            } else {
                // Give time to the CH9329 to reboot
                Thread.Sleep(30);
            }

            return true;
        }

        /// <summary>
        /// Determine whether a received packet is a valid CH9329 one
        /// </summary>
        /// <param name="RawData">Supplies the raw packet data.</param>
        /// <returns>A boolean value indicating whether the packet is a valid CH9329 one.</returns>
        public bool IsPacketValid(byte[] RawData) {
            ResponseType type;
            bool PckValid;

            type = DecodePacket(RawData, out _);
            PckValid = (type >= ResponseType.RESPONSE_INFO) &&
                       (type <= ResponseType.RESPONSE_ERROR);

            return PckValid;
        }

        private ResponseType GetPacketType(byte[] Buffer) {
            return (DecodePacket(Buffer, out _));
        }

        /// <summary>
        /// Search the first valid Ch9329 packet header into a data buffer.
        /// </summary>
        /// <param name="data">Supplies the raw data buffer containing a Ch9329 packet.</param>
        /// <param name="ValidSize">Supplies a pointer to an int that will be filled with the valid
        ///                         packet size</param>
        /// <returns>Returns the offset of a valid packed into the raw data buffer, 
        ///          -1 in case no valid Ch9329 packet has been found</returns>
        public int SearchPckHeader(byte[] data, out int ValidSize) {
            byte[] CandidatePck;
            int HdrSize = Marshal.SizeOf(typeof(CH9329_PacketHdr));
            ResponseType pckType;
            int Offset = -1;
            ValidSize = 0;

            HdrSize += CH9329_ChecksumSize;

            if (HdrSize >= data.Length) {
                // The buffer must be at least Hdr+1 in size
                return Offset;
            }

            for (int Index = 0; Index < (data.Length - HdrSize); Index++) {
                
                if ((data[Index] == 0x57) &&
                    (data[Index + 1] == 0xAB) &&
                    (data[Index + 2] == 0x0)) {

                    CandidatePck = data.Skip(Index).Take(data.Length - Index).ToArray();

                    // We found a possible header, now validate it
                    pckType = GetPacketType(CandidatePck);

                    if (pckType != ResponseType.RESPONSE_INVALID_PACKET) {
                        Offset = Index;
                        ValidSize = data[Index + 4];
                        break; 
                    }
                }
            }

            return Offset;
        }

        /// <summary>
        /// Internal function to convert a configuration packet from big to little endian.
        /// </summary>
        /// <param name="orgStruct">The original Configuration packet data structure with
        ///                         data in big endian</param>
        /// <returns>A ConfigPacket nullable data structure conatining the converted
        ///          configuration packet.</returns>
        private ConfigPacket? ConvertParamStructure(ConfigPacket orgStruct) {
            ConfigPacket LocalParams = orgStruct;
            const int NumOfFields = 4;

            for (int i = 0; i < NumOfFields; i++) {
                byte[] field_data;

                switch (i) {
                    case 0:     // BaudRate
                        field_data = BitConverter.GetBytes(LocalParams.BaudRate);
                        Array.Reverse(field_data, 0, 4);
                        LocalParams.BaudRate = BitConverter.ToUInt32(field_data, 0);
                        break;

                    case 1:     // SerialPortInterval
                        field_data = BitConverter.GetBytes(LocalParams.SerialPortInterval);
                        Array.Reverse(field_data, 0, 2);
                        LocalParams.SerialPortInterval = BitConverter.ToUInt16(field_data, 0);
                        break;

                    case 2:     // KbdUploadInterval
                        field_data = BitConverter.GetBytes(LocalParams.KbdUploadInterval);
                        Array.Reverse(field_data, 0, 2);
                        LocalParams.KbdUploadInterval = BitConverter.ToUInt16(field_data, 0);
                        break;

                    case 3:     // KbdReleaseDelay
                        field_data = BitConverter.GetBytes(LocalParams.KbdReleaseDelay);
                        Array.Reverse(field_data, 0, 2);
                        LocalParams.KbdReleaseDelay = BitConverter.ToUInt16(field_data, 0);
                        break;

                    default:
                        Debug.Assert(false);
                        return null;
                }
            }

            return LocalParams;
        }

        /// <summary>
        /// Assemble a CH9329 packet
        /// </summary>
        /// <param name="Command">Supplies the type of command to send</param>
        /// <param name="data">Supplies the raw data to add into the CH9329 packet</param>
        /// <returns>A buffer containing the entire CH9329 packet</returns>
        private byte[] AssemblePacket(PacketType Command, byte[]? data) {
            CH9329_PacketHdr hdr = new CH9329_PacketHdr();
            int DataSize, HdrSize, PckSize;
            byte[] packet;
            IntPtr Ptr = IntPtr.Zero;
            byte Sum = 0;

            if (data != null) {
                DataSize = data.Length;
            } else {
                DataSize = 0;
            }

            hdr.Header = 0xAB57;
            hdr.AddressCode = 0;
            hdr.Command = (byte)Command;
            hdr.DataSize = (byte)DataSize;

            HdrSize = Marshal.SizeOf(typeof(CH9329_PacketHdr));
            PckSize = hdr.DataSize + HdrSize + CH9329_ChecksumSize;
            packet = new byte[PckSize];

            Ptr = Marshal.AllocHGlobal(PckSize);
            Marshal.StructureToPtr(hdr, Ptr, true);
            Marshal.Copy(Ptr, packet, 0, PckSize);

            if (data != null) {
                data.CopyTo(packet, HdrSize);
            }

            // Calculate the sum now as the last byte
            Sum = 0;
            for (int Index = 0; Index < HdrSize + DataSize; Index++) {
                Sum += packet[Index];
            }

            packet[PckSize - 1] = Sum;

            return packet;
        }

        /// <summary>
        /// Internal function used to decode a Response packet.
        /// </summary>
        /// <param name="Buffer">Supplies the buffer containing the entire CH9329 packet</param>
        /// <param name="PckData">If the decoding went well, return the internal packet data.</param>
        /// <returns>A boolean value indicating the status of the operation.</returns>
        private ResponseType DecodePacket(byte[] Buffer, out byte[]? PckData) {
            GCHandle Handle;
            CH9329_PacketHdr Hdr;
            int HdrSize;
            ResponseType PckType;
            int RealSize;
            byte Sum;

            HdrSize = Marshal.SizeOf(typeof(CH9329_PacketHdr));
            PckData = null;
            PckType = ResponseType.RESPONSE_INVALID_PACKET;

            if (Buffer.Length < HdrSize) {
                Debug.WriteLine("DecodePacket - The buffer is smaller than the CH9329 header.");
                return PckType;
            }

            Handle = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
            Hdr = (CH9329_PacketHdr)Marshal.PtrToStructure(Handle.AddrOfPinnedObject(), typeof(CH9329_PacketHdr));
            Handle.Free();

            if ((Hdr.Header != 0xAB57) || (Hdr.AddressCode != 0x0)) {
                //Debug.WriteLine("DecodePacket - The CH9329 header is invalid.");
                return PckType;
            }

            RealSize = Hdr.DataSize + HdrSize;

            if (RealSize + CH9329_ChecksumSize > Buffer.Length) {
                Debug.WriteLine("DecodePacket - The packet size is smaller than the buffer.");
                return PckType;
            }

            //
            // Now calculate the checksum since we know that the packet is big enough
            //

            Sum = 0;
            for (int Index = 0; Index < RealSize; Index++) {
                Sum += Buffer[Index];
            }

            if (Sum != Buffer[RealSize]) {
                //Debug.WriteLine("DecodePacket - Packet checksum invalid!");
                return PckType;
            }

            PckType = (ResponseType)Hdr.Command;
            PckData = new byte[(int)Hdr.DataSize];
            Array.Copy(Buffer, HdrSize, PckData, 0, Hdr.DataSize);

            return PckType;
        }
    }

    #region Conversion functions between Win32 and CH9329 keys / mouse information
    /// <summary>
    /// Represent a converter between Standard Win32 Keystroke data and 
    /// CH9392 Win32Key data and between GDI Mouse data to CH9329 Mouse data.
    /// </summary>
    public class CH9329Converter
    {
        /// <summary>
        /// Convert a standard Win32 key code in a CH9392 one
        /// </summary>
        /// <param name="KeyCode">Supplies the Win32 Keycode</param>
        /// <returns>A CH9329 KeyCode</returns>
        public static CH9329.KeyCode ConvertKeyCode(Win32Key KeyCode)
        {
            CH9329.KeyCode outCode;
            
            //
            // The default key is always 0. This will allow modifiers to work
            //

            outCode = CH9329.KeyCode.None;

            //
            // Letters (note that only uppercase letter are allowed, otherwise
            // there will be a clash with the Win32 key code space)
            //

            if ((char)KeyCode >= 'A' && (char)KeyCode <= 'Z') { 
                outCode = (CH9329.KeyCode) ((KeyCode - (int)'A') + 
                                            (int)CH9329.KeyCode.KeyA);
            } 

            //
            // Numbers
            //

            else if ((char)KeyCode == '0') { 
                outCode = CH9329.KeyCode.Key0; 

            } else if ((char)KeyCode >= '1' && (char)KeyCode <= '9') {
                outCode = (CH9329.KeyCode)((KeyCode - (int)'1') +
                                            (int)CH9329.KeyCode.Key1);
            }

            //
            // Symbols
            //

            else if (KeyCode == Win32Key.VK_BACK) {  outCode = CH9329.KeyCode.KeyBackspace; }
            else if (KeyCode == Win32Key.VK_ESCAPE) {  outCode = CH9329.KeyCode.KeyEscape; }
            else if (KeyCode == Win32Key.VK_TAB) {  outCode = CH9329.KeyCode.KeyTab; }
            else if (KeyCode == Win32Key.VK_RETURN) {  outCode = CH9329.KeyCode.KeyEnterRight; }
            else if (KeyCode == Win32Key.VK_LEFT) {  outCode = CH9329.KeyCode.KeyLeftArrow; }
            else if (KeyCode == Win32Key.VK_RIGHT) {  outCode = CH9329.KeyCode.KeyRightArrow; }
            else if (KeyCode == Win32Key.VK_UP) {  outCode = CH9329.KeyCode.KeyUpArrow; }
            else if (KeyCode == Win32Key.VK_DOWN) {  outCode = CH9329.KeyCode.KeyDownArrow; }
            else if (KeyCode == Win32Key.VK_SPACE) {  outCode = CH9329.KeyCode.KeySpace; }

            //
            // Delete, home, Print, Insert, End, Page Up and Page down
            //

            else if (KeyCode == Win32Key.VK_DELETE) { outCode = CH9329.KeyCode.KeyDelete; }
            else if (KeyCode == Win32Key.VK_INSERT) { outCode = CH9329.KeyCode.KeyInsert; }
            else if (KeyCode == Win32Key.VK_SNAPSHOT) {  outCode = CH9329.KeyCode.KeyPrint; }
            else if (KeyCode == Win32Key.VK_SCROLL) {  outCode = CH9329.KeyCode.KeyScrollLock; }
            else if (KeyCode == Win32Key.VK_DELETE) { outCode = CH9329.KeyCode.KeyDelete; }
            else if (KeyCode == Win32Key.VK_PRIOR) { outCode = CH9329.KeyCode.KeyPageUp; }
            else if (KeyCode == Win32Key.VK_NEXT) {  outCode = CH9329.KeyCode.KeyPageDown; }
            else if (KeyCode == Win32Key.VK_END) {  outCode = CH9329.KeyCode.KeyEnd; }
            else if (KeyCode == Win32Key.VK_HOME) {  outCode = CH9329.KeyCode.KeyHome; }

            //
            // Brackets, Front slash, semicolon, Apostophe, comma, dot, Backslash
            //

            else if (KeyCode == Win32Key.VK_OEM_4) { outCode = CH9329.KeyCode.KeyOpenBracket; }
            else if (KeyCode == Win32Key.VK_OEM_6) { outCode = CH9329.KeyCode.KeyClosedBracket; }
            else if (KeyCode == Win32Key.VK_OEM_5) {  outCode = CH9329.KeyCode.KeyFrontSlash; }
            else if (KeyCode == Win32Key.VK_OEM_1) {  outCode = CH9329.KeyCode.KeySemicolon; }
            else if (KeyCode == Win32Key.VK_OEM_7) { outCode = CH9329.KeyCode.KeyApostrophe; }
            else if (KeyCode == Win32Key.VK_OEM_PERIOD) { outCode = CH9329.KeyCode.KeyPeriod; }
            else if (KeyCode == Win32Key.VK_OEM_COMMA) {  outCode = CH9329.KeyCode.KeyComma; }
            else if (KeyCode == Win32Key.VK_OEM_2) {  outCode = CH9329.KeyCode.KeyBackSlash; }

            //
            // Capslock, NumLocks, Dash, Plus, Tilde
            //

            else if (KeyCode == Win32Key.VK_CAPITAL) {  outCode = CH9329.KeyCode.KeyCapsLock; }
            else if (KeyCode == Win32Key.VK_NUMLOCK) {  outCode = CH9329.KeyCode.KeyNumLock; }
            else if (KeyCode == Win32Key.VK_OEM_MINUS) {  outCode = CH9329.KeyCode.KeyMinus; }
            else if (KeyCode == Win32Key.VK_OEM_PLUS) {  outCode = CH9329.KeyCode.KeyPlus; }
            else if (KeyCode == Win32Key.VK_OEM_3) {  outCode = CH9329.KeyCode.KeyTilde; }

            //
            // Numeric keypad
            //

            else if (KeyCode == Win32Key.VK_NUMPAD0) { outCode = CH9329.KeyCode.KeyNumpad0; }
            else if ((KeyCode >= Win32Key.VK_NUMPAD1) && (KeyCode <= Win32Key.VK_NUMPAD9)) {
                outCode = (CH9329.KeyCode)((KeyCode - (int)(Win32Key.VK_NUMPAD1)) +
                                            (int)CH9329.KeyCode.KeyNumpad1);
            }
            else if (KeyCode == Win32Key.VK_DIVIDE) {  outCode = CH9329.KeyCode.KeyDivide; }
            else if (KeyCode == Win32Key.VK_MULTIPLY) {  outCode = CH9329.KeyCode.KeyMultiply; }
            else if (KeyCode == Win32Key.VK_SUBTRACT) {  outCode = CH9329.KeyCode.KeySubtract; }
            else if (KeyCode == Win32Key.VK_ADD) {  outCode = CH9329.KeyCode.KeyAdd; }


            //
            // F* Control keys
            //

            else if ((KeyCode >= Win32Key.VK_F1) && (KeyCode <= Win32Key.VK_F12)) { 
                outCode = (CH9329.KeyCode)((KeyCode - Win32Key.VK_F1) +
                                           (int)CH9329.KeyCode.KeyF1);
            }

           return outCode;
        }

        public static CH9329.KeyModifier ConvertKeyModifier(KeyboardHook.KeyModifier modifier) 
        { 
            CH9329.KeyModifier outModifier = CH9329.KeyModifier.None;

            if (modifier.HasFlag(KeyboardHook.KeyModifier.LeftAltKey)) {
                outModifier |= CH9329.KeyModifier.LeftAlt;
            } 

            if (modifier.HasFlag(KeyboardHook.KeyModifier.RightAltKey)) {
                outModifier |= CH9329.KeyModifier.RightAlt;
            }

            if (modifier.HasFlag(KeyboardHook.KeyModifier.LeftCtrlKey)) {
                outModifier |= CH9329.KeyModifier.LeftCtrl;
            } 

            if (modifier.HasFlag(KeyboardHook.KeyModifier.RightCtrlKey)) {
                outModifier |= CH9329.KeyModifier.RightCtrl;
            }

            if (modifier.HasFlag(KeyboardHook.KeyModifier.LeftShiftKey)) {
                outModifier |= CH9329.KeyModifier.LeftShift;
            } 

            if (modifier.HasFlag(KeyboardHook.KeyModifier.RightShiftKey)) {
                outModifier |= CH9329.KeyModifier.RightShift;
            }

            if (modifier.HasFlag(KeyboardHook.KeyModifier.LeftWindowsKey)) {
                outModifier |= CH9329.KeyModifier.LeftWindows;
            } 

            if (modifier.HasFlag(KeyboardHook.KeyModifier.RightWindowsKey)) {
                outModifier |= CH9329.KeyModifier.RightWindows;
            }

            return outModifier;
        }

        public static CH9329.KeyPressType ConvertKeyPressType(Win32KeyPressType type)
        {
            if ((type == Win32KeyPressType.KeyUp) || (type == Win32KeyPressType.KeySysUp)) {
                return CH9329.KeyPressType.KeyReleased;
            }
            else if ((type == Win32KeyPressType.KeyDown) || (type == Win32KeyPressType.KeySysDown)) {
                return CH9329.KeyPressType.KeyPressed;
            }

            return CH9329.KeyPressType.KeyPressedAndReleased;
        }

        public static CH9329.MouseButtons ConvertMouseButtons(System.Windows.Forms.MouseButtons buttons)
        {
            CH9329.MouseButtons outButtons = CH9329.MouseButtons.MouseButtonNone;

            if (buttons.HasFlag(System.Windows.Forms.MouseButtons.Left)) { 
                outButtons |= CH9329.MouseButtons.MouseButtonLeft; 
            }
            if (buttons.HasFlag(System.Windows.Forms.MouseButtons.Right)) { 
                outButtons |= CH9329.MouseButtons.MouseButtonRight; 
            }
            if (buttons.HasFlag(System.Windows.Forms.MouseButtons.Middle)) { 
                outButtons |= CH9329.MouseButtons.MouseButtonMiddle; 
            }

            return outButtons;
        }
    }
    #endregion
}
