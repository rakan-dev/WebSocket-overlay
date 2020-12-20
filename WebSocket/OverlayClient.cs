using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

public class OverlayClient : WebSocketSession
{
    public int ProcessPID { set; get; }
    public int WindowWidth { set; get; }
    public int WindowHeight { set; get; }
    public OverlayClient(TcpClient tcpClient):base(tcpClient)
    {
        TextMessageReceived += _TextMessageReceived;
    }

    private void _TextMessageReceived(object sender, string e)
    {
        string[] vs = e.Split('#');
        WebSocketSession session = (WebSocketSession)sender;
        Console.WriteLine(e);
        if (vs[1] == "new")
        {
            Id = vs[0];
            WindowWidth = int.Parse(vs[2]);
            WindowHeight = int.Parse(vs[3]);
            Frame frame = new Frame();
            frame.DrawCircle(new System.Numerics.Vector2(WindowWidth / 2, WindowHeight / 2), 200, Color.Black, false);
            session.SendMessage(frame.ToJson());
        }
        else if (vs[1] == "frame")
        {
            Frame frame = new Frame();
            frame.DrawCircle(new System.Numerics.Vector2(WindowWidth / 2, WindowHeight / 2), 200, Color.Black, false);
            session.SendMessage(frame.ToJson());
        }
        else if(vs[1] == "update")
        {
            WindowWidth = int.Parse(vs[2]);
            WindowHeight = int.Parse(vs[3]);
        }
    }
}

