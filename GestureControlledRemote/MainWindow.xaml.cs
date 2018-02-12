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
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        KinectSensor sensor;

        /// Depth Pixels and Bitmap 
        private WriteableBitmap depthBitmap;
        private DepthImagePixel[] depthPixels;
        private byte[] depthcolorPixels;
        //private Image<Gray, Byte> handImage;
        private double threshDepth = 1000;

        /// DTW
        private DtwGestureRecognizer _dtw;

        /// Video
        private ArrayList _video;
        private const int MinimumFrames = 6;
        private bool _capturing;
        private const int BufferSize = 60; // 32
        /// <summary>
        /// ArrayList of coordinates which are recorded in sequence to define one gesture
        /// </summary>
        private DateTime _captureCountdown = DateTime.Now;
        /// <summary>
        /// ArrayList of coordinates which are recorded in sequence to define one gesture
        /// </summary>
        private Timer _captureCountdownTimer;
        /// <summary>
        /// The minumum number of frames in the _video buffer before we attempt to start matching gestures
        /// </summary>
        private const int CaptureCountdownSeconds = 3;
        /// <summary>
        /// Total number of framed that have occurred. Used for calculating frames per second
        /// </summary>
        private int _totalFrames;
        /// <summary>
        /// The 'last time' DateTime. Used for calculating frames per second
        /// </summary>
        private DateTime _lastTime = DateTime.MaxValue;
        /// <summary>
        /// How many frames occurred 'last time'. Used for calculating frames per second
        /// </summary>
        private int _lastFrames;
        /*
        /// <summary>
        /// How many skeleton frames to ignore (_flipFlop)
        /// 1 = capture every frame, 2 = capture every second frame etc.
        /// </summary>
        private const int Ignore = 2;
        /// <summary>
        /// Switch used to ignore certain skeleton frames
        /// </summary>
        private int _flipFlop;
        */


        /// <summary>
        /// Where we will save our gestures to. The app will append a data/time and .txt to this string
        /// </summary>
        // private const string GestureSaveFileLocation = @"H:\My Dropbox\Dropbox\Microsoft Kinect SDK Beta\DTWGestureRecognition\DTWGestureRecognition\";
        private const string GestureSaveFileLocation = @"C:\Users\joshu\Desktop\Recorded Gestures\";

        /// <summary>
        /// Where we will save our gestures to. The app will append a data/time and .txt to this string
        /// </summary>
        private const string GestureSaveFileNamePrefix = @"RecordedGestures";

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
                _dtw = new DtwGestureRecognizer(2, 10, 13, 20, 10); // 2, 7, 10, 15, 10
                _video = new ArrayList();
            }
        }

        /// <summary>
        /// Opens the sent text file and creates a _dtw recorded gesture sequence
        /// Currently not very flexible and totally intolerant of errors.
        /// </summary>
        /// <param name="fileLocation">Full path to the gesture file</param>
        public void LoadGesturesFromFile(string fileLocation)
        {
            int itemCount = 0;
            string line;
            string gestureName = String.Empty;

            // TODO I'm defaulting this to 2 here for now as it meets my current need but I need to cater for variable lengths in the future
            ArrayList frames = new ArrayList();
            double[] items = new double[2];

            // Read the file and display it line by line.
            System.IO.StreamReader file = new System.IO.StreamReader(fileLocation);
            while ((line = file.ReadLine()) != null)
            {
                if (line.StartsWith("@"))
                {
                    gestureName = line;
                    continue;
                }

                if (line.StartsWith("~"))
                {
                    frames.Add(items);
                    itemCount = 0;
                    items = new double[2];
                    continue;
                }

                if (!line.StartsWith("----"))
                {
                    items[itemCount] = Double.Parse(line);
                }

                itemCount++;

                if (line.StartsWith("----"))
                {
                    _dtw.AddOrUpdate(frames, gestureName);
                    frames = new ArrayList();
                    gestureName = String.Empty;
                    itemCount = 0;
                }
            }

            file.Close();
        }

        /// Capture and show Emgu image
        private void captureImage(object sender, RoutedEventArgs e)
        {
            Emgu.CV.UI.ImageViewer.Show(convertToEmgu());
        }

        /*
        // /Display emgu depth stream
        private void EmguDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            //this.emguImage.Source = BitmapSourceConvert.ToBitmapSource(convertToEmgu());
            ContourAndHull(convertToEmgu());
        }
        */
        

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


        /// Taken from open source KinectDTW project
        /// <summary>
        /// Called when each depth frame is ready
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Depth Image Frame Ready Event Args</param>
        private void GestureDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            ContourAndHull(convertToEmgu());
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
                        if (depth >= minDepth && depth <= threshDepth)
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

                    Seq.Text = "seq:" + _dtw.get_seq_count();


                    /// Pass off to DTW
                    currentBufferFrame.Text = _video.Count.ToString();

                    // We need a sensible number of frames before we start attempting to match gestures against remembered sequences
                    if (_video.Count > MinimumFrames && _capturing == false)
                    {
                        ////Debug.WriteLine("Reading and video.Count=" + video.Count);
                        string s = _dtw.Recognize(_video);
                        results.Text = "Recognised as: " + s;
                        if (!s.Contains("__UNKNOWN"))
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
                    
                    // Update the debug window with Sequences information
                    //dtwTextOutput.Text = _dtw.RetrieveText();

                    // Write the pixel data into our bitmap
                    this.depthBitmap.WritePixels(
                        new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                        this.depthcolorPixels,
                        this.depthBitmap.PixelWidth * sizeof(int),
                        0);

                    ++_totalFrames;

                    DateTime cur = DateTime.Now;
                    if (cur.Subtract(_lastTime) > TimeSpan.FromSeconds(1))
                    {
                        int frameDiff = _totalFrames - _lastFrames;
                        _lastFrames = _totalFrames;
                        _lastTime = cur;
                        frameRate.Text = frameDiff + " fps";
                    }
                }
            }
        }

        private double[] findHandPos(VectorOfPoint vp)
        {
            VectorOfPoint contours = vp;
            // Get the avg of x values and y values of hand position
            int sumX = 0;
            int sumY = 0;
            int totalPixels = 0;
            double avgX = 0;
            double avgY = 0;

            for (int i = 0; i < contours.Size; ++i)
            {
                sumX += contours[i].X;
                sumY += contours[i].Y;
                ++totalPixels;
            }

            if (totalPixels > 0)
            {
                avgX = (double)(sumX / totalPixels);
                avgY = (double)(sumY / totalPixels);
            }

            double[] tmp = new double[2];
            tmp[0] = avgX;
            tmp[1] = avgY;

            _video.Add(tmp);

            return tmp;
        }

        private VectorOfPoint ContourAndHull(Image<Gray, Byte> img)
        {

            // Find the max contour
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
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



            if (biggestContour != null)
            {
                VectorOfPoint currentContour = new VectorOfPoint();
                VectorOfPoint hull = new VectorOfPoint();
                CvInvoke.ApproxPolyDP(biggestContour, currentContour, CvInvoke.ArcLength(biggestContour, true) * .025, true);
                biggestContour = currentContour;

                CvInvoke.ConvexHull(biggestContour, hull, true);
                RotatedRect bound = CvInvoke.MinAreaRect(biggestContour);
                PointF[] vertices = bound.GetVertices();

                CvInvoke.DrawContours(img, contours, largestContourIndex, new MCvScalar(255, 0, 0), 5);
                CvInvoke.Polylines(img, hull.ToArray(), true, new MCvScalar(255, 255, 255), 10);

                for (int i = 0; i < hull.Size; ++i)
                {
                    CvInvoke.Circle(img, System.Drawing.Point.Round(new System.Drawing.PointF(hull[i].X, hull[i].Y)), 10, new MCvScalar(255, 255, 255), 10);
                }
            }           
            //double avgX = findHandPos(biggestContour)[0];
            //double avgY = findHandPos(biggestContour)[1];
            //int x = (int)avgX;
            //int y = (int)avgY;
            //Coords.Text = "(" + avgX + "," + avgY + ")";
            //CvInvoke.Circle(img, new System.Drawing.Point(x, y), 10, new MCvScalar(255, 255, 255), 10);

            this.emguImage.Source = BitmapSourceConvert.ToBitmapSource(img);

            return biggestContour;
        }

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
                    this.depthImage.Source = this.depthBitmap;
                    this.sensor.DepthFrameReady += this.GestureDepthFrameReady;

                    /// Shows emgu depth image
                    //this.sensor.DepthFrameReady += this.EmguDepthFrameReady;

                    /// Capture clicks
                    this.capture.Click += captureImage;
                }
                catch (IOException)
                {
                    sensor = null;
                }
            }
        }

        // Cleanup 
        private void WindowClosed(object sender, EventArgs e)
        {
            if (null != this.sensor)
                this.sensor.Stop();

            Environment.Exit(0);
        }


        
        /// DTW Window Elements
        
        /// <summary>
        /// Read mode. Sets our control variables and button enabled states
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Routed Event Args</param>
        private void DtwReadClick(object sender, RoutedEventArgs e)
        {
            // Set the buttons enabled state
            dtwRead.IsEnabled = false;
            dtwCapture.IsEnabled = true;
            dtwStore.IsEnabled = false;

            // Set the capturing? flag
            _capturing = false;

            // Update the status display
            status.Text = "Reading";
        }

        /// <summary>
        /// Starts a countdown timer to enable the player to get in position to record gestures
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Routed Event Args</param>
        private void DtwCaptureClick(object sender, RoutedEventArgs e)
        {
            _captureCountdown = DateTime.Now.AddSeconds(CaptureCountdownSeconds);

            _captureCountdownTimer = new Timer();
            _captureCountdownTimer.Interval = 50;
            _captureCountdownTimer.Start();
            _captureCountdownTimer.Tick += CaptureCountdown;
        }

        /// <summary>
        /// The method fired by the countdown timer. Either updates the countdown or fires the StartCapture method if the timer expires
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Event Args</param>
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

        /// <summary>
        /// Capture mode. Sets our control variables and button enabled states
        /// </summary>
        private void StartCapture()
        {
            // Set the buttons enabled state
            dtwRead.IsEnabled = false;
            dtwCapture.IsEnabled = false;
            dtwStore.IsEnabled = true;

            // Set the capturing? flag
            _capturing = true;

            ////_captureCountdownTimer.Dispose();

            status.Text = "Recording gesture" + gestureList.Text;

            // Clear the _video buffer and start from the beginning
            _video = new ArrayList();
        }

        /// <summary>
        /// Stores our gesture to the DTW sequences list
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Routed Event Args</param>
        private void DtwStoreClick(object sender, RoutedEventArgs e)
        {
            // Set the buttons enabled state
            dtwRead.IsEnabled = false;
            dtwCapture.IsEnabled = true;
            dtwStore.IsEnabled = false;

            // Set the capturing? flag
            _capturing = false;

            status.Text = "Remembering " + gestureList.Text;

            // Add the current video buffer to the dtw sequences list
            _dtw.AddOrUpdate(_video, gestureList.Text);
            results.Text = "Gesture " + gestureList.Text + "added";

            // Scratch the _video buffer
            _video = new ArrayList();

            // Switch back to Read mode
            DtwReadClick(null, null);
        }

        /// <summary>
        /// Stores our gesture to the DTW sequences list
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Routed Event Args</param>
        private void DtwSaveToFile(object sender, RoutedEventArgs e)
        {
            string fileName = GestureSaveFileNamePrefix + DateTime.Now.ToString("yyyy-MM-dd_HH-mm") + ".txt";
            System.IO.File.WriteAllText(GestureSaveFileLocation + fileName, _dtw.RetrieveText());
            status.Text = "Saved to " + fileName;
        }

        /// <summary>
        /// Loads the user's selected gesture file
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Routed Event Args</param>
        private void DtwLoadFile(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension
            dlg.DefaultExt = ".txt";
            dlg.Filter = "Text documents (.txt)|*.txt";

            dlg.InitialDirectory = GestureSaveFileLocation;

            // Display OpenFileDialog by calling ShowDialog method
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox
            if (result == true)
            {
                // Open document
                LoadGesturesFromFile(dlg.FileName);
                dtwTextOutput.Text = _dtw.RetrieveText();
                status.Text = "Gestures loaded!";
            }
        }

        /// <summary>
        /// Stores our gesture to the DTW sequences list
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Routed Event Args</param>
        private void DtwShowGestureText(object sender, RoutedEventArgs e)
        {
            dtwTextOutput.Text = _dtw.RetrieveText();
        }
    }
}