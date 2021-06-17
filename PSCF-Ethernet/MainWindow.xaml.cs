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
            if (packet.Ethernet.IpV4.Protocol.ToString() == "Tcp")
            {
                amountTCP++;
            }
            else if (packet.Ethernet.IpV4.Protocol.ToString() == "Udp")
            {
                amountUDP++;
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
            double jitter = calculateJitter();

            index++;

            dataGrid.Add(new DataGrid() { Id = index, SourceIP = ip.Source, SourcePort = udp.SourcePort, DestinationIP = ip.Destination, DestinationPort = udp.DestinationPort, DelayInSeconds = delayInSeconds, BytesPerSecond = (int)bytesPerSecond, Jitter = (int)jitter });
        }
        
        private double calculateJitter()
        {
            return totalDelay * 1000 / packetsTotal;
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
