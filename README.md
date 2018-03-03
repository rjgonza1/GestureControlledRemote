# GestureControlledRemote
Senior design project using the Kinect to gather gesture data, and send commands to a televsion set


## Goal
Our goal is to create a create a system in which a user can control their TV functionality through gestures. We are creating a hands-free, interactive experience that implements many popular technologies and theories, mainly data science, machine learning, and computational vision.

## Approach
Our general idea is to gather skeleton data points from the Kinect (roughly six points: middle of palm and the tip of each finger). We will then take that data set and use libraries like OpenCV and EmguCV to help us classify these data points. After some supervised training, our program should be able to make accurate predictions (we haven't decided what kind of classifier to use yet). Depending on the gesture, our program will label map it to a command, and send that information to a RasberryPi 3 via sockets. The Pi 3 will read that command, and run a program that will be deployed onto it. This program will make use of some PWM and a infrared circuit that will be connected to the Pi through its GPIO pins to send a specific IR command to a receiving TV.

## Included Open Source Projects
DTWGestureRecognition is an open source project that is used here as a template for collecting skeleton data points from the Kinect. We will be modifying this to monitor the hand rather than the entire body

Link to DTW Project Site:
https://archive.codeplex.com/?p=kinectdtw
