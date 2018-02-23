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
        private double threshDepth = 800; // 1000

        /// DTW
        private DtwGestureRecognizer _dtw;
        private int _dimension = 12;

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
        private const string GestureSaveFileLocation = @"C:\Users\rjgon_000\Desktop\Recorded Gestures\";

        /// <summary>
        /// Where we will save our gestures to. The app will append a data/time and .txt to this string
        /// </summary>
        private const string GestureSaveFileNamePrefix = @"RecordedGestures";
        private const string ModelingSaveFileNamePrefix = @"Modeling";

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
            double[] items = new double[_dimension];

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
                    items = new double[_dimension];
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

            //_video.Add(tmp);

            return tmp;
        }

        private VectorOfPoint ContourAndHull(Image<Gray, Byte> img)
        {

            /// Find the max contour
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

            /// Calculate convex hull
            VectorOfPoint currentContour = new VectorOfPoint();
            VectorOfPoint hullPoints = new VectorOfPoint();
            VectorOfInt hullIndices = new VectorOfInt();
            double avgX = 0.0;
            double avgY = 0.0;
            if (biggestContour != null)
            {
                CvInvoke.ApproxPolyDP(biggestContour, currentContour, CvInvoke.ArcLength(biggestContour, true) * .005, true);
                biggestContour = currentContour;

                /// Calculate midpoint of the biggest contour
                avgX = findHandPos(biggestContour)[0];
                avgY = findHandPos(biggestContour)[1];
                int x = (int)avgX;
                int y = (int)avgY;

                CvInvoke.Circle(img, new System.Drawing.Point(x, y), 10, new MCvScalar(255, 255, 255), 10);

                CvInvoke.ConvexHull(biggestContour, hullIndices, false, false);
                CvInvoke.ConvexHull(biggestContour, hullPoints, false);

                /// Draw contours and convex hull
                CvInvoke.DrawContours(img, contours, largestContourIndex, new MCvScalar(255, 0, 0), 5);
                CvInvoke.Polylines(img, hullPoints.ToArray(), true, new MCvScalar(255, 255, 255), 10);

                /// Calcualate convexity defects
                /// Defects is a 4-element integer vector
                /// (start_index, end_index, farthest_pt_index, fixpt_depth)
                /// stored in a matrix where each row is a defect
                Mat mat = new Mat();
                CvInvoke.ConvexityDefects(biggestContour, hullIndices, mat);
                if(mat.Rows > 0)
                {
                    Matrix<int> defects = new Matrix<int>(mat.Rows, mat.Cols, mat.NumberOfChannels);
                    mat.CopyTo(defects);
                    /// channel[0] = start_point, channel[1] = end_point, channel[2] = fixpt_depth
                    Matrix<int>[] channels = defects.Split();

                    VectorOfPointF fingers = new VectorOfPointF();

                    for (int j = 0; j < defects.Rows; ++j)
                    {
                        if(j < 5)
                            CvInvoke.Circle(img, System.Drawing.Point.Round(new System.Drawing.PointF(biggestContour[channels[0][j, 0]].X, biggestContour[channels[0][j, 0]].Y)), 10, new MCvScalar(255, 255, 255), 10);
                    }

                    /// Store indices that are finger points and palm
                    double[] tmp = new double[_dimension];
                    Array.Clear(tmp, 0, _dimension);
                    if (defects.Rows > 0)
                    {
                        tmp[0] = biggestContour[channels[0][0, 0]].X;
                        tmp[1] = biggestContour[channels[0][0, 0]].Y;
                    }
                    if (defects.Rows > 1)
                    {
                        tmp[2] = biggestContour[channels[0][1, 0]].X;
                        tmp[3] = biggestContour[channels[0][1, 0]].Y;
                    }
                    if (defects.Rows > 2)
                    {
                        tmp[4] = biggestContour[channels[0][2, 0]].X;
                        tmp[5] = biggestContour[channels[0][2, 0]].Y;
                    }
                    if (defects.Rows > 3)
                    {
                        tmp[6] = biggestContour[channels[0][3, 0]].X;
                        tmp[7] = biggestContour[channels[0][3, 0]].Y;
                    }
                    if (defects.Rows > 4)
                    {
                        tmp[8] = biggestContour[channels[0][4, 0]].X;
                        tmp[9] = biggestContour[channels[0][4, 0]].Y;
                    }

                    tmp[10] = avgX;
                    tmp[11] = avgY;

                    _video.Add(tmp);
                }
            }

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

            string fileName_modeling = ModelingSaveFileNamePrefix + ".txt";
            //System.IO.File.WriteAllText(GestureSaveFileLocation + fileName_modeling, _dtw.RetrieveText1());
            System.IO.File.AppendAllText(GestureSaveFileLocation + fileName_modeling, _dtw.RetrieveText1());
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