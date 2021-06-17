using PcapDotNet.Core;
using PcapDotNet.Packets;
using PcapDotNet.Packets.IpV4;
using PcapDotNet.Packets.Transport;
using System;
using System.Windows;

namespace PSCF_Ethernet
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public int TCPamount = 0;
        public int UDPamount = 0;
        public string fileName = "";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ReceiveTraffic_Click(object sender, RoutedEventArgs e)
        {
            if(fileName != "")
            {
                trafficBox.Items.Clear();

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

                tcpBox.Items.Add(TCPamount);
                udpBox.Items.Add(UDPamount);
            }
            else
            {
                trafficBox.Items.Add("First choose file to analyze!!!");
            }

            //OfflinePacketDevice selectedDevice = new OfflinePacketDevice(@"inputFile2.pcap");
            /*
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

            tcpBox.Items.Add(TCPamount);
            udpBox.Items.Add(UDPamount);
            */
        }

        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog 
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 
            dlg.DefaultExt = ".pcap";
            dlg.Filter = "Wireshark capture file (*.pcap)|*.pcap";

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();

            fileName = "";
            trafficBox.Items.Clear();
            tcpBox.Items.Clear();
            udpBox.Items.Clear();
            pathBox.Items.Clear();

            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                // Get file path
                fileName = dlg.FileName;
            }

            pathBox.Items.Add(fileName);
        }

        private void DispatcherHandler(Packet packet)
        {
            trafficBox.Items.Add(packet.Timestamp.ToString("yyyy-MM-dd hh:mm:ss.fff") + " length:" + packet.Length);

            CountTCP(packet);
            CountUDP(packet);

            IpV4Datagram ip = packet.Ethernet.IpV4;
            UdpDatagram udp = ip.Udp;

            if (ip != null && udp != null)
                trafficBox.Items.Add(ip.Source + ":" + udp.SourcePort + " -> " + ip.Destination + ":" + udp.DestinationPort + "\n");
            else
                trafficBox.Items.Add("\n");
        }

        private void CountTCP(Packet packet)
        {
            if(packet.IpV4.Tcp != null)
            {
                TCPamount++;
            }
        }

        private void CountUDP(Packet packet)
        {
            if (packet.IpV4.Udp != null)
            {
                UDPamount++;
            }
        }

    }
}
