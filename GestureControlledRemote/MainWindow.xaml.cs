using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes; 
using Microsoft.Kinect;
using Emgu.CV;
using Emgu.CV.Util;
using Emgu.CV.VideoSurveillance;
using Emgu.CV.CvEnum;
using System.IO;
using Emgu.CV.Structure;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace GestureControlledRemote
{
    public enum Gestures { ReadySignal = 0, TogglePower, ChannelUp, ChannelDown, VolumeUp, VolumeDown, None };

    public partial class MainWindow : Window
    {
        KinectSensor sensor;

        /// FSM
        private bool ready = false;

        /// KNN
        private const string TDFile = @"GestureData.txt";
        private Matrix<float> sample;

        /// Depth Pixels and Bitmap 
        private WriteableBitmap depthBitmap;
        private DepthImagePixel[] depthPixels;
        private byte[] depthcolorPixels;
        private const double threshDepth = 800;

        /// DTW
        private DtwGestureRecognizer _dtw;
        private const int _dimension = 12;

        /// Video
        private ArrayList _video;
        private bool _capturing;
        private const int MinimumFrames = 6;
        private const int BufferSize = 60;

        /// QoL counter for tracking how many gestures were saved for training
        private int gestureCount = 0;

        /// ArrayList of coordinates which are recorded in sequence to define one gesture
        private DateTime _captureCountdown = DateTime.Now;
      
        /// ArrayList of coordinates which are recorded in sequence to define one gesture
        private Timer _captureCountdownTimer;
        
        /// The minumum number of frames in the _video buffer before we attempt to start matching gestures
        private const int CaptureCountdownSeconds = 3;

        /// Where we will save our gestures to. The app will append a data/time and .txt to this string
        // private const string GestureSaveFileLocation = @"H:\My Dropbox\Dropbox\Microsoft Kinect SDK Beta\DTWGestureRecognition\DTWGestureRecognition\";
        private const string GestureSaveFileLocation = @"C:\Users\joshu\Desktop\Recorded Gestures\";
        private const string GestureSaveFileNamePrefix = @"RecordedGestures";
        private const string ModelingSaveFileNamePrefix = @"Modeling";


        //////////////////////////////////
        //// Component Initialization ////
        //////////////////////////////////

        public MainWindow()
        {
            InitializeComponent();
        }

        private void InitKinect()
        {
            /// Search for connected sensors
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            /// Init Data Streams
            if (this.sensor != null)
            {
                /// Color Stream
                //this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

                /// Depth Stream
                this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];
                this.depthcolorPixels = new byte[this.sensor.DepthStream.FramePixelDataLength * sizeof(int)];
                this.depthBitmap = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                /// Skeleton Stream
                //this.sensor.SkeletonStream.Enable();

                /// Init DTW
                _dtw = new DtwGestureRecognizer(_dimension, 30, 40, 13, 30); // 12, 10, 13, 20, 10    12, 20, 30, 40, 20
                _video = new ArrayList();
            }
        }


        //////////////////////////////////
        ////// Main Window Elements //////
        //////////////////////////////////

        /// Called when each depth frame is ready
        /// Does necessary processing to get our finger points and predicting gestures
        /// Most of the interesting stuff happens in here
        private void GestureDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            // Calculating and storing finger and palm positions
            Image<Gray, Byte> emguImg = convertToEmgu();
            CalculateAndStorePos(emguImg);
            this.emguImage.Source = BitmapSourceConvert.ToBitmapSource(emguImg);

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);

                    // Get the min and max reliable depth for the current frame
                    int minDepth = depthFrame.MinDepth;
                    int maxDepth = depthFrame.MaxDepth;

                    // Convert the depth to RGB
                    int colorPixelIndex = 0;
                    for (int i = 0; i < this.depthPixels.Length; ++i)
                    {
                        int x = i % this.sensor.DepthStream.FrameWidth;
                        int y = (int)(i / this.sensor.DepthStream.FrameWidth);

                        // Get the depth for this pixel
                        short depth = depthPixels[i].Depth;

                        // To convert to a byte, we're discarding the most-significant
                        // rather than least-significant bits.
                        // We're preserving detail, although the intensity will "wrap."
                        // Values outside the reliable depth range are mapped to 0 (black).

                        // Note: Using conditionals in this loop could degrade performance.
                        // Consider using a lookup table instead when writing production code.
                        // See the KinectDepthViewer class used by the KinectExplorer sample
                        // for a lookup table example.
                        byte intensity = (byte)(0);
                        //if (depth >= minDepth && depth <= threshDepth)
                        if (depth >= 180 && depth <= threshDepth)
                        {
                            intensity = (byte)(depth);
                        }
                        // Write out blue byte
                        this.depthcolorPixels[colorPixelIndex++] = intensity;

                        // Write out green byte
                        this.depthcolorPixels[colorPixelIndex++] = intensity;

                        // Write out red byte                        
                        this.depthcolorPixels[colorPixelIndex++] = intensity;

                        // We're outputting BGR, the last byte in the 32 bits is unused so skip it
                        // If we were outputting BGRA, we would write alpha here.
                        ++colorPixelIndex;
                    }

                    ///// Pass off to DTW
                    //currentBufferFrame.Text = _video.Count.ToString();

                    // If read is enabled
                    if (_video.Count > MinimumFrames && _capturing == true)
                    {
                        sample = new Matrix<float>(1, _dimension);
                        string[] features = _dtw.ExtractFeatures().Split(' ');
                        for (int i = 0; i < features.Length; i++)
                        {
                            int featureIndex;
                            if (Int32.TryParse(features[i], out featureIndex))
                                sample[0, i] = (float)featureIndex;
                        }

                        Gestures recordedGesture = EmguCVKNearestNeighbors.Predict(sample);
                        if (ready == false)
                        {
                            if (recordedGesture == Gestures.ReadySignal)
                            {
                                ready = true;
                                _video = new ArrayList();
                            }
                        }
                        else if (_video.Count == BufferSize && ready == true)
                        {
                            if (recordedGesture == Gestures.ReadySignal)
                            {
                                _video = new ArrayList();
                            }
                            else
                            {
                                string s = (recordedGesture.ToString());
                                ready = false;
                            }
                        }

                        results.Text = "Recognised as: " + recordedGesture.ToString();

                        if(SerialSender.GetSendState() && recordedGesture != Gestures.ReadySignal)
                        {                    
                            SerialSender.SendGesture(recordedGesture);
                            // Volume Commands require double pulse.
                            if (recordedGesture == Gestures.VolumeUp || recordedGesture == Gestures.VolumeDown)
                                SerialSender.SendGesture(recordedGesture);
                            SerialSender.SetSendState(false);
                            recordedGesture = Gestures.None;
                            imageBorder.BorderThickness = new Thickness(0);
                        }

                        if (recordedGesture == Gestures.ReadySignal)
                        {
                            imageBorder.BorderThickness = new Thickness(10);
                            SerialSender.SetSendState(true);
                        }


                        if (recordedGesture == Gestures.None)
                        {
                            // There was no match so reset the buffer
                            _video = new ArrayList();
                        }
                    }

                    // Ensures that we remember only the last x frames
                    if (_video.Count > BufferSize)
                    {
                        // If we are currently capturing and we reach the maximum buffer size then automatically store
                        if (_capturing)
                        {
                            DtwStoreClick(null, null);
                        }
                        else
                        {
                            // Remove the first // 2 frame in the buffer
                            for (int i = 0; i < 3; ++i)
                            {
                                _video.RemoveAt(0);
                            }
                        }
                    }

                    // Write the pixel data into our bitmap
                    this.depthBitmap.WritePixels(
                        new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                        this.depthcolorPixels,
                        this.depthBitmap.PixelWidth * sizeof(int),
                        0);
                }
            }
        }

        /// Runs when window is loaded
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            /// Initialize sensors and streams
            InitKinect();

            /// Start the stream
            if (this.sensor != null)
            {
                try
                {
                    sensor.Start();

                    /// Shows depth image
                    //this.depthImage.Source = this.depthBitmap;
                    this.sensor.DepthFrameReady += this.GestureDepthFrameReady;
                }
                catch (IOException)
                {
                    sensor = null;
                }
            }
        }

        /// Cleanup 
        private void WindowClosed(object sender, EventArgs e)
        {
            // Stop the sensor
            if (null != this.sensor)
                this.sensor.Stop();

            // Close the SerialSender communication
            SerialSender.SerialSenderShutdown();

            Environment.Exit(0);
        }


        //////////////////////////////////
        /////// Point Calculations ///////
        //////////////////////////////////

        /// Calculate finger position and send them to DTW for recording
        private void CalculateAndStorePos(Image<Gray, Byte> img)
        {
            /// Find all contours on screen
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(img, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);

            /// Find the biggest contour
            VectorOfPoint biggestContour = CalculateBiggestContour(img, contours);

            if (biggestContour != null)
            {
                /// Calculate midpoint of the biggest contour
                System.Drawing.Point midPoint = findHandPos(img, biggestContour);

                /// Calculate convexity defects
                Matrix<int> defects = CalculateConvexityDefects(img, biggestContour, contours);

                if(defects != null)
                    /// Extract finger points from defects and send to DTW
                    StorePoints(biggestContour, defects, midPoint);
            }
        }

        /// Calculate the max contour
        private VectorOfPoint CalculateBiggestContour(Image<Gray, Byte> img, VectorOfVectorOfPoint contours)
        {
            VectorOfPoint biggestContour = null;

            CvInvoke.FindContours(img, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);

            double calculatedArea = 0;
            double maxArea = 0;
            int largestContourIndex = 0;

            for (int i = 0; i < contours.Size; ++i)
            {
                calculatedArea = CvInvoke.ContourArea(contours[i]);
                if (calculatedArea > maxArea)
                {
                    maxArea = calculatedArea;
                    largestContourIndex = i;
                    biggestContour = contours[i];
                }
            }

            /// For debugging and training purposes
            /// Draws biggestContour
            //CvInvoke.DrawContours(img, contours, largestContourIndex, new MCvScalar(255, 0, 0), 5);

            return biggestContour;
        }

        /// Calculate convex hull and convexity defects for accurate finger calculation
        private Matrix<int> CalculateConvexityDefects(Image<Gray, Byte> img, VectorOfPoint biggestContour, VectorOfVectorOfPoint contours)
        {
            VectorOfPoint currentContour = new VectorOfPoint();
            VectorOfInt hullIndices = new VectorOfInt();

            CvInvoke.ApproxPolyDP(biggestContour, currentContour, CvInvoke.ArcLength(biggestContour, true) * .005, true);
            biggestContour = currentContour;
            CvInvoke.ConvexHull(biggestContour, hullIndices, false, false);

            /// Calcualate convexity defects
            /// Defects is a 4-element integer vector
            /// (start_index, end_index, farthest_pt_index, fixpt_depth)
            /// stored in a matrix where each row is a defect
            Matrix<int> defects = null;
            Mat mat = new Mat();

            CvInvoke.ConvexityDefects(biggestContour, hullIndices, mat);
            if (mat.Rows > 0)
            {
                defects = new Matrix<int>(mat.Rows, mat.Cols, mat.NumberOfChannels);
                mat.CopyTo(defects);

                /// For debugging and training purposes
                /// Draws finger points using convexity defects
                Matrix<int>[] channels = defects.Split();
                /// channel[0] = start_point, channel[1] = end_point, channel[2] = fixpt_depth

                for (int j = 0; j < defects.Rows; ++j)
                {
                    if (j < 5)
                        CvInvoke.Circle(img, System.Drawing.Point.Round(new System.Drawing.PointF(biggestContour[channels[0][j, 0]].X, biggestContour[channels[0][j, 0]].Y)), 10, new MCvScalar(255, 255, 255), 10);
                }
            }

            /// For debugging and training purposes
            /// Draws convex hull of biggest contour
            VectorOfPoint hullPoints = new VectorOfPoint();
            CvInvoke.ConvexHull(biggestContour, hullPoints, false);
            CvInvoke.Polylines(img, hullPoints.ToArray(), true, new MCvScalar(255, 255, 255), 10);

            return defects;
        }
        
        /// Store indices that are finger points and palm
        private void StorePoints(VectorOfPoint biggestContour, Matrix<int> defects, System.Drawing.Point midPoint)
        {
            Matrix<int>[] channels = defects.Split();
            /// channel[0] = start_point, channel[1] = end_point, channel[2] = fixpt_depth
            
            double[] points = new double[_dimension];
            Array.Clear(points, 0, _dimension);

            if (defects.Rows > 0)
            {
                points[0] = biggestContour[channels[0][0, 0]].X;
                points[1] = biggestContour[channels[0][0, 0]].Y;
            }
            if (defects.Rows > 1)
            {
                points[2] = biggestContour[channels[0][1, 0]].X;
                points[3] = biggestContour[channels[0][1, 0]].Y;
            }
            if (defects.Rows > 2)
            {
                points[4] = biggestContour[channels[0][2, 0]].X;
                points[5] = biggestContour[channels[0][2, 0]].Y;
            }
            if (defects.Rows > 3)
            {
                points[6] = biggestContour[channels[0][3, 0]].X;
                points[7] = biggestContour[channels[0][3, 0]].Y;
            }
            if (defects.Rows > 4)
            {
                points[8] = biggestContour[channels[0][4, 0]].X;
                points[9] = biggestContour[channels[0][4, 0]].Y;
            }

            points[10] = midPoint.X;
            points[11] = midPoint.Y;

            _video.Add(points);
        }

        /// Find average x and y value of hand
        private System.Drawing.Point findHandPos(Image<Gray, Byte> img, VectorOfPoint biggestContour)
        {
            // Get the avg of x values and y values of hand position
            int sumX = 0;
            int sumY = 0;
            int totalPixels = 0;
            double avgX = 0;
            double avgY = 0;

            for (int i = 0; i < biggestContour.Size; ++i)
            {
                sumX += biggestContour[i].X;
                sumY += biggestContour[i].Y;
                ++totalPixels;
            }

            if (totalPixels > 0)
            {
                avgX = (double)(sumX / totalPixels);
                avgY = (double)(sumY / totalPixels);
            }

            System.Drawing.Point handPos = new System.Drawing.Point((int) avgX, (int) avgY);
            //double[] handPos = new double[2];
            //handPos[0] = avgX;
            //handPos[1] = avgY;

            /// For debugging and training purposes
            /// Draw mid point
            CvInvoke.Circle(img, handPos, 10, new MCvScalar(255, 255, 255), 10);

            return handPos;
        }


        /////////////////////////////////
        //////// Window Elements ////////
        /////////////////////////////////

        /// Read mode. Sets our control variables and button enabled states
        private void DtwReadClick(object sender, RoutedEventArgs e)
        {
            // Set the buttons enabled state
            dtwRead.IsEnabled = false;
            Stop.IsEnabled = true;

            // Set the capturing? flag
            _capturing = true;

            // Update the status display
            imageBorder.BorderThickness = new Thickness(0);
            status.Text = "Reading";
        }

        /// Stops read mode
        private void StopRead(object sender, RoutedEventArgs e)
        {
            // Set the buttons enabled state
            dtwRead.IsEnabled = true;
            Stop.IsEnabled = false;

            // Set the capturing? flag
            _capturing = false;

            // Update the status display
            imageBorder.BorderThickness = new Thickness(0);
            status.Text = "Done Reading";
        }

        /// Starts a countdown timer to enable the player to get in position to record gestures
        private void DtwCaptureClick(object sender, RoutedEventArgs e)
        {
            _captureCountdown = DateTime.Now.AddSeconds(CaptureCountdownSeconds);

            _captureCountdownTimer = new Timer();
            _captureCountdownTimer.Interval = 50;
            _captureCountdownTimer.Start();
            _captureCountdownTimer.Tick += CaptureCountdown;
        }

        /// The method fired by the countdown timer. Either updates the countdown or fires the StartCapture method if the timer expires
        private void CaptureCountdown(object sender, EventArgs e)
        {
            if (sender == _captureCountdownTimer)
            {
                if (DateTime.Now < _captureCountdown)
                {
                    status.Text = "Wait " + ((_captureCountdown - DateTime.Now).Seconds + 1) + " seconds";
                }
                else
                {
                    _captureCountdownTimer.Stop();
                    status.Text = "Recording gesture";
                    StartCapture();
                }
            }
        }

        /// Capture mode. Sets our control variables and button enabled states
        private void StartCapture()
        {
            // Set the buttons enabled state
            dtwRead.IsEnabled = false;
            dtwCapture.IsEnabled = false;

            // Set the capturing? flag
            _capturing = true;

            // Clear the _video buffer and start from the beginning
            _video = new ArrayList();

            imageBorder.BorderThickness = new Thickness(10);
        }

        /// Stores our gesture to the DTW sequences list
        private void DtwStoreClick(object sender, RoutedEventArgs e)
        {
            // Set the buttons enabled state
            dtwRead.IsEnabled = false;
            dtwCapture.IsEnabled = true;

            // Set the capturing? flag
            _capturing = false;

            // Add the current video buffer to the dtw sequences list
            _dtw.AddOrUpdate(_video);

            // Scratch the _video buffer
            _video = new ArrayList();

            // Switch back to Read mode
            DtwReadClick(null, null);
        }

        /// Stores our gesture to the DTW sequences list
        private void DtwSaveToFile(object sender, RoutedEventArgs e)
        {
            counter.Text = "Recorded Gestures: " + (++gestureCount).ToString();

            string fileName_modeling = ModelingSaveFileNamePrefix + ".txt";
            System.IO.File.AppendAllText(GestureSaveFileLocation + fileName_modeling, _dtw.ExtractFeatures());
        }

        /// Reset gesture saved counter
        private void ResetCounter(object sender, RoutedEventArgs e)
        {
            gestureCount = 0;
            counter.Text = "Recorded Gestures: " + gestureCount.ToString();
        }


        //////////////////////////////////
        //////// Helper Functions ////////
        //////////////////////////////////

        /// Convert to Emgu
        private Image<Gray, Byte> convertToEmgu()
        {
            BitmapEncoder encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(this.depthBitmap));
            MemoryStream ms = new MemoryStream();

            encoder.Save(ms);
            Bitmap b = new Bitmap(ms);

            Image<Gray, Byte> img = new Image<Gray, Byte>(b);

            return img;
        }

        /// Convert Emug Image back to Bitmap for display
        public static class BitmapSourceConvert
        {
            [DllImport("gdi32")]
            private static extern int DeleteObject(IntPtr o);

            public static BitmapSource ToBitmapSource(IImage image)
            {
                using (System.Drawing.Bitmap source = image.Bitmap)
                {
                    IntPtr ptr = source.GetHbitmap();

                    BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        ptr,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                    DeleteObject(ptr);
                    return bs;
                }
            }
        }
    }
}