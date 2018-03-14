using System;
using System.IO.Ports;
using System.Threading;
using GestureControlledRemote;

namespace GestureControlledRemote
{
    public static class SerialSender
    {
        private static SerialPort serialPort;
        private static String portName;
        private static Int32 baudRate;
        private static bool sendState;

        /// Static Constructor
        static SerialSender()
        {
            // Configure port name and baud rate
            portName = "COM3";
            baudRate = 9600;

            // Create a new SerialPort object with default settings
            serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);

            // Set the read/write timeouts
            serialPort.ReadTimeout = 500;
            serialPort.WriteTimeout = 500;

            // Needs to be enabled if Handshake is None
            serialPort.DtrEnable = true;
            serialPort.RtsEnable = true;

            // Open comms and start thread
            serialPort.Open();
        }

        public static void SendGesture(Gestures gesture)
        {
            if (sendState)
            {
                try
                {
                    serialPort.WriteLine(((int)gesture).ToString());

                    if(gesture == Gestures.VolumeUp || gesture == Gestures.VolumeDown)
                    {
                        int pulse = 0;
                        while (pulse < 5)
                        {
                            serialPort.WriteLine(((int)gesture).ToString());
                            Thread.Sleep(200);
                            ++pulse;
                        }
                    }
                    
                }
                catch (TimeoutException)
                {
                }
            }
        }

        /// Get shtudown signal from MainWindow class
        public static void SerialSenderShutdown()
        {
            serialPort.Close();
        }

        /// Set state for sending gesture
        public static void SetSendState(bool state)
        {
            sendState = state;
        }

        /// Get state for sending gesture
        public static bool GetSendState()
        {
            return sendState;
        }
    }
}