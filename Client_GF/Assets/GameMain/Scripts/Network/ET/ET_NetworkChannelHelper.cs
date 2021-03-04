using GameFramework;
using GameFramework.Event;
using GameFramework.Network; 
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityGameFramework.Runtime;

public class ET_NetworkChannelHelper : INetworkChannelHelper {
    private readonly Dictionary<int, Type> m_ServerToClientPacketTypes = new Dictionary<int, Type>();

    private readonly List<byte[]> byteses = new List<byte[]>() { new byte[ETPackets.ET_PacketSizeLength], new byte[1], new byte[2] };

    private INetworkChannel m_NetworkChannel = null;

    private MemoryStream memoryStream = new MemoryStream(1024 * 8);
    /// <summary>
    /// 前2个字节代表消息的长度
    /// 第3个字节代表消息flag，1表示rpc消息，0表示普通消息
    /// 第4和第5个字节表示消息的Id
    /// 这样做是为了和ET服务器的消息包的解析保持一致
    /// </summary>
    public int PacketHeaderLength {
        get {
            return ETPackets.ET_PacketSizeLength + 3;
        }
    }
    /// <summary>
    /// 准备进行连接。
    /// </summary>
    public void PrepareForConnecting() {
        m_NetworkChannel.Socket.ReceiveBufferSize = 1024 * 64;
        m_NetworkChannel.Socket.SendBufferSize = 1024 * 64;
    }


    public void Initialize(INetworkChannel networkChannel) {
        m_NetworkChannel = networkChannel;

        //memoryStream = MemoryStreamManager.GetStream("message", ushort.MaxValue);            

        // 反射注册包和包处理函数。
        Type packetBaseType = typeof(SCPacketBase);
        Type eT_PacketHandlerType = typeof(ET_PacketHandler);
        Assembly assembly = Assembly.GetExecutingAssembly();
        Type[] types = assembly.GetTypes();
        for (int i = 0; i < types.Length; i++) {
            if (!types[i].IsClass || types[i].IsAbstract) {
                continue;
            }

            if (types[i].BaseType == packetBaseType) {
                PacketBase packetBase = (PacketBase)Activator.CreateInstance(types[i]);
                Type packetType = GetServerToClientPacketType(packetBase.Id);
                if (packetType != null) {
                    Log.Warning("Already exist packet type '{0}', check '{1}' or '{2}'?.", packetBase.Id.ToString(), packetType.Name, packetBase.GetType().Name);
                    continue;
                }

                m_ServerToClientPacketTypes.Add(packetBase.Id, types[i]);
            }
            else if (types[i].BaseType == eT_PacketHandlerType) {
                IPacketHandler packetHandler = (IPacketHandler)Activator.CreateInstance(types[i]);
                m_NetworkChannel.RegisterHandler(packetHandler);
            }
        }

        GameEntry.Event.Subscribe(UnityGameFramework.Runtime.NetworkConnectedEventArgs.EventId, OnNetworkConnected);
        GameEntry.Event.Subscribe(UnityGameFramework.Runtime.NetworkClosedEventArgs.EventId, OnNetworkClosed);
        GameEntry.Event.Subscribe(UnityGameFramework.Runtime.NetworkMissHeartBeatEventArgs.EventId, OnNetworkMissHeartBeat);
        GameEntry.Event.Subscribe(UnityGameFramework.Runtime.NetworkErrorEventArgs.EventId, OnNetworkError);
        GameEntry.Event.Subscribe(UnityGameFramework.Runtime.NetworkCustomErrorEventArgs.EventId, OnNetworkCustomError);
    }
    
