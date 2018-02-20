import numpy as np
import matplotlib.pyplot as plt
import mltools as ml

# Parse data
gestures = np.genfromtxt("data/Modeling.txt", delimiter=None)
Y = gestures[:, -1]
X = gestures[:, 0:-1]
# Randomize data and split into 75/25 train/validation
np.random.seed(0)
X, Y = ml.shuffleData(X, Y)

Xtr, Xva, Ytr, Yva = ml.splitData(X, Y, .75)

### Scatter plot
# plt.figure(1)
# plt.title("Feature pair (1,2):")
# plt.plot(X[:,0], 'k.', X[:,1], 'b.', X[:,2], 'g.')
# plt.show()
#
# plt.figure(2)
# plt.title("Feature pair (1,3):")
# plt.plot(X[:,0], 'k.', X[:,2], 'g.')
#
# plt.figure(3)
# plt.title("Feature pair (1,4):")
# plt.plot(X[:,0], 'k.', X[:,1], 'r.')


# for i, k in enumerate(K):
#     plt.figure(i)
#     plt.title("K=" + str(k))
#     knn = ml.knn.knnClassify()  # Create object and train it
#     knn.train(Xtr, Ytr, k)
#     ml.plotClassify2D(knn, Xtr, Ytr)  # Visualize data set and decision regions

### Knn Neighbors
# K = [1, 2, 5, 10, 50, 100, 200]
#
# errTrain = []
# errVal = []
#
# # #Uses same neighbor array K
#
# for j, k in enumerate(K):
#     learner = ml.knn.knnClassify(Xtr, Ytr, k)
#     errVal.append(learner.err(Xva, Yva))
#     errTrain.append(learner.err(Xtr, Ytr))
#
# plt.figure(1)
# plt.title("All Features Error Rate")
# plt.semilogx(errTrain, 'r', errVal, 'g')
# plt.show()
