using RobotKit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


namespace SpheroPartyGame
{
    public sealed partial class MainPage : Page
    {
        private Sphero m_robot = null;
        private long m_lastCommandSentTimeMs;
        private bool m_gameIsStarted = false;

        private const string c_noSpheroConnected = "No Sphero Connected";
        private const string c_connectingToSphero = "Connecting to {0}";
        private const string c_spheroConnected = "Connected to {0}";

        public MainPage()
        {
            this.InitializeComponent();
        }
         
        //Начало работы. Переход на основной экран
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            SetupRobotConnection();
            Application app = Application.Current;
            app.Suspending += OnSuspending;
        }

        //Завершение работы. Переход с основного экрана
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            ShutdownRobotConnection();
           
            Application app = Application.Current;
            app.Suspending -= OnSuspending;
        }

        //handle the application entering the background
        private void OnSuspending(object sender, SuspendingEventArgs args)
        {
            ShutdownRobotConnection();
        }

        //Ищем робота и подключаемся к нему
        private void SetupRobotConnection()
        {
            SpheroName.Text = c_noSpheroConnected;

            RobotProvider provider = RobotProvider.GetSharedProvider();
            provider.DiscoveredRobotEvent += OnRobotDiscovered;
            provider.NoRobotsEvent += OnNoRobotsEvent;
            provider.ConnectedRobotEvent += OnRobotConnected;
            provider.FindRobots();
        }

        //Завершаем работу с роботом
        private void ShutdownRobotConnection()
        {
            if (m_robot != null)
            {
                m_robot.SensorControl.StopAll();
                m_robot.Sleep();                
                m_robot.Disconnect();

                ConnectionToggle.OffContent = "Disconnected";
                SpheroName.Text = c_noSpheroConnected;

                m_robot.SensorControl.AccelerometerUpdatedEvent -= OnAccelerometerUpdated;
               
                RobotProvider provider = RobotProvider.GetSharedProvider();
                provider.DiscoveredRobotEvent -= OnRobotDiscovered;
                provider.NoRobotsEvent -= OnNoRobotsEvent;
                provider.ConnectedRobotEvent -= OnRobotConnected;
            }
        }
       
        //Робот найден!
        private void OnRobotDiscovered(object sender, Robot robot)
        {          
            if (m_robot == null)
            {                
                RobotProvider provider = RobotProvider.GetSharedProvider();
                provider.ConnectRobot(robot);
                ConnectionToggle.OnContent = "Connecting...";
                m_robot = (Sphero)robot;
                SpheroName.Text = string.Format(c_connectingToSphero, robot.BluetoothName);
            }
        }

        //Робот не найден :(
        private void OnNoRobotsEvent(object sender, EventArgs e)
        {
            MessageDialog dialog = new MessageDialog(c_noSpheroConnected);
            dialog.DefaultCommandIndex = 0;
            dialog.CancelCommandIndex = 1;
            dialog.ShowAsync();
        }


        //Робот готов исполнять команды
        private void OnRobotConnected(object sender, Robot robot)
        {
            ConnectionToggle.IsOn = true;
            ConnectionToggle.OnContent = "Connected";
                       
            SpheroName.Text = string.Format(c_spheroConnected, robot.BluetoothName);
            SetRobotDefault();

            m_robot.SensorControl.Hz = 10;
            m_robot.SensorControl.AccelerometerUpdatedEvent += OnAccelerometerUpdated;
           
        }

        //Изменение местоположения робота
        private void OnAccelerometerUpdated(object sender, AccelerometerReading reading)
        {           
            if (m_gameIsStarted)
                MoveRobot(reading.X, reading.Y);
        }
        
        //Начальное состояние робота. Он зеленый и неподвижно стоит
        private void SetRobotDefault()
        {
            m_robot.SetHeading(0);
            m_robot.SetRGBLED(0, 255, 0);
            m_robot.Roll(0, 0);
        }

        //Герератор случайных цветов робота и интервала их смены
        public async void ChangeRobotColor()
        {
            int colorsCount = 20;
            Random r = new Random();

            List<Color> colors = new List<Color> { Color.FromArgb(100, 0, 255, 0) };
            for (int c = 0; c < colorsCount; c++)
                colors.Add(Color.FromArgb(100, (byte)r.Next(255), 0, (byte)r.Next(255)));

            List<int> miliseconds = new List<int>();
            for (int m = 0; m <= 5; m++)
                miliseconds.Add(r.Next(1000));

            while (true)
            {
                int colorNumber = r.Next(colorsCount);
                Color color = colors[colorNumber];
                int milisecond = miliseconds[r.Next(5)];

                m_robot.SetRGBLED(color.R, color.G, color.B);
                await Task.Delay(TimeSpan.FromMilliseconds(milisecond));
            }
        }

        //Вращение робота по кругу. На 10 градусов в милисекунду
        public void MoveRobot(float x, float y)
        {
            m_robot.SetHeading(0);

            int angleDegrees = 10;

            long milliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            if ((milliseconds - m_lastCommandSentTimeMs) > 1)
            {
                m_robot.Roll(angleDegrees, 0);
                m_lastCommandSentTimeMs = milliseconds;
            }
        }

        //Кнопка для запуска и остановки игры
        private void startGameBtn_Click(object sender, RoutedEventArgs e)
        {
            m_gameIsStarted = !m_gameIsStarted;

            if (m_gameIsStarted)
            {
                startGameBtn.Content = "Stop";
                ChangeRobotColor();
            }
            else
            {
                startGameBtn.Content = "Start";
                SetRobotDefault();
            }
        }

        //Кнопка для отключения робота
        private void ConnectionToggle_Toggled(object sender, RoutedEventArgs e)
        {
            ConnectionToggle.OnContent = "Connecting...";
            if (ConnectionToggle.IsOn)
            {
                if (m_robot == null)
                {
                    SetupRobotConnection();
                }
            }
            else
            {
                ShutdownRobotConnection();
            }
        }

    }
}
