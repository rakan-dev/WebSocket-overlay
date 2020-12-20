using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebSocket_Overlay
{
    class Program
    {
        static WebSocketServer WS = new WebSocketServer();
        static void Main(string[] args)
        {
            Console.WriteLine(RGBConverter(Color.Red));
            WS.Listen(3030);
            WS.ClientConnected += ClientConnected;

            Console.Read();
        }

        private static string RGBConverter(System.Drawing.Color c)
        {
            return "RGB(" + c.R.ToString() + "," + c.G.ToString() + "," + c.B.ToString() + ")";
        }

        private static void ClientConnected(object sender, WebSocketSession e)
        {
            e.TextMessageReceived += E_TextMessageReceived;
        }

        private static void E_TextMessageReceived(object sender, string e)
        {
            //  100#new#1920#966
           
        }
    }
}
