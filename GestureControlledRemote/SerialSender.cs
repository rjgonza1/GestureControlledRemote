using System;
using System.IO.Ports;
using System.Threading;
using GestureControlledRemote;

namespace GestureControlledRemote
{
    public static class SerialSender
    {
        private static Gestures outgoingGesture;
        private static SerialPort serialPort;
        private static bool open;

        /// Static Constructor
        static SerialSender()
        {
            // Initialize Gestures enum to 0
            outgoingGesture = Gestures.None;

            // Create thread to execute Send function
            //Thread sendThread = new Thread(SendGesture);

            // Create a new SerialPort object with default settings
            serialPort = new SerialPort("COM2", 9600);

            // Set the read/write timeouts
            //serialPort.ReadTimeout = 500;
            serialPort.WriteTimeout = 500;

            // Open comms and start thread
            serialPort.Open();
            open = true;
            //sendThread.Start();

            // MainWindow close will send a shutdown signal and exit the loop
            while(open)
            {
                if(outgoingGesture == Gestures.ReadySignal)
                {
                    ReadyState();
                    outgoingGesture = Gestures.None;
                }
            }

            // Join thread and close port
            //sendThread.Join();
            serialPort.Close();
        }

        private static void ReadyState()
        {
            while(outgoingGesture == Gestures.ReadySignal)
            {
                if (outgoingGesture != Gestures.ReadySignal || outgoingGesture != Gestures.None)
                    SendGesture();
            }
        }

        private static void SendGesture()
        {
            try
            {
                serialPort.Write(outgoingGesture.ToString());
            }
            catch (TimeoutException) { }
        }

        /// Receive the prediction from the learner
        public static void RecievePrediciton(Gestures prediction) { outgoingGesture = prediction; }

        /// Get shtudown signal from MainWindow class
        public static void SerialSenderShutdown() { open = false;  }

        /// Return outgoingGesture
        public static String GetOutgoingGesture() { return outgoingGesture.ToString(); }
    }
}