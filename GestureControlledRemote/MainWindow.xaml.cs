using System;
using System.Collections;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using Emgu.CV;
using System.IO;
using Emgu.CV.Structure;
using System.Drawing;
using System.Runtime.InteropServices;
using Emgu.CV.Util;
using Emgu.CV.CvEnum;

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
        private Image<Gray, Byte> handImage;
        private double threshDepth = 1000;

        /// DTW
        private DtwGestureRecognizer _dtw;

        /// Video
        private ArrayList _video;
        private const int MinimumFrames = 6;
        private bool _capturing;
        private const int BufferSize = 32;
        /// <summary>
        /// Switch used to ignore certain skeleton frames
        /// </summary>
        private int _flipFlop;

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
                _dtw = new DtwGestureRecognizer(2, 0.6, 2, 2, 10);
                _video = new ArrayList();
            }
        }

        /// Taken from open source KinectDTW project
        /// <summary>
        /// Called when each depth frame is ready
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Depth Image Frame Ready Event Args</param>
        private void GestureDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);

                    // Get the min and max reliable depth for the current frame
                    int minDepth = depthFrame.MinDepth;
                    int maxDepth = depthFrame.MaxDepth;

                    // Get the avg of x values and y values of hand position
                    int sumX = 0;
                    int sumY = 0;
                    int totalPixels = 0;
                    float avgX = 0;
                    float avgY = 0;

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
                            sumX += x;
                            sumY += y;
                            ++totalPixels;
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
                    if (totalPixels > 0)
                    {
                        avgX = (float)(sumX / totalPixels);
                        avgY = (float)(sumY / totalPixels);
                    }

                    ContourAndHull(convertToEmgu());
                    /// Pass off to DTW

                    // We need a sensible number of frames before we start attempting to match gestures against remembered sequences
                    if (_video.Count > MinimumFrames && _capturing == false)
                    {
                        ////Debug.WriteLine("Reading and video.Count=" + video.Count);
                        string s = _dtw.Recognize(_video);
                        if (!s.Contains("__UNKNOWN"))
                        {
                            // There was no match so reset the buffer
                            _video = new ArrayList();
                        }
                    }
                    
                    _video.Add(avgX);
                    _video.Add(avgY);

                    Coords.Text = "(" + avgX + "," + avgY + ")";
                    
                    

                    // Update the debug window with Sequences information
                    //dtwTextOutput.Text = _dtw.RetrieveText();



                    // Write the pixel data into our bitmap
                    this.depthBitmap.WritePixels(
                        new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                        this.depthcolorPixels,
                        this.depthBitmap.PixelWidth * sizeof(int),
                        0);

                    
                    
                }
            }
        }

        private void ContourAndHull(Image<Gray, Byte> img)
        {

            // Find the max contour
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            VectorOfPoint biggestContour = new VectorOfPoint();
            
            CvInvoke.FindContours(img, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);

            double calculatedArea = 0;
            double maxArea = 0;
            int largestContourIndex = 0;

            for(int i = 0; i < contours.Size; ++i)
            {
                calculatedArea = CvInvoke.ContourArea(contours[i]);
                if(calculatedArea > maxArea)
                {
                    maxArea = calculatedArea;
                    largestContourIndex = i;
                    biggestContour = contours[i];
                }   
            }

            CvInvoke.DrawContours(img, contours, largestContourIndex, new MCvScalar(255, 0, 0));
            
            // Extract and draw convex hull 
            // This part is in progress
            //VectorOfPoint currentContour = new VectorOfPoint();

            //if(biggestContour != null)
            //{
            //    // Toggle closed parameter if need to
            //    CvInvoke.ApproxPolyDP(biggestContour, currentContour, CvInvoke.ArcLength(biggestContour, true), true);


            //}
        }

        /// EmguCV Helper Methods
        // Capture and show Emgu image
        private void captureImage(object sender, RoutedEventArgs e)
        {
            Emgu.CV.UI.ImageViewer.Show(convertToEmgu());
        }

        // Display emgu depth stream
        private void EmguDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            this.emguImage.Source = BitmapSourceConvert.ToBitmapSource(convertToEmgu());
        }

        // Convert to Emgu
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

        // Converts image to display as bitmap
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
                    this.sensor.DepthFrameReady += this.EmguDepthFrameReady;

                    /// Capture clicks
                    this.capture.Click += captureImage;
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
            if (null != this.sensor)
                this.sensor.Stop();

            Environment.Exit(0);
        }
    }

}