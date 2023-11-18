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
using System.Runtime.CompilerServices;

namespace SnakeGame;


public class WorldPanel : IDrawable
{
    private IImage wall;
    private IImage background;

    public delegate void ObjectDrawer(object o, ICanvas canvas);

    private World theWorld;
    private int playerID;
    private int viewSize = 900;

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
    }

    public void SetWorld(World world)
    {
        theWorld = world;
    }

    public void SetPlayerID(int ID)
    {
        playerID = ID;
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

        canvas.DrawImage(background, 0, 0, 900, 900);
        lock (theWorld)
        {
            //Basically if the player hasnt connected to the server yet, dont draw and center on screen. 
            //This should only matter within the first few frames of starting the server. 
            if (playerID != -1)
            {
                Snake currentPlayer = theWorld.Players[playerID];
                float playerX = (float)currentPlayer.body.First().GetX();
                float playerY = (float)currentPlayer.body.First().GetY();

                //Centers current player on screen.
                canvas.Translate(-playerX + (viewSize / 2), -playerY + (viewSize / 2));
            }

            //Draw Snakes
            foreach (var p in theWorld.Players.Values)
                DrawObjectWithTransform(canvas, p,
                  p.body[0].GetX(), p.body[0].GetY(), p.dir.ToAngle(),
                  SnakeSegmentDrawer);

            //Draw Walls
            foreach (var p in theWorld.Walls.Values)
                DrawObjectWithTransform(canvas, p,
                  p.p1.GetX(), p.p2.GetY(), 0,
                  WallDrawer);

            //Draw Powerups
            foreach (var p in theWorld.Powerups.Values)
                DrawObjectWithTransform(canvas, p, p.loc.GetX(),
                    p.loc.GetY(), 0,
                    PowerupDrawer);

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
        float w = 50;
        float h = 50;

        // Images are drawn starting from the top-left corner.
        // So if we want the image centered on the player's location, we have to offset it
        // by half its size to the left (-width/2) and up (-height/2)
        canvas.DrawImage(wall, -w / 2, -h / 2, w, h);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="o">The player to draw</param>
    /// <param name="canvas"></param>
    private void PowerupDrawer(object o, ICanvas canvas)
    {
        Powerup p = o as Powerup;
        float width = 10;
        if (p.power % 2 == 0)
            canvas.FillColor = Colors.Orange;
        else
            canvas.FillColor = Colors.Red;

        // Ellipses are drawn starting from the top-left corner.
        // So if we want the circle centered on the powerup's location, we have to offset it
        // by half its size to the left (-width/2) and up (-height/2)
        canvas.FillEllipse(-(width / 2), -(width / 2), width, width);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="o"></param>
    /// <param name="canvas"></param>
    private void SnakeSegmentDrawer(object o, ICanvas canvas)
    {
        Snake snake = o as Snake;
        int snakeSegmentLength = 20;
        canvas.StrokeSize = 10;
        canvas.FillColor = Colors.HotPink;
        canvas.DrawLine(0, 0, 0, -snakeSegmentLength);

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
