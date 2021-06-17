using PcapDotNet.Core;
using PcapDotNet.Packets;
using PcapDotNet.Packets.IpV4;
using PcapDotNet.Packets.Transport;
using System;
using System.Numerics;
using System.Windows;
using LiveCharts;
using LiveCharts.Wpf;

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

        Packet previousPacket = null;
        ulong packetsTotal = 0;
        double totalDelay = 0.0;

        private void ReceiveTraffic_Click(object sender, RoutedEventArgs e)
        {
            OfflinePacketDevice selectedDevice = new OfflinePacketDevice("E:/POLSL/PSCF_PROJ/PSCF-Ethernet/testPOLSL2.pcap");

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

            if (previousPacket == null) previousPacket = packet;
            packetsTotal++;

            IpV4Datagram ip = packet.Ethernet.IpV4;
            UdpDatagram udp = ip.Udp;

            double delayInSeconds = calculateDelayInSeconds(previousPacket, packet);
            totalDelay += delayInSeconds;
            double bytesPerSecond = calculateBytesPerSecond(packet, delayInSeconds);
            double jitter = calculateJitter();


            if (ip != null && udp != null)
                trafficBox.Items.Add(ip.Source + ":" + udp.SourcePort + " -> " + ip.Destination + ":" + udp.DestinationPort + "\n" +
                    "delay: " + delayInSeconds + "\n" + "bytes per second: " + (int) bytesPerSecond  +
                    "\n" + "jitter: " + (int) jitter);
            else
                trafficBox.Items.Add("\n");

            previousPacket = packet;
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

    }
}
