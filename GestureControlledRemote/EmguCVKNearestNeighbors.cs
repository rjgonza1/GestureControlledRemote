using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using System.Drawing;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.ML;
using Emgu.CV.ML.MlEnum;

namespace GestureControlledRemote
{
    class EmguCVKNearestNeighbors
    {
        /// Directory of gesture data
        //private const string TDFile = @"C:\Users\joshu\Desktop\Training Data\traindata.txt";
        private const string TDFile = @"GestureData.txt";

        /// 7 nearest neighbors found to be best from our premodeling phase
        private const int K = 7;
        private const int Features = 12;
        private const int trainSampleCount = 600; // Train all of the Data. (100% Train, 0% Verif)

        private Matrix<float> trainData;
        private Matrix<float> trainClasses;
        private KNearest knn;

        public EmguCVKNearestNeighbors()
        {
            this.trainData = new Matrix<float>(trainSampleCount, Features);
            this.trainClasses = new Matrix<float>(trainSampleCount, 1);

            this.knn = new KNearest();

            this.knn.DefaultK = K;
            this.knn.IsClassifier = true;
            ReadTrainingData(TDFile);
            this.knn.Train(trainData, DataLayoutType.RowSample, trainClasses);
        }

        /// Read gesture data and train the learner
        private void ReadTrainingData(String TrainingDataFileLocation)
        {
            int counter = 0;
            string line;

            // Read the file and fill trainData and trainClasses Matrices line by line.  
            System.IO.StreamReader file = new System.IO.StreamReader(TrainingDataFileLocation);
            while ((line = file.ReadLine()) != null)
            {
                String[] s = line.Split();
                for (int i = 0; i < trainData.Width; i++)
                {
                    trainData[counter, i] = float.Parse(s[i]);
                }
                trainClasses[counter, 0] = float.Parse(s[s.Length - 1]);
                counter++;
            }

            file.Close();
        }

        /// Predict gesture from sample
        public Gestures Predict(Matrix<float> sample)
        { 
            return (Gestures) knn.Predict(sample);
        }

        // Test Function
        //public void Funct1()
        //{
        //    int counter = 0;
        //    string line;

        //    // Read the file and fill trainData and trainClasses Matrices line by line.  
        //    System.IO.StreamReader file = new System.IO.StreamReader(TDFile);
        //    while ((line = file.ReadLine()) != null)
        //    {
        //        String[] s = line.Split();
        //        for (int i = 0; i < trainData.Width; i++)
        //        {
        //            trainData[counter, i] = float.Parse(s[i]);
        //        }
        //        trainClasses[counter, 0] = float.Parse(s[s.Length - 1]);
        //        counter++;
        //    }

        //    file.Close();
            
        //    // test sample class 0
        //    Matrix<float> sample0 = new Matrix<float>(1, Features); // 1x12 matrix
        //    float[] f0 = { 1, 5, -1, -3, 2, 0, 1, 2, -1, 2, 1, 1 };
        //    for (int i = 0; i < sample0.Width; i++)
        //    {
        //        sample0[0, i] = f0[i];
        //    }

        //    // test sample class 1
        //    Matrix<float> sample1 = new Matrix<float>(1, Features); // 1x12 matrix
        //    float[] f1 = { -30, 46, -29, 48, -22, 60, -26, 56, -23, 46, -2, 47 };
        //    for (int i = 0; i < sample1.Width; i++)
        //    {
        //        sample1[0, i] = f1[i];
        //    }
        //    //Matrix<float> results = new Matrix<float>(sample.Rows, 1); // 1x1
        //    //Matrix<float> neighborResponses;
        //    //neighborResponses = new Matrix<float>(sample.Rows, K); // 1xK
        //    //dist = new Matrix<float>(sample.Rows, K);
            
        //    using (KNearest knn = new KNearest())
        //    {
        //        knn.DefaultK = K;
        //        knn.IsClassifier = true;
        //        knn.Train(trainData, DataLayoutType.RowSample, trainClasses);

        //        // estimates the response and get the neighbors' labels
        //        float response0 = knn.Predict(sample0); //knn.FindNearest(sample, K, results, null, neighborResponses, null);
        //        float response1 = knn.Predict(sample1);
        //    }
        //}
    }
}
