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
        List<Packet> allCapturedPackets = new List<Packet>();

        Packet previousPacket = null;

        public double totalDelay = 0.0;

        public int amountOther = 0;
        public int amountTCP = 0;
        public int amountUDP = 0;
        public int index = 0;

        public string fileName = "";

        public ulong packetsTotal = 0;
        //#####################################################


        // Default Constructor
        //#####################################################
        public MainWindow()
        {
            InitializeComponent();
        }
        //#####################################################


        // OpenFile_Click - BUTTON
        //#####################################################
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

            if (fileName != "")
            {
                // Show file path in pathBox
                pathBox.Items.Add(fileName);
            }
            else
            {
                // Show appropriate message in pathBox
                pathBox.Items.Add("First choose file to analyze!!!");
            }
        }
        //#####################################################


        // ReceiveTraffic_Click - BUTTON
        //#####################################################
        private void ReceiveTraffic_Click(object sender, RoutedEventArgs e)
        {
            if (packetsForJitter.Count > 0) packetsForJitter.Clear();
            noPacketsLabel.Visibility = Visibility.Hidden;
            textBoxNotFilledLabel.Visibility = Visibility.Hidden;
            if (fileName != "")
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
                //otherBox.Items.Add(amountOther);
                //tcpBox.Items.Add(amountTCP);
                //udpBox.Items.Add(amountUDP);

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
                pathBox.Items.Clear();
                pathBox.Items.Add("First choose file to analyze!!!");
            }
        }
        //#####################################################


        // CalculateJitter_Click - BUTTON
        //#####################################################
        private void CalculateJitter_Click(object sender, RoutedEventArgs e)
        {
            noPacketsLabel.Visibility = Visibility.Hidden;
            textBoxNotFilledLabel.Visibility = Visibility.Hidden;

            jitterBox.Text = "";
            intervalBox.Text = "";
            if (packetsForJitter.Count > 0)
            {
                packetsForJitter.Clear();   
            }
            foreach (Packet packet in allCapturedPackets)
            {
                checkPacketForJitter(packet);
            }

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
                string notEnoughAccuracy = " < 0 ";
                if (((int)mostFrequentInterval).Equals(0))
                {
                    intervalBox.Text = notEnoughAccuracy;
                }
                else
                {
                    intervalBox.Text = ((int)mostFrequentInterval).ToString();
                }
            }
            else
            {
                if (packetsForJitter.Count == 0) noPacketsLabel.Visibility = Visibility.Visible;
                else if (epsilonBox.Text.Length < 1) textBoxNotFilledLabel.Visibility = Visibility.Visible;
            }
        }
        //#####################################################


        // MOST IMPORTANT - FUNCTION
        //#####################################################
        private void DispatcherHandler(Packet packet)
        {
            if (previousPacket == null)
                previousPacket = packet;

            countPackets(packet);

            IpV4Datagram ip = packet.Ethernet.IpV4;
            UdpDatagram udp = ip.Udp;

            double delayInSeconds = calculateDelayInSeconds(previousPacket, packet);
            totalDelay += delayInSeconds;
            double bytesPerSecond = calculateBytesPerSecond(packet, delayInSeconds);
            allCapturedPackets.Add(packet);

            index++;

            try
            {
                dataGrid.Add(new DataGrid()
                {
                    Id = index,
                    SourceIP = ip.Source,
                    SourcePort = udp != null ? udp.SourcePort : (ushort)0,
                    DestinationIP = ip.Destination,
                    DestinationPort = udp != null ? udp.DestinationPort : (ushort)0,
                    Protocol = packet.Ethernet.IpV4.Protocol.ToString(),
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
                    Protocol = "ERROR",
                    DelayInSeconds = delayInSeconds,
                    BytesPerSecond = (int)bytesPerSecond
                });
            }
        }
        //#####################################################


        // JITTER - FUNCTIONS
        //#####################################################
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
            else
            {
                textBoxNotFilledLabel.Visibility = Visibility.Visible;
            }

        }

        private bool checkControlSumAndAdd(Packet packet, System.Windows.Controls.TextBox startBitBox,
            System.Windows.Controls.TextBox endBitBox,
            System.Windows.Controls.TextBox controlSumBox)
        {
            int startBit = Int32.Parse(startBitBox.Text);
            int endBit = Int32.Parse(endBitBox.Text);
            string givenCharacterString = controlSumBox.Text;
            byte[] packetCharacterString = packet.Buffer.SubArray(startBit, endBit - startBit + 1);
            string hexPacketCharacterString = BitConverter.ToString(packetCharacterString);
            return hexPacketCharacterString.Equals(givenCharacterString, StringComparison.CurrentCultureIgnoreCase);
        }
        //#####################################################


        // CALCULATE - FUNCTIONS
        //#####################################################
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
        //#####################################################


        // CountPackets - FUNCTION
        //#####################################################
        private void countPackets(Packet packet)
        {
            try
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
            catch (IndexOutOfRangeException e)
            {
                amountOther++;
            }
        }
        //#####################################################


        // CLEAR_ALL - FUNCTION
        //#####################################################
        private void clearAll()
        {
            dataGrid.Clear();
            packetsForJitter.Clear();
            allCapturedPackets.Clear();
            trafficData.ItemsSource = null;

            previousPacket = null;

            totalDelay = 0.0;

            amountOther = 0;
            amountTCP = 0;
            amountUDP = 0;
            index = 0;

            fileName = "";

            packetsTotal = 0;


            pathBox.Items.Clear();
            //otherBox.Items.Clear();
            //tcpBox.Items.Clear();
            //udpBox.Items.Clear();

            cartChart.Series.Clear();
        }
        //#####################################################
    }


    // DataGrid - TABLE
    //#####################################################
    public class DataGrid
    {
        public int Id { get; set; }
        public IpV4Address SourceIP { get; set; }
        public ushort SourcePort { get; set; }
        public IpV4Address DestinationIP { get; set; }
        public ushort DestinationPort { get; set; }
        public string Protocol { get; set; }
        public double DelayInSeconds { get; set; }
        public int BytesPerSecond { get; set; }
    }
    //#####################################################


    // Extensions - CLASS
    //#####################################################
    public static class Extensions
    {
        public static T[] SubArray<T>(this T[] array, int offset, int length)
        {
            T[] result = new T[length];
            Array.Copy(array, offset, result, 0, length);
            return result;
        }
    }
    //#####################################################
}
