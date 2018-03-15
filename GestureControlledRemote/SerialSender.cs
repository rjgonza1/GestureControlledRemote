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
            portName = "COM4";
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

        public static void SendGesture(Gestures gesture, int pulse)
        {
            if (sendState)
            {
                try
                {
                    serialPort.WriteLine(((int)gesture).ToString());

                    if(gesture == Gestures.VolumeUp || gesture == Gestures.VolumeDown)
                    {
                        int p = 0;
                        while (p < pulse)
                        {
                            serialPort.WriteLine(((int)gesture).ToString());
                            Thread.Sleep(200);
                            ++p;
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