import numpy as np
import matplotlib.pyplot as plt
import mltools as ml

# Parse data
gestures = np.genfromtxt("data/GestureData.txt", delimiter=None)
# input = np.genfromtxt("data/Input.txt", delimiter=None)

Y = gestures[:, -1]
X = gestures[:, 0:-1]

errTrain = []
errVal = []

# Randomize data and split into 75/25 train/validation
np.random.seed(0)
X, Y = ml.shuffleData(X, Y)
Xtr, Xva, Ytr, Yva = ml.splitData(X, Y, .75)

### Knn Neighbors
# For comparing two classes
# K = [1, 2, 5, 10, 50]

# For the entire training set
K = [7, 14, 21, 28, 35]

# for i, k in enumerate(K):
#     plt.figure(i)
#     plt.title("K=" + str(k))
#     knn = ml.knn.knnClassify()  # Create object and train it
#     knn.train(Xtr, Ytr, k)
#     ml.plotClassify2D(knn, Xtr, Ytr)  # Visualize data set and decision regions
#     plt.show()

#Uses same neighbor array K

for j, k in enumerate(K):
    learner = ml.knn.knnClassify(Xtr, Ytr, k)
    errVal.append(learner.err(Xva, Yva))
    errTrain.append(learner.err(Xtr, Ytr))

plt.figure(1)
plt.title("All Features Error Rate")
plt.semilogx(errTrain, 'r', errVal, 'g')
plt.show()


# Testing prediction wiht k=5
# knn = ml.knn.knnClassify()
# knn.train(Xtr, Ytr, 5)
# print(knn.predict(input))
