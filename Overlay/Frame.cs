using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

public class Frame
{
   private List<Shape> shapes = new List<Shape>();
    public Frame()
    {
        shapes = new List<Shape>();
    }
    public void DrawLine(Vector2 point1,Vector2 point2,Color color)
    {
        Shape line = new Shape();
        line.Type = "line";
        line.x = (int)point1.X;
        line.y = (int)point1.Y;
        line.x1 = (int)point2.X;
        line.y1 = (int)point2.X;
        line.color = RGBConverter(color);
        shapes.Add(line);
    }
    public void DrawCircle(Vector2 point,int d,Color color ,bool fill)
    {
        Shape Circle = new Shape();
        Circle.Type = "circle";
        Circle.x = (int)point.X;
        Circle.y = (int)point.Y;
        Circle.d = d;
        Circle.color = RGBConverter(color);
        Circle.fill = fill;
        shapes.Add(Circle);
    }
    
    public void DrawBox(Vector2 point, int w, int h, Color color,bool fill)
    {
        Shape Rect = new Shape();
        Rect.Type = "rect";
        Rect.x = (int)point.X;
        Rect.y = (int)point.Y;
        Rect.width = w;
        Rect.height = h;
        Rect.color = RGBConverter(color);
        Rect.fill = fill;
        shapes.Add(Rect);
    }
    public void DrawText(string Text,Vector2 point,int Size,Color color)
    {
        Shape txt = new Shape();
        txt.Type = "text";
        txt.x = (int)point.X;
        txt.txt = Text;
        txt.y = (int)point.Y;
        txt.color = RGBConverter(color);
        txt.txtSize = Size;
        shapes.Add(txt);
    }

    public string ToJson()
    {

        return JsonConvert.SerializeObject(shapes);
    }
    private static string RGBConverter(System.Drawing.Color c)
    {
        return "RGB(" + c.R.ToString() + "," + c.G.ToString() + "," + c.B.ToString() + ")";
    }
    public class Shape
    {
        public string Type,txt;
        public int x, y, width, height, txtSize,x1,y1,d;
        public string color;
        public bool fill;
    }
}

