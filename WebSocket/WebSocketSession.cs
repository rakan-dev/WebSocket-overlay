
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

public class WebSocketSession : IDisposable
{
    private static readonly Random Random = new Random();

    private TcpClient Client { get; }
    private Stream ClientStream { get; }

    public string Id { set; get; }
    public bool IsMasking { get; private set; }

    public event EventHandler<WebSocketSession> HandshakeCompleted;
    public event EventHandler<WebSocketSession> Disconnected;
    public event EventHandler<Exception> Error;
    public event EventHandler<byte[]> AnyMessageReceived;
    public event EventHandler<string> TextMessageReceived;
    public event EventHandler<string> BinaryMessageReceived;

    public WebSocketSession(TcpClient client)
    {
        Client = client;
        ClientStream = client.GetStream();
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Internal, do not use :)
    /// </summary>
    internal void Start()
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            if (!DoHandshake())
            {
                Error?.Invoke(this, new Exception("Handshake Failed."));
                Disconnected?.Invoke(this, this);
                return;
            }

            HandshakeCompleted?.Invoke(this, this);
            StartMessageLoop();
        });
    }

    private void StartMessageLoop()
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                MessageLoop();
            }
            catch (Exception e)
            {
                Error?.Invoke(this, e);
            }
            finally
            {
                Disconnected?.Invoke(this, this);
            }
        });
    }

    private bool DoHandshake()
    {
        while (Client.Available == 0 && Client.Connected) { }
        if (!Client.Connected) return false;

        byte[] handshake;
        using (var handshakeBuffer = new MemoryStream())
        {
            while (Client.Available > 0)
            {
                var buffer = new byte[Client.Available];
                ClientStream.Read(buffer, 0, buffer.Length);
                handshakeBuffer.Write(buffer, 0, buffer.Length);
            }

            handshake = handshakeBuffer.ToArray();
        }

        if (!Encoding.UTF8.GetString(handshake).StartsWith("GET")) return false;

        var response = Encoding.UTF8.GetBytes("HTTP/1.1 101 Switching Protocols" + Environment.NewLine
                                              + "Connection: Upgrade" + Environment.NewLine
                                              + "Upgrade: websocket" + Environment.NewLine
                                              + "Sec-WebSocket-Accept: " + Convert.ToBase64String(
                                                  SHA1.Create().ComputeHash(
                                                      Encoding.UTF8.GetBytes(
                                                          new Regex("Sec-WebSocket-Key: (.*)").Match(Encoding.UTF8.GetString(handshake)).Groups[1].Value.Trim() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
                                                      )
                                                  )
                                              ) + Environment.NewLine
                                              + Environment.NewLine);

        ClientStream.Write(response, 0, response.Length);
        return true;
    }
    private void MessageLoop()
    {
        var session = this;
        var client = session.Client;
        var stream = session.ClientStream;

        var packet = new List<byte>();

        var messageOpcode = 0x0;
        using (var messageBuffer = new MemoryStream())
            while (client.Connected)
            {
                packet.Clear();

                var ab = client.Available;
                if (ab == 0) continue;

                packet.Add((byte)stream.ReadByte());
                var fin = (packet[0] & (1 << 7)) != 0;
                var rsv1 = (packet[0] & (1 << 6)) != 0;
                var rsv2 = (packet[0] & (1 << 5)) != 0;
                var rsv3 = (packet[0] & (1 << 4)) != 0;

                // Must error if is set.
                //if (rsv1 || rsv2 || rsv3)
                //    return;

                var opcode = packet[0] & ((1 << 4) - 1);

                switch (opcode)
                {
                    case 0x0: // Continuation Frame
                        break;
                    case 0x1: // Text
                    case 0x2: // Binary
                    case 0x8: // Connection Close
                        messageOpcode = opcode;
                        break;
                    case 0x9:
                        continue; // Ping
                    case 0xA:
                        continue; // Pong
                    default:
                        continue; // Reserved
                }

                packet.Add((byte)stream.ReadByte());
                var masked = IsMasking = (packet[1] & (1 << 7)) != 0;
                var pseudoLength = packet[1] - (masked ? 128 : 0);

                ulong actualLength = 0;
                if (pseudoLength > 0 && pseudoLength < 125) actualLength = (ulong)pseudoLength;
                else if (pseudoLength == 126)
                {
                    var length = new byte[2];
                    stream.Read(length, 0, length.Length);
                    packet.AddRange(length);
                    Array.Reverse(length);
                    actualLength = BitConverter.ToUInt16(length, 0);
                }
                else if (pseudoLength == 127)
                {
                    var length = new byte[8];
                    stream.Read(length, 0, length.Length);
                    packet.AddRange(length);
                    Array.Reverse(length);
                    actualLength = BitConverter.ToUInt64(length, 0);
                }

                var mask = new byte[4];
                if (masked)
                {
                    stream.Read(mask, 0, mask.Length);
                    packet.AddRange(mask);
                }

                if (actualLength > 0)
                {
                    var data = new byte[actualLength];
                    stream.Read(data, 0, data.Length);
                    packet.AddRange(data);

                    if (masked)
                        data = ApplyMask(data, mask);

                    messageBuffer.Write(data, 0, data.Length);
                }


                if (!fin) continue;
                var message = messageBuffer.ToArray();

                switch (messageOpcode)
                {
                    case 0x1:
                        AnyMessageReceived?.Invoke(session, message);
                        TextMessageReceived?.Invoke(session, Encoding.UTF8.GetString(message));
                        break;
                    case 0x2:
                        AnyMessageReceived?.Invoke(session, message);
                        BinaryMessageReceived?.Invoke(session, Encoding.UTF8.GetString(message));
                        break;
                    case 0x8:
                        Close();
                        break;
                    default:
                        throw new Exception("Invalid opcode: " + messageOpcode);
                }

                messageBuffer.SetLength(0);
            }
    }

    public void Close()
    {
        if (!Client.Connected) return;

        var mask = new byte[4];
        if (IsMasking) Random.NextBytes(mask);
        SendMessage(new byte[] { }, 0x8, IsMasking, mask);

        Client.Close();
    }

    public void SendMessage(string payload) => SendMessage(Client, payload, false);
    public void SendMessage(byte[] payload, bool isBinary = false) => SendMessage(Client, payload, isBinary, IsMasking);

    public void SendMessage(byte[] payload, bool isBinary = false, bool masking = false) => SendMessage(Client, payload, isBinary, masking);
    public void SendMessage(byte[] payload, int opcode, bool masking, byte[] mask) => SendMessage(Client, payload, opcode, masking, mask);

    static void SendMessage(TcpClient client, string payload, bool masking = false) =>
                SendMessage(client, Encoding.UTF8.GetBytes(payload), false, masking);
    static void SendMessage(TcpClient client, byte[] payload, bool isBinary = false, bool masking = false)
    {
        var mask = new byte[4];
        if (masking) Random.NextBytes(mask);
        SendMessage(client, payload, isBinary ? 0x2 : 0x1, masking, mask);
    }
    static void SendMessage(TcpClient client, byte[] payload, int opcode, bool masking, byte[] mask)
    {
        if (masking && mask == null) throw new ArgumentException(nameof(mask));

        using (var packet = new MemoryStream())
        {
            byte firstbyte = 0b0_0_0_0_0000; // fin | rsv1 | rsv2 | rsv3 | [ OPCODE | OPCODE | OPCODE | OPCODE ]

            firstbyte |= 0b1_0_0_0_0000; // fin
            //firstbyte |= 0b0_1_0_0_0000; // rsv1
            //firstbyte |= 0b0_0_1_0_0000; // rsv2
            //firstbyte |= 0b0_0_0_1_0000; // rsv3

            firstbyte += (byte)opcode; // Text
            packet.WriteByte(firstbyte);

            // Set bit: bytes[byteIndex] |= mask;

            byte secondbyte = 0b0_0000000; // mask | [SIZE | SIZE  | SIZE  | SIZE  | SIZE  | SIZE | SIZE]

            if (masking)
                secondbyte |= 0b1_0000000; // mask

            if (payload.LongLength <= 0b0_1111101) // 125
            {
                secondbyte |= (byte)payload.Length;
                packet.WriteByte(secondbyte);
            }
            else if (payload.LongLength <= UInt16.MaxValue) // If length takes 2 bytes
            {
                secondbyte |= 0b0_1111110; // 126
                packet.WriteByte(secondbyte);

                var len = BitConverter.GetBytes(payload.LongLength);
                Array.Reverse(len, 0, 2);
                packet.Write(len, 0, 2);
            }
            else // if (payload.LongLength <= Int64.MaxValue) // If length takes 8 bytes
            {
                secondbyte |= 0b0_1111111; // 127
                packet.WriteByte(secondbyte);

                var len = BitConverter.GetBytes(payload.LongLength);
                Array.Reverse(len, 0, 8);
                packet.Write(len, 0, 8);
            }

            if (masking)
            {
                packet.Write(mask, 0, 4);
                payload = ApplyMask(payload, mask);
            }

            // Write all data to the packet
            packet.Write(payload, 0, payload.Length);

            // Get client's stream
            var stream = client.GetStream();

            var finalPacket = packet.ToArray();

            // Send the packet
            foreach (var b in finalPacket)
                stream.WriteByte(b);
        }
    }

    static byte[] ApplyMask(IReadOnlyList<byte> msg, IReadOnlyList<byte> mask)
    {
        var decoded = new byte[msg.Count];
        for (var i = 0; i < msg.Count; i++)
            decoded[i] = (byte)(msg[i] ^ mask[i % 4]);
        return decoded;
    }

    public void Dispose()
    {
        Close();

        ((IDisposable)Client)?.Dispose();
        ClientStream?.Dispose();
    }
}