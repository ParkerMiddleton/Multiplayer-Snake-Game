using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using IImage = Microsoft.Maui.Graphics.IImage;
#if MACCATALYST
using Microsoft.Maui.Graphics.Platform;
#else
using Microsoft.Maui.Graphics.Win2D;
#endif
using Color = Microsoft.Maui.Graphics.Color;
using System.Reflection;
using Microsoft.Maui;
using System.Net;
using Font = Microsoft.Maui.Graphics.Font;
using SizeF = Microsoft.Maui.Graphics.SizeF;
using System.Numerics;
using Microsoft.Maui.Controls;
using ABI.System.Numerics;
using Microsoft.UI.Xaml.Controls;
using System.Linq;

namespace SnakeGame;


public class WorldPanel : IDrawable
{
    private IImage wall;
    private IImage background;

    // A delegate for DrawObjectWithTransform
    // Methods matching this delegate can draw whatever they want onto the canvas
    public delegate void ObjectDrawer(object o, ICanvas canvas);

    private World theWorld;

    // for use of drawing dummy data for the view
    // ideally this would come from the server in the form of json
    Wall wall23 = new Wall(23, new Vector2D(100, -100), new Vector2D(150, 300));
    Wall wall22 = new Wall(22, new Vector2D(100, -200), new Vector2D(150, 450));
    Snake snake = new Snake(1, "snake", new List<Vector2D> { new Vector2D(100, -100), new Vector2D(150, 300) }, new Vector2D(), 0, false, true, false, true);


    private bool initializedForDrawing = false;

    private IImage loadImage(string name)
    {
        Assembly assembly = GetType().GetTypeInfo().Assembly;
        string path = "SnakeClient.Resources.Images";
        using (Stream stream = assembly.GetManifestResourceStream($"{path}.{name}"))
        {
#if MACCATALYST
            return PlatformImage.FromStream(stream);
#else
            return new W2DImageLoadingService().FromStream(stream);
#endif
        }
    }

    public WorldPanel()
    {
        theWorld = new World(1200);
        theWorld.Walls.Add(wall23.wall, wall23);
        theWorld.Walls.Add(wall22.wall, wall22);
        theWorld.Players.Add(snake.snake, snake);
    }

    private void InitializeDrawing()
    {
        wall = loadImage("wallsprite.png");
        background = loadImage("background.png");
        initializedForDrawing = true;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (!initializedForDrawing)
            InitializeDrawing();

        // undo previous transformations from last frame
        canvas.ResetState();


        //float playerX = //... (the player's world-space Y coordinate)
        //float playerY = //... (the player's world-space Y coordinate)
        // example code for how to draw
        //canvas.Translate(-playerX + (viewSize / 2), -playerY + (viewSize / 2));
        canvas.DrawImage(background, 0, 0, 2000, 2000);
        lock (theWorld)
        {
            //GetsSnakes 
            foreach (var p in theWorld.Players.Values)
            {
                canvas.StrokeColor = Colors.HotPink;
                DrawObjectWithTransform(canvas, p,
                  p.body[0].GetX(), p.body[0].GetY(), p.dir.ToAngle(),
                  SnakeSegmentDrawer);
            }


            foreach (var p in theWorld.Walls.Values)
                DrawObjectWithTransform(canvas, p,
                  p.p1.GetX(), p.p2.GetY(), 0,
                  WallDrawer);

        }
    }

    /// <summary>
    /// A method that can be used as an ObjectDrawer delegate
    /// </summary>
    /// <param name="o">The player to draw</param>
    /// <param name="canvas"></param>
    private void WallDrawer(object o, ICanvas canvas)
    {
        Wall p = o as Wall;
        // scale the ships down a bit
        float w = wall.Width * 0.4f;
        float h = wall.Height * 0.4f;

        // Images are drawn starting from the top-left corner.
        // So if we want the image centered on the player's location, we have to offset it
        // by half its size to the left (-width/2) and up (-height/2)
        canvas.DrawImage(wall, -w / 2, -h / 2, w, h);
    }

    private void SnakeSegmentDrawer(object o, ICanvas canvas)
    {
        Snake snake = o as Snake;
        int snakeSegmentLength = 20;
        canvas.DrawLine( 0,0,0, -snakeSegmentLength);

    }

    /// <summary>
    /// This method performs a translation and rotation to draw an object.
    /// </summary>
    /// <param name="canvas">The canvas object for drawing onto</param>
    /// <param name="o">The object to draw</param>
    /// <param name="worldX">The X component of the object's position in world space</param>
    /// <param name="worldY">The Y component of the object's position in world space</param>
    /// <param name="angle">The orientation of the object, measured in degrees clockwise from "up"</param>
    /// <param name="drawer">The drawer delegate. After the transformation is applied, the delegate is invoked to draw whatever it wants</param>
    private void DrawObjectWithTransform(ICanvas canvas, object o, double worldX, double worldY, double angle, ObjectDrawer drawer)
    {
        // "push" the current transform
        canvas.SaveState();

        canvas.Translate((float)worldX, (float)worldY);
        canvas.Rotate((float)angle);
        drawer(o, canvas);

        // "pop" the transform
        canvas.RestoreState();
    }
}
