﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System;
using System.Security.Cryptography;
using System.Net.Http.Headers;

public class Client : MonoBehaviour
{
    public static Client instance;
    public static int dataBufferSize = 4096;
    public static int MaxPlayers { get; private set; } = 10;

    public delegate void PacketHandler(Packet packet);
    public static Dictionary<int, PacketHandler> packetHandlers;
    public static Dictionary<int, Peer> peers = new Dictionary<int, Peer>();

    private static TcpListener tcpListener;
    private static UdpClient udpListener;


    //Server Info
    private string serverIP = "127.0.0.1";
    public int serverPort = 26950;
    public TCP server { get; private set; }

    //Client Info
    public static string MyIP;
    public static int MyPort;
    public int myId = 0;
    private bool isConnected = false;

    public int clientExeID;

    #region Singleton
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(this);
        }
    }
    #endregion

    public void GoOnline(string ip, int port)
    {
        serverIP = ip;
        serverPort = port;

        //Depending on the id set on inspector, set a different ip and port to listen to peers
        MyIP = $"127.0.0.{1 + clientExeID}";
        MyPort = 5000 + clientExeID;


        InitializeData();
        ConnectToServer();
        ListenToPeers();
    }

    private void ConnectToServer()
    {
        server = new TCP();

        isConnected = true;
        server.Connect(serverIP, serverPort);
    }

    private void ListenToPeers()
    {
        tcpListener = new TcpListener(IPAddress.Parse(MyIP), MyPort);
        tcpListener.Start();
        tcpListener.BeginAcceptTcpClient(new AsyncCallback(TCPConnectCallback), null);

        udpListener = new UdpClient(MyPort);
        udpListener.BeginReceive(UDPReceiveCallback, null);

        Debug.Log($"Client started listening on {MyPort}");
    }

    #region Callbacks
    //Incoming Peer requests to connect
    private static void TCPConnectCallback(IAsyncResult result)
    {
        TcpClient client = tcpListener.EndAcceptTcpClient(result);
        //Once it connects we want to still keep on listening for more clients so we call it again
        tcpListener.BeginAcceptTcpClient(new AsyncCallback(TCPConnectCallback), null);
        Debug.Log($"Incoming connection from {client.Client.RemoteEndPoint}...");

        //Give peer an id and add to the list of peers
        for (int i = 1; i <= MaxPlayers; i++)
        {
            if (peers[i].tcp.socket == null)
            {
                peers[i].tcp.Connect(client);
                return;
            }
        }
        Debug.Log($"{client.Client.RemoteEndPoint} failed to connect: Server full.");
    }

    private static void UDPReceiveCallback(IAsyncResult result)
    {
        try
        {
            IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Parse(MyIP), MyPort);
            byte[] data = udpListener.EndReceive(result, ref clientEndPoint);
            udpListener.BeginReceive(UDPReceiveCallback, null);
            Debug.Log("1");

            if (data.Length < 4)
            {
                Debug.Log("<4");
                return;
            }

            using (Packet packet = new Packet(data))
            {
                int clientId = packet.ReadInt();

                if (clientId == 0)
                {
                    Debug.Log("clientId == 0");
                    return;
                }

                if (peers[0].udp.endPoint == null)
                {
                    Debug.Log("UDP connected");
                    //Client.peers[clientId].udp.Connect(((IPEndPoint)Client.peers[clientId].tcp.socket.Client.LocalEndPoint).Address,((IPEndPoint)Client.peers[clientId].tcp.socket.Client.LocalEndPoint).Port);
                    Client.peers[0].udp.Connect(((IPEndPoint)Client.peers[0].tcp.socket.Client.LocalEndPoint));
                    return;
                }

                //verifiy if the endpoint corresponds to the endpoint that sent the data
                //this is for security reasons otherwise hackers could inpersonate other clients by send a clientId that does not corresponds to them
                //without the string conversion even if the endpoint matched it returned false
                if (peers[0].udp.endPoint.Equals(clientEndPoint))
                {
                    Debug.Log($"Handle data, peerID:{clientId}");

                    //peers[clientId].udp.HandleData(data);
                    peers[0].udp.HandleData(packet);
                }
                Debug.Log("4");
            }
        }
        catch (Exception ex)
        {
            Debug.Log($"Error receiving UDP data: {ex}");
        }
    }

    #endregion

    public static void SendUDPData(IPEndPoint peerEndPoint, Packet packet)
    {
        try
        {
            //Send my id so who receives it knows who sent it
            packet.InsertInt(Client.instance.myId);

            if (peerEndPoint != null)
            {
                udpListener.BeginSend(packet.ToArray(), packet.Length(), peerEndPoint, null, null);
            }
        }
        catch (Exception ex)
        {
            Debug.Log($"Error sending UDP data: {ex}");
        }
    }

    public void ConnectToPeer(string ip, int port)
    {
        //store peer info
        for (int i = 1; i <= MaxPlayers; i++)
        {
            if (peers[i].tcp.socket == null)
            {
                peers[i].tcp.Connect(ip, port);
                Debug.Log($"Tried to connect to peer {i}");
                return;
            }
        }
    }

    public class TCP
    {
        public TcpClient socket;

        private NetworkStream stream;
        private Packet receivedData;
        private byte[] receiveBuffer;

        public void Connect(string ip, int port)
        {
            socket = new TcpClient
            {
                ReceiveBufferSize = dataBufferSize,
                SendBufferSize = dataBufferSize
            };

            receiveBuffer = new byte[dataBufferSize];
            socket.BeginConnect(ip, port, ConnectCallback, socket);
        }

        private void ConnectCallback(IAsyncResult result)
        {
            socket.EndConnect(result);

            if (!socket.Connected)
            {
                return;
            }

            stream = socket.GetStream();

            receivedData = new Packet();

            stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
        }

        public void SendData(Packet packet)
        {
            try
            {
                if (socket != null)
                {
                    stream.BeginWrite(packet.ToArray(), 0, packet.Length(), null, null);
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"Error sending data to server via TCP: {ex}");
            }
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                int byteLength = stream.EndRead(result);
                if (byteLength <= 0)
                {
                    instance.Disconnect();
                    return;
                }

                byte[] data = new byte[byteLength];
                Array.Copy(receiveBuffer, data, byteLength);

                //handle data
                receivedData.Reset(HandleData(data));
                stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            }
            catch (Exception)
            {
                Disconnect();
            }
        }

        private bool HandleData(byte[] data)
        {
            int packetLength = 0;

            receivedData.SetBytes(data);
            if (receivedData.UnreadLength() >= 4)
            {
                packetLength = receivedData.ReadInt();
                if (packetLength <= 0)
                    return true;
            }

            while (packetLength > 0 && packetLength <= receivedData.UnreadLength())
            {
                byte[] packetBytes = receivedData.ReadBytes(packetLength);
                ThreadManager.ExecuteOnMainThread(() =>
                {
                    using (Packet packet = new Packet(packetBytes))
                    {
                        int packetId = packet.ReadInt();
                        packetHandlers[packetId](packet);
                    }
                });

                packetLength = 0;
                if (receivedData.UnreadLength() >= 4)
                {
                    packetLength = receivedData.ReadInt();
                    if (packetLength <= 0)
                        return true;
                }
            }

            if (packetLength <= 1)
                return true;
            return false;
        }

        private void Disconnect()
        {
            instance.Disconnect();

            stream = null;
            receiveBuffer = null;
            receivedData = null;
            socket = null;
        }
    }

    private void InitializeData()
    {
        //Initialize dictionary of peers
        for (int i = 1; i <= MaxPlayers; i++)
        {
            peers.Add(i, new Peer(i));
        }

        packetHandlers = new Dictionary<int, PacketHandler>()
        {
            { (int) ServerPackets.welcome, ClientHandle.WelcomeServer },
            { (int) ServerPackets.peer, ClientHandle.PeerList },
            { (int) ClientPackets.welcome, ClientHandle.WelcomePeer },
            { (int) ClientPackets.welcomeReceived, ClientHandle.WelcomeReceived },
            { (int) ClientPackets.playerInput, ClientHandle.PlayerInput },
            { (int) ClientPackets.udpTest, ClientHandle.UDPTest },
        };
        //Debug.Log("Initializing packets");
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    public void Disconnect()
    {
        if (isConnected)
        {
            isConnected = false;
            //Close connection to server
            server.socket.Close();
            //Stop listening to incoming messages
            tcpListener.Stop();
            udpListener.Close();
            //Clear peer dictionary
            peers = new Dictionary<int, Peer>();

            Debug.Log("Disconnected.");
        }
    }
}