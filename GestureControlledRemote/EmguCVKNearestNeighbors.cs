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
    static class EmguCVKNearestNeighbors
    {
        /// Directory of gesture data
        private const string TDFile = @"GestureData.txt";

        /// 7 nearest neighbors found to be best from our premodeling phase
        private const int K = 7;
        private const int Features = 12;
        private const int trainSampleCount = 600; // Train all of the Data. (100% Train, 0% Verif)

        private static Matrix<float> trainData;
        private static Matrix<float> trainClasses;
        private static KNearest knn;

        /// Static constructor
        static EmguCVKNearestNeighbors()
        {
            trainData = new Matrix<float>(trainSampleCount, Features);
            trainClasses = new Matrix<float>(trainSampleCount, 1);

            knn = new KNearest();

            knn.DefaultK = K;
            knn.IsClassifier = true;
            ReadTrainingData(TDFile);
            knn.Train(trainData, DataLayoutType.RowSample, trainClasses);
        }

        /////////////////////////////////
        /////// Learner Functions ///////
        /////////////////////////////////

        /// Read gesture data and train the learner
        private static void ReadTrainingData(String TrainingDataFileLocation)
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
        public static Gestures Predict(Matrix<float> sample)
        { 
            return (Gestures) knn.Predict(sample);
        }
    }
}