    public Packet DeserializePacket(IPacketHeader packetHeader, Stream source, out object customErrorData) {
        customErrorData = null;
        ET_SCPacketHeader header = packetHeader as ET_SCPacketHeader;
        if (header == null) {
            Log.Warning("Packet header is invalid.");
            return null;
        }

        Packet packet = null;
        if (header.IsValid) {
            Type packetType = GetServerToClientPacketType(header.Id);

            //Log.Debug("~~~~~~~~~~~"+packetType.Name);
            if (packetType != null && source is MemoryStream) {
                //packet = (Packet)RuntimeTypeModel.Default.DeserializeWithLengthPrefix(source, ReferencePool.Acquire(packetType), packetType, PrefixStyle.Fixed32, 0);
                object instance = Activator.CreateInstance(packetType);
                //packet = (Packet)ProtobufHelper.FromStream(packetType, (MemoryStream)source);
                packet = (Packet)ProtobufHelper.FromStream(instance, (MemoryStream)source);
            }
            else {
                Log.Warning("Can not deserialize packet for packet id '{0}'.", header.Id.ToString());

            }
        }
        else {
            Log.Warning("Packet header is invalid.");
        }
        ReferencePool.Release(header);
        //DispatchMsg(packet);
        return packet;
    } 

    public IPacketHeader DeserializePacketHeader(Stream source, out object customErrorData) {
        customErrorData = null;

        ET_SCPacketHeader scHeader = ReferencePool.Acquire<ET_SCPacketHeader>();
        MemoryStream memoryStream = source as MemoryStream;
        if (memoryStream != null) {
            int packetSize = BitConverter.ToUInt16(memoryStream.GetBuffer(), 0);
            byte flag = memoryStream.GetBuffer()[ETPackets.ET_MessageFlagIndex];
            ushort opcode = BitConverter.ToUInt16(memoryStream.GetBuffer(), ETPackets.ET_MessageOpcodeIndex);

            //这里需要用服务端发过来的packetSize的值减去消息包中flag和opcode的长度，
            //因为服务端在发送消息时设置的packetSize的值是包含flag和opcode的，而
            //客户端在解析包头的时候已经解析了flag和opcode，因此剩余要解析的数据长度要减去3（flag和opcode的总长度是3个字节）
            scHeader.PacketLength = packetSize - ETPackets.ET_MessageIdentifyLength;
            scHeader.Flag = flag;
            scHeader.Id = opcode;
            return scHeader;
        }

        return null;
    }


    public bool SendHeartBeat() {
        //m_NetworkChannel.Send(ReferencePool.Acquire<CSHeartBeat>());
        return true;
    }

    public bool Serialize<T>(T packet, Stream destination) where T : Packet {
        PacketBase packetImpl = packet as PacketBase;
        if (packetImpl == null) {
            Log.Warning("Packet is invalid.");
            return false;
        }

        if (packetImpl.PacketType != PacketType.ClientToServer) {
            Log.Warning("Send packet invalid.");
            return false;
        }

        //memoryStream.SetLength(memoryStream.Capacity);
        //memoryStream.Position = PacketHeaderLength;
        memoryStream.Seek(PacketHeaderLength, SeekOrigin.Begin);
        memoryStream.SetLength(PacketHeaderLength);
        //Serializer.SerializeWithLengthPrefix(memoryStream, packet, PrefixStyle.Fixed32);
        ProtobufHelper.ToStream(packet, memoryStream);

        // 头部消息
        ET_CSPacketHeader packetHeader = ReferencePool.Acquire<ET_CSPacketHeader>();
        packetHeader.Flag = 0;      //客户端发送的消息，默认flag为0,服务器会解析flag字段值
        packetHeader.PacketLength = (int)memoryStream.Length - ETPackets.ET_PacketSizeLength; // 消息内容长度需要减去头部消息长度,只包含packetSize一个字段
        packetHeader.Id = (ushort)packet.Id;

        memoryStream.Position = 0;
        this.byteses[0].WriteTo(0, (ushort)packetHeader.PacketLength);
        this.byteses[1][0] = packetHeader.Flag;
        this.byteses[2].WriteTo(0, packetHeader.Id);
        int index = 0;
        foreach (var bytes in this.byteses) {
            Array.Copy(bytes, 0, memoryStream.GetBuffer(), index, bytes.Length);
            index += bytes.Length;
        }

        //Serializer.SerializeWithLengthPrefix(memoryStream, packetHeader, PrefixStyle.Fixed32);

        ReferencePool.Release(packetHeader);

        memoryStream.WriteTo(destination);

        long len = destination.Length;
        long pos = destination.Position;
        byte[] temp = (destination as MemoryStream).GetBuffer();
        return true;
        //}
    }

