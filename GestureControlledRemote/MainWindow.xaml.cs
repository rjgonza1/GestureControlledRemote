using System;
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

        /// <summary>
        ///  Hand Detection Variables
        /// </summary>
        int backgroundFrame = 500;

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
            }
        }

        /// Capture and show Emgu image
        private void captureImage(object sender, RoutedEventArgs e)
        {
            Emgu.CV.UI.ImageViewer.Show(convertToEmgu());
        }

        // /Display emgu depth stream
        private void EmguDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            this.emguImage.Source = BitmapSourceConvert.ToBitmapSource(convertToEmgu());
        }

        /// Called each time a Depth Frame is ready. Passes Hand data to DTW Processor
        private void EmguHandExtractDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            CvInvoke.NamedWindow("Frame");
            CvInvoke.NamedWindow("Background");

            // Get hand position and then process hand data.
            Image<Gray, Byte> eimage = convertToEmgu();
            Mat frame = CvInvoke.CvArrToMat(eimage);
            Mat back = new Mat();
            Mat fore = new Mat();
            
            List<Tuple<PointF,double>> palm_centers = new List<Tuple<PointF, double>>();
            BackgroundSubtractorMOG2 bg = new BackgroundSubtractorMOG2();

            //Update the current background model and get the foreground
            if (backgroundFrame>0)
            {
                bg.Apply(frame, fore, backgroundFrame--);
            }
            else
            {
                bg.Apply(frame, fore, 0);
            }

            //Find the contours in the foreground
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            Mat h = new Mat();
            CvInvoke.FindContours(fore, contours, h, RetrType.External, ChainApproxMethod.ChainApproxNone);
            for (int i=0;i<contours.Size;i++)
            {
                //Ignore all small insignificant areas
                if (CvInvoke.ContourArea(contours[i])>=5000)
                {
                    //Draw contour
                    VectorOfVectorOfPoint tcontours = new VectorOfVectorOfPoint();
                    tcontours.Push(contours[i]);
                    CvInvoke.DrawContours(frame, tcontours, -1, new MCvScalar(0, 0, 255), 2);

                    //Detect Hull in current contour
                    VectorOfVectorOfPoint hulls = new VectorOfVectorOfPoint();
                    VectorOfVectorOfInt hullsI = new VectorOfVectorOfInt();
                    CvInvoke.ConvexHull(tcontours[0], hulls[0], false);
                    CvInvoke.ConvexHull(tcontours[0], hullsI[0], false);
                    CvInvoke.DrawContours(frame, hulls, -1, new MCvScalar(0, 255, 0), 2);

                    //Find Convex Defects
                    VectorOfVectorOfInt defects = new VectorOfVectorOfInt();
                    if (hullsI[0].Size>0)
                    {
                        System.Windows.Point rough_palm_center = new System.Windows.Point();
                        CvInvoke.ConvexityDefects(tcontours[0], hullsI[0], defects);
                        if (defects.Size>=3)
                        {
                            VectorOfPoint palm_points = new VectorOfPoint();
                            for (int j = 0; j < defects.Size; j++)
                            {
                                int startidx = defects[j][0]; System.Windows.Point ptStart = new System.Windows.Point(tcontours[0][startidx] );
                                int endidx = defects[j][1]; System.Windows.Point ptEnd = new System.Windows.Point(tcontours[0][endidx] );
                                int faridx = defects[j][2]; System.Windows.Point ptFar(tcontours[0][faridx] );
                                //Sum up all the hull and defect points to compute average
                                rough_palm_center += ptFar + ptStart + ptEnd;
                                palm_points.push_back(ptFar);
                                palm_points.push_back(ptStart);
                                palm_points.push_back(ptEnd);
                            }
                        }
                    }
                }
            }
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
                        byte intensity = (byte)(depth >= minDepth && depth <= maxDepth ? depth : 0);

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

                    // Write the pixel data into our bitmap
                    this.depthBitmap.WritePixels(
                        new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                        this.depthcolorPixels,
                        this.depthBitmap.PixelWidth * sizeof(int),
                        0);              
                }
            }
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

        // Cleanup 
        private void WindowClosed(object sender, EventArgs e)
        {
            if (null != this.sensor)
                this.sensor.Stop();

            Environment.Exit(0);
        }
    }

}