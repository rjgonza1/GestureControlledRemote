# GestureControlledRemote
Senior design project using the Kinect to gather gesture data, and send commands to a televsion set


## Goal
Our goal is to create a create a system in which a user can control their TV functionality through gestures. We are creating a hands-free, interactive experience that implements many popular technologies and theories, mainly data science, machine learning, and computational vision.

## Approach
Our general idea is to gather skeleton data points from the Kinect (roughly six points: middle of palm and the tip of each finger). We will then take that data set and use libraries like OpenCV and EmguCV to help us classify these data points. We're using a k-nearest neighbor classifier to predict all input gestures. After pre-modeling to find the optimal values of k, we can use the training data as a look-up table for future inputs. Depending on the gesture, our program will map it to a command, and send that information to an Atmega 328p through UART protocol communication. The Atmega will recieve the command and send a sequence of ones and zeros that the TV will recieve as a command.

## Included Open Source Projects
DTWGestureRecognition is an open source project that is used here as a template for collecting skeleton data points from the Kinect. We cut it down to only track a gesture sequence and extract that sequence into features that we can hand off to our machcine learning model.

Link to DTW Project Site:
https://archive.codeplex.com/?p=kinectdtw

For pre-modeling our data, we made use of the mltools library, which is a library written for student in CS 178 to help learn and utilize a wide array of machine learning models. A more detailed README file for that is included in the KNNPre-model directory.