    public void Shutdown() {
        GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.NetworkConnectedEventArgs.EventId, OnNetworkConnected);
        GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.NetworkClosedEventArgs.EventId, OnNetworkClosed);
        GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.NetworkMissHeartBeatEventArgs.EventId, OnNetworkMissHeartBeat);
        GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.NetworkErrorEventArgs.EventId, OnNetworkError);
        GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.NetworkCustomErrorEventArgs.EventId, OnNetworkCustomError);

        m_NetworkChannel = null;
        memoryStream.Dispose();
    }



    private Type GetServerToClientPacketType(int id) {
        Type type = null;
        if (m_ServerToClientPacketTypes.TryGetValue(id, out type)) {
            return type;
        }

        return null;
    }


    private void OnNetworkConnected(object sender, GameEventArgs e) {
        UnityGameFramework.Runtime.NetworkConnectedEventArgs ne = (UnityGameFramework.Runtime.NetworkConnectedEventArgs)e;
        if (ne.NetworkChannel != m_NetworkChannel) {
            return;
        }

        //Log.Info("Network channel '{0}' connected, local address '{1}:{2}', remote address '{3}:{4}'.", ne.NetworkChannel.Name, ne.NetworkChannel.LocalIPAddress, ne.NetworkChannel.LocalPort.ToString(), ne.NetworkChannel.RemoteIPAddress, ne.NetworkChannel.RemotePort.ToString());
    }

    private void OnNetworkClosed(object sender, GameEventArgs e) {
        UnityGameFramework.Runtime.NetworkClosedEventArgs ne = (UnityGameFramework.Runtime.NetworkClosedEventArgs)e;
        if (ne.NetworkChannel != m_NetworkChannel) {
            return;
        }

        Log.Info("Network channel '{0}' closed.", ne.NetworkChannel.Name);
    }


    private void OnNetworkMissHeartBeat(object sender, GameEventArgs e) {
        UnityGameFramework.Runtime.NetworkMissHeartBeatEventArgs ne = (UnityGameFramework.Runtime.NetworkMissHeartBeatEventArgs)e;
        if (ne.NetworkChannel != m_NetworkChannel) {
            return;
        }

        Log.Info("Network channel '{0}' miss heart beat '{1}' times.", ne.NetworkChannel.Name, ne.MissCount.ToString());

        if (ne.MissCount < 2) {
            return;
        }

        ne.NetworkChannel.Close();
    }

    private void OnNetworkError(object sender, GameEventArgs e) {
        UnityGameFramework.Runtime.NetworkErrorEventArgs ne = (UnityGameFramework.Runtime.NetworkErrorEventArgs)e;
        if (ne.NetworkChannel != m_NetworkChannel) {
            return;
        }

        Log.Info("Network channel '{0}' error, error code is '{1}', error message is '{2}'.", ne.NetworkChannel.Name, ne.ErrorCode.ToString(), ne.ErrorMessage);

        ne.NetworkChannel.Close();
    }

    private void OnNetworkCustomError(object sender, GameEventArgs e) {
        UnityGameFramework.Runtime.NetworkCustomErrorEventArgs ne = (UnityGameFramework.Runtime.NetworkCustomErrorEventArgs)e;
        if (ne.NetworkChannel != m_NetworkChannel) {
            return;
        }
    }

}

