using PcapDotNet.Core;
using PcapDotNet.Packets;
using PcapDotNet.Packets.IpV4;
using PcapDotNet.Packets.Transport;
using System;
using System.Numerics;
using System.Windows;
using LiveCharts;
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

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ReceiveTraffic_Click(object sender, RoutedEventArgs e)
        {
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

                trafficData.ItemsSource = dataGrid;

                otherBox.Items.Add(amountOther);
                tcpBox.Items.Add(amountTCP);
                udpBox.Items.Add(amountUDP);
            }
            else
            {
                pathBox.Items.Add("First choose file to analyze!!!");
            }
        }

        private void Calculate_Jitter(object sender, RoutedEventArgs e)
        {
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
                jitterBox.Text = ((int) calculateJitterOnGivenInterval(mostFrequentInterval)).ToString();   
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

        private double calculateJitterOnGivenInterval(double interval)
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
            if (inputFromBox.Text.Length > 0 && inputToBox.Text.Length > 0 && controlSumBox.Text.Length > 0)
            {
                try
                {
                    int startBit = Int32.Parse(inputFromBox.Text);
                    int endBit = Int32.Parse(inputToBox.Text);
                    int controlSum = Int32.Parse(controlSumBox.Text);
                    int currentSum = 0;
                    for (int i = startBit; i <= endBit; i++)
                    {
                        currentSum += packet.Buffer[i];
                    }
                    if (currentSum.Equals(controlSum))
                    {
                        packetsForJitter.Add(packet);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + "\n error parsing to value");
                }

            }

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

            IpV4Datagram ip = packet.Ethernet.IpV4;
            UdpDatagram udp = ip.Udp;

            double delayInSeconds = calculateDelayInSeconds(previousPacket, packet);
            totalDelay += delayInSeconds;
            double bytesPerSecond = calculateBytesPerSecond(packet, delayInSeconds);
            checkPacketForJitter(packet);

            index++;

            dataGrid.Add(new DataGrid() {
                Id = index,
                SourceIP = ip.Source,
                SourcePort = udp.SourcePort,
                DestinationIP = ip.Destination,
                DestinationPort = udp.DestinationPort,
                DelayInSeconds = delayInSeconds,
                BytesPerSecond = (int)bytesPerSecond
            });
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
        public int Jitter { get; set; }
    }
}
