using PcapDotNet.Core;
using PcapDotNet.Packets;
using PcapDotNet.Packets.IpV4;
using PcapDotNet.Packets.Transport;
using System.Windows;

namespace PSCF_Ethernet
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ReceiveTraffic_Click(object sender, RoutedEventArgs e)
        {
            OfflinePacketDevice selectedDevice = new OfflinePacketDevice(@"testPOLSL.pcap");

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
        }

        private void DispatcherHandler(Packet packet)
        {
            trafficBox.Items.Add(packet.Timestamp.ToString("yyyy-MM-dd hh:mm:ss.fff") + " length:" + packet.Length);

            IpV4Datagram ip = packet.Ethernet.IpV4;
            UdpDatagram udp = ip.Udp;

            if (ip != null && udp != null)
                trafficBox.Items.Add(ip.Source + ":" + udp.SourcePort + " -> " + ip.Destination + ":" + udp.DestinationPort + "\n");
            else
                trafficBox.Items.Add("\n");
        }

    }
}
