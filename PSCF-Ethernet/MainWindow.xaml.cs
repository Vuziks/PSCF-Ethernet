using PcapDotNet.Core;
using PcapDotNet.Packets;
using PcapDotNet.Packets.IpV4;
using PcapDotNet.Packets.Transport;
using System;
using System.Numerics;
using System.Windows;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using System.Collections.Generic;

namespace PSCF_Ethernet
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Variables
        //#####################################################
        List<DataGrid> dataGrid = new List<DataGrid>();
        List<Packet> packetsForJitter = new List<Packet>();

        Packet previousPacket = null;

        public double totalDelay = 0.0;

        public int amountOther = 0;
        public int amountTCP = 0;
        public int amountUDP = 0;
        public int index = 0;

        public string fileName = "";

        public ulong packetsTotal = 0;
        //#####################################################

        // TMP
        // Check work of chart
        //#####################################################
        //private Random rand = new Random(0);
        //private double[] RandomWalk(int points = 5, double start = 100, double mult = 50)
        //{
            // return an array of difting random numbers
        //    double[] values = new double[points];
        //    values[0] = start;
        //    for (int i = 1; i < points; i++)
        //        values[i] = values[i - 1] + (rand.NextDouble() - .5) * mult;
        //    return values;
        //}
        //#####################################################

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ReceiveTraffic_Click(object sender, RoutedEventArgs e)
        {
            // TMP
            // To check work of Chart
            //#####################################################
            // generate some random Y data
            //int pointCount = 5;
            //double[] ys1 = RandomWalk(pointCount);
            //double[] ys2 = RandomWalk(pointCount);
            //int[] ys0 = { amountTCP+1, amountUDP, amountOther+2 };

            // create series and populate them with data
            //var series1 = new LiveCharts.Wpf.ColumnSeries
            //{
            //   Title = "Group A",
            //    Values = new LiveCharts.ChartValues<int>(ys0)
            //};

            //var series2 = new LiveCharts.Wpf.ColumnSeries()
            //{
            //    Title = "Group B",
            //    Values = new LiveCharts.ChartValues<double>()
            //};

            // display the series in the chart control
            //cartChart.Series.Clear();
            //cartChart.Series.Add(series1);
            //cartChart.Series.Add(series2);
            //#####################################################

            if (packetsForJitter.Count > 0) packetsForJitter.Clear();
            if(fileName != "")
            {
                OfflinePacketDevice selectedDevice = new OfflinePacketDevice(fileName);

                // Open the capture file
                using (PacketCommunicator communicator =
                    selectedDevice.Open(65536,                                  // portion of the packet to capture
                                                                                // 65536 guarantees that the whole packet will be captured on all the link layers
                                        PacketDeviceOpenAttributes.Promiscuous, // promiscuous mode
                                        1000))                                  // read timeout
                {
                    // Read and dispatch packets until EOF is reached
                    communicator.ReceivePackets(0, DispatcherHandler);
                }

                // Fill DataGrid
                trafficData.ItemsSource = dataGrid;

                // Fill amount Box'y
                otherBox.Items.Add(amountOther);
                tcpBox.Items.Add(amountTCP);
                udpBox.Items.Add(amountUDP);

                // Create value to the series
                int[] ysTCP = { amountTCP };
                int[] ysUDP = { amountUDP };
                int[] ysOther = { amountOther };
                int[] ysAll = { amountTCP + amountUDP + amountOther };

                // create series and populate them with data
                var seriesTCP = new LiveCharts.Wpf.ColumnSeries
                {
                    Title = "TCP",
                    Values = new LiveCharts.ChartValues<int>(ysTCP)
                };
                var seriesUDP = new LiveCharts.Wpf.ColumnSeries
                {
                    Title = "UDP",
                    Values = new LiveCharts.ChartValues<int>(ysUDP)
                };
                var seriesOther = new LiveCharts.Wpf.ColumnSeries
                {
                    Title = "Other",
                    Values = new LiveCharts.ChartValues<int>(ysOther)
                };
                var seriesAll = new LiveCharts.Wpf.ColumnSeries
                {
                    Title = "All",
                    Values = new LiveCharts.ChartValues<int>(ysAll)
                };

                // display the series in the chart control
                cartChart.Series.Clear();
                cartChart.Series.Add(seriesTCP);
                cartChart.Series.Add(seriesUDP);
                cartChart.Series.Add(seriesOther);
                cartChart.Series.Add(seriesAll);
            }
            else
            {
                pathBox.Items.Add("First choose file to analyze!!!");
            }
        }

        private void Calculate_Jitter(object sender, RoutedEventArgs e)
        {
            jitterBox.Text = "";
            intervalBox.Text = "";

            if (packetsForJitter.Count > 1 && epsilonBox.Text.Length > 0) //jesli sa wypelnione te pola
            {
                var possibleIntervals = new Dictionary<double, int>(); //inicjalizacja zmiennych
                double marginOfError = Double.Parse(epsilonBox.Text);
                for (int i = 1; i < packetsForJitter.Count; i++)        //dla wszystkich pakietow dodanych do liczenia jittera
                {
                    double currentInterval = (packetsForJitter[i].Timestamp - packetsForJitter[i - 1].Timestamp).TotalMilliseconds; //policz interwał mieczy pakietami
                    if (possibleIntervals.Count < 1)        //jesli pierwszy interwał
                    {
                        possibleIntervals.Add(currentInterval, 1);
                    }
                    else
                    {
                        IncrementExistingOrAddNewInterval(possibleIntervals, marginOfError, currentInterval);
                    }
                }
                //wybieranie najczesciej wystepujacego interwalu
                double mostFrequentInterval = 0.0;
                int mostOccurences = 0;
                foreach (KeyValuePair<double, int> interval in possibleIntervals)
                {
                    if(mostOccurences < interval.Value)
                    {
                        mostOccurences = interval.Value;
                        mostFrequentInterval = interval.Key;
                    }
                }
                jitterBox.Text = ((int) calculateJitterOnGivenInterval((int) mostFrequentInterval)).ToString();
                intervalBox.Text = ((int) mostFrequentInterval).ToString();
            }
        }

        private static void IncrementExistingOrAddNewInterval(Dictionary<double, int> possibleIntervals, double marginOfError, double currentInterval)
        {
            List<double> intervals = new List<double>(possibleIntervals.Keys);
            foreach (double interval in intervals)       //sprobuj znalezc istniejacy interwał
            {
                if (Math.Abs(interval - currentInterval) < marginOfError)       //jesli jest juz taki interwał zikrementuj liczbe wystapien
                {
                    KeyValuePair<double, int> intervalToIncrement = new KeyValuePair<double, int>(interval, possibleIntervals[interval] + 1);
                    possibleIntervals.Remove(interval);
                    possibleIntervals.Add(intervalToIncrement.Key, intervalToIncrement.Value);
                    return;
                }
            }
            possibleIntervals.Add(currentInterval, 1); //jesli nie, dodaj taki interwał
        }

        private double calculateJitterOnGivenInterval(int interval)
        {
            Packet previousPacket = null;
            double unstabilityTotal = 0.0;
            foreach (Packet packet in packetsForJitter)
            {
                if(previousPacket == null)
                {
                    previousPacket = packet;
                }
                else
                {
                    double delay = Math.Abs((previousPacket.Timestamp - packet.Timestamp).TotalMilliseconds);
                    unstabilityTotal += Math.Abs(interval - delay);
                }
                previousPacket = packet;
            }
            double jitter = unstabilityTotal / packetsForJitter.Count;
            return jitter;
        }

        private void checkPacketForJitter(Packet packet)
        {
            if (inputFromBox1.Text.Length > 0 && inputToBox1.Text.Length > 0 && controlSumBox1.Text.Length > 0)
            {
                bool firstSumGood = false;
                bool secondSumGood = false;
                try
                {
                    firstSumGood = checkControlSumAndAdd(packet, inputFromBox1, inputToBox1, controlSumBox1);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + "\n error parsing to value");
                }
                if ((bool)checkSecondControlSum.IsChecked &&
                    inputFromBox2.Text.Length > 0 && inputToBox2.Text.Length > 0 && controlSumBox2.Text.Length > 0)
                {
                    secondSumGood = checkControlSumAndAdd(packet, inputFromBox2, inputToBox2, controlSumBox2);
                }
                
                if ((firstSumGood && !(bool)checkSecondControlSum.IsChecked) || ((bool)checkSecondControlSum.IsChecked && firstSumGood && secondSumGood))
                {
                    packetsForJitter.Add(packet);
                }
            }

        }

        private bool checkControlSumAndAdd(Packet packet, System.Windows.Controls.TextBox startBitBox,
            System.Windows.Controls.TextBox endBitBox,
            System.Windows.Controls.TextBox controlSumBox)
        {
            int startBit = Int32.Parse(startBitBox.Text);
            int endBit = Int32.Parse(endBitBox.Text);
            int controlSum = Int32.Parse(controlSumBox.Text);
            int currentSum = 0;
            for (int i = startBit; i <= endBit; i++)
            {
                currentSum += packet.Buffer[i];
            }
            if (currentSum.Equals(controlSum))
            {
               return true;
            }
            return false;
        }

        private void clearAll()
        {
            dataGrid.Clear();
            trafficData.ItemsSource = null;

            previousPacket = null;

            totalDelay = 0.0;

            amountOther = 0;
            amountTCP = 0;
            amountUDP = 0;
            index = 0;

            fileName = "";

            packetsTotal = 0;


            otherBox.Items.Clear();
            pathBox.Items.Clear();
            tcpBox.Items.Clear();
            udpBox.Items.Clear();

            cartChart.Series.Clear();
        }

        private void countPackets(Packet packet)
        {
            if (packet.Ethernet.EtherType.Equals(PcapDotNet.Packets.Ethernet.EthernetType.IpV4))
            {
                if (packet.Ethernet.IpV4.Protocol.ToString() == "Tcp")
                {
                    amountTCP++;
                }
                else if (packet.Ethernet.IpV4.Protocol.ToString() == "Udp")
                {
                    amountUDP++;
                }
            }
            else
            {
                amountOther++;
            }
        }

        private void DispatcherHandler(Packet packet)
        {
            //trafficBox.Items.Add(packet.Timestamp.ToString("yyyy-MM-dd hh:mm:ss.fff") + " length:" + packet.Length);
            if (previousPacket == null) 
                previousPacket = packet;

            countPackets(packet);
            packetsTotal++;

            IpV4Datagram ip = packet.Ethernet.IpV4;
            UdpDatagram udp = ip.Udp;

            double delayInSeconds = calculateDelayInSeconds(previousPacket, packet);
            totalDelay += delayInSeconds;
            double bytesPerSecond = calculateBytesPerSecond(packet, delayInSeconds);
            checkPacketForJitter(packet);

            index++;

            if(packetsTotal == 1958)
            {
                int x = 99;
            }

            try
            {
                dataGrid.Add(new DataGrid()
                {
                    Id = index,
                    SourceIP = ip.Source,
                    SourcePort = udp != null ? udp.SourcePort : (ushort)0,
                    DestinationIP = ip.Destination,
                    DestinationPort = udp != null ? udp.DestinationPort : (ushort)0,
                    DelayInSeconds = delayInSeconds,
                    BytesPerSecond = (int)bytesPerSecond
                });
            }
            catch (IndexOutOfRangeException e)
            {
                dataGrid.Add(new DataGrid()
                {
                    Id = index,
                    SourceIP = new IpV4Address("0.0.0.0"),
                    SourcePort = (ushort)0,
                    DestinationIP = new IpV4Address("0.0.0.0"),
                    DestinationPort = (ushort)0,
                    DelayInSeconds = delayInSeconds,
                    BytesPerSecond = (int)bytesPerSecond
                }); 
            }
        }
                
        private double calculateDelayInSeconds(Packet previousPacket, Packet currentPacket)
        {
            return (currentPacket.Timestamp - previousPacket.Timestamp).TotalSeconds;
        }

        private double calculateBytesPerSecond(Packet currentPacket, double delayInSeconds)
        {
            if (delayInSeconds > 0.00001)
            {
                return currentPacket.Length * 8 / delayInSeconds;
            }
            return 0;
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            // Clear all variables and contents from MainWindow
            clearAll();

            // Create OpenFileDialog 
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 
            dlg.DefaultExt = ".pcap";
            dlg.Filter = "Wireshark capture file (*.pcap)|*.pcap";

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                // Get file path
                fileName = dlg.FileName;
            }

            if(fileName != "")
            {
                pathBox.Items.Add(fileName);
            }
            else
            {
                pathBox.Items.Add("First choose file to analyze!!!");
            }
        }
    }

    public class DataGrid
    {
        public int Id { get; set; }
        public IpV4Address SourceIP { get; set; }
        public ushort SourcePort { get; set; }
        public IpV4Address DestinationIP { get; set; }
        public ushort DestinationPort { get; set; }
        public double DelayInSeconds { get; set; }
        public int BytesPerSecond { get; set; }
    }
}
