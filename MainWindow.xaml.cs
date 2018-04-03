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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

// how did I figure this out?
// https://docs.microsoft.com/en-us/dotnet/framework/wpf/graphics-multimedia/animation-overview#example-make-an-element-fade-in-and-out-of-view
// https://stackoverflow.com/questions/23591106/create-a-blink-animation-in-wpf-in-code-behind


namespace PulseWidthDemo {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        bool loaded = false; // we need this because the updateAnimation function might be called before the UI is loaded

        Storyboard story;
        DoubleAnimationUsingKeyFrames anim;

        // the refresh rate of the line might not actually map to the rate of the robot
        // so here is a simple mapping (rounded to nearest 10%)
        // this could be adjusted experimentally

        Dictionary<double, double> speedLookup;

        int frameRate = 60; // default WPF frame rate
        double updateInterval = 1; // our animation loop runs every this many seconds
        double pwmRate = 1; // this goes from 1 (full speed) to 0 (no speed)

        // for simulating what the robot should be doing
        DispatcherTimer robotTimer;
        double robotPixelsPerSecond = 40; // the expected robot speed
        int robotTimerUpdateMilliseconds = 500;

        public MainWindow() {
            InitializeComponent();

            // set up some stuff we need
            story = new Storyboard();
            speedLookup = new Dictionary<double, double> {
                {0,0},{0.1,0.1},{0.2,0.2},{0.3,0.3},{0.4,0.4},{0.5,0.5},
                {0.6,0.6},{0.7,0.7},{0.8,0.8},{0.9,0.9},{1,1}
            };

            // the animation just toggles opacity between 0 and 1
            // we will give it initial settings, we can (and will) change it later
            anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
            anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1))));
            anim.Duration = new Duration(TimeSpan.FromSeconds(1));
            anim.RepeatBehavior = RepeatBehavior.Forever;

            // the storyboard just manages the animation. why? I don't know. it just does.
            story = new Storyboard();
            story.Children.Add(anim);
            Storyboard.SetTarget(anim, line);
            Storyboard.SetTargetProperty(anim, new PropertyPath(Rectangle.OpacityProperty));

            // we will use this timer to simulate how we'd expect the robot to move at
            // the specified pwm rate. that way we can see whether there is indeed a 1:1
            // mapping between pwm rate and actual robot speed, or not
            // for example, if we set our PWM to 80%, but the actual robot moves slower
            // than it should, we can adjust that in speedLookup (so that when we ask
            // for 80% speed, we actually get a faster pwm rate so that the robot
            // really moves 80% of full speed)

            robotTimer = new DispatcherTimer();
            robotTimer.Interval = TimeSpan.FromMilliseconds(robotTimerUpdateMilliseconds);
            robotTimer.Tick += RobotTimer_Tick;
        }

        // call this any time we change the UI, for now
        // this does a few things (see the comments)
        public void updateAnimation() {
            if (loaded) {
            // first, update the frame rate
            int newFrameRate = Int32.Parse(txtFrameRate.Text);
            frameRate = newFrameRate;
            anim.SetValue(Timeline.DesiredFrameRateProperty, frameRate);

            // now reset the animation timing
            double newUpdateInterval = Double.Parse(txtUpdateInterval.Text);
            updateInterval = newUpdateInterval;

            double newPwmRate = slider.Value;
            pwmRate = newPwmRate;

            // now, change the animation (anim) to make it the way we want it
            // the animation will run at updateInterval
            // as we decrease this value, our animation should get smoother (but perf might decline?)
            anim.Duration = new Duration(TimeSpan.FromSeconds(updateInterval));

            // now, we'll shift our keyframes based on the pwmRate and update interval
            // so if our pwm is 0.6, we stay on 0.6*interval, then go off for (1-0.6)*interval

            // HOWEVER! we don't know that rate of flickering the line will exactly match the
            // speed of the robot. in fact, it probably won't. but we can probably approximate it
            // well enough with a little testing. hence, we use speedLookup to take our desired robot
            // speed (say, 60%), and map it to the pulse rate that gives us about 60% speed (which could be
            // something like 0.7). a 1:1 mapping might be good enough, but this lookup will give us the
            // ability to customize
            double realPercentageOn = speedLookup[Math.Round(pwmRate,1)];

            // the amount of time we stay on is percentage on * our interval rate
            double timeOn = realPercentageOn * updateInterval;
            // and we are off the rest of the time
            double timeOff = (1.0 - realPercentageOn) * updateInterval;

            // now, change our keyframes
            // the line will be on for timeOn, then go off for the rest of the loop (which is updateInterval seconds long)
            anim.KeyFrames[0].KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0));
            anim.KeyFrames[1].KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(timeOn));

            // start the animation via its storyboard
            story.Begin();

            // how does fps fit in? well higher should give us smoother pwm animation
            // no idea what happens when the animation doesn't match the framerate
        }
        }

        private void Line_Loaded(object sender, RoutedEventArgs e) {
            
            anim.SetValue(Timeline.DesiredFrameRateProperty, 100);
            
            //DoubleAnimation anim = new DoubleAnimation();
            //anim.From = 1;
            //anim.To = 0;
            
            //anim.AutoReverse = true;

            story.Begin();
        }

        private void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            updateAnimation();
        }

        private void btnUpdate_Click(object sender, RoutedEventArgs e) {
            updateAnimation();
        }

        private void btnRunSim_Click(object sender, RoutedEventArgs e) {
            // move the fake robot to the start of the line
            Canvas.SetLeft(robot, Canvas.GetLeft(line) - robot.Width / 2);
            Canvas.SetTop(robot, Canvas.GetTop(line) - robot.Height / 2 + line.Height / 2);

            // check the speed
            int newSpeed = Int32.Parse(txtRobotSpeed.Text);
            robotPixelsPerSecond = newSpeed;

            // and start the timer
            robotTimer.Start();
        }

        // move our fake robot
        private void RobotTimer_Tick(object sender, EventArgs e) {
            Canvas.SetLeft(robot, Canvas.GetLeft(robot) + robotPixelsPerSecond * (robotTimerUpdateMilliseconds / 1000.0));
            if (Canvas.GetLeft(robot) > Canvas.GetLeft(line) + line.Width) {
                robotTimer.Stop();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            loaded = true;
        }
    }
}
