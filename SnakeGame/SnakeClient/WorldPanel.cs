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
//using ABI.System.Numerics;
//using Microsoft.UI.Xaml.Controls;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;
using Microsoft.UI.Xaml.Controls;

namespace SnakeGame;

/// <summary>
/// For Abbey:
/// Since the last time we talked:
/// 
/// I was able to parse the JSON from the server then update the world object with it. 
/// The world object and player ID, which are the first two things that are sent in the protocol by the server, are continuously passed to the 
/// view via getters and setters via OnFrame event. Basically they are always passed every frame. 
/// 
/// I was able to get all objects to display on screen. A snake, all powerups and all walls are present but not without any bugs. 
/// 
/// Known issues: 
///-> The snake hits random unseen objects, then dies. 
///   This probably happens because the edges of the worldpanel are not being displayed
///   or because there are invisable wall objects that the snake is hitting... Not sure on this one though. 
///   
///-> The background never changes and always moves with the snake. 
///   The snake should be able to traverse the background instead of having it move with it.
///   
/// Still TODO: 
/// -> Controller implementation - w,a,s,d stuff in MainPage.xaml.cs
///
/// -> Display name and score above snake's head
///    This should always display in the same direction the snake is fasing. So if direction is Vector2D(X=1,Y=0) then the name should 
///    be paralell to the right side of the screen, the same direction the snake is traveling. 
///
/// -> Properly drawing a snake in general 
///    The code in its current state just draws the head and tail for some reason, not sure if this is a glitch or not.
///    This might be better to implement after controls are implemented and above bugs are fixed. 
/// 
/// I think this is it for now. I'm sure that other issues will arise. But we are certainly over halfway there. 
/// 
/// </summary>
public class WorldPanel : IDrawable
{
    private IImage wall;
    private IImage background;

    /// <summary>
    /// Note: This is super important. 
    /// basically every object that is drawn on this screen needs to be a version of this
    /// So far we have "snakeSegmentDrawer", "WallDrawer" and "PowerupDrawer". 
    /// </summary>
    /// <param name="o"></param>
    /// <param name="canvas"></param>
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

    /// <summary>
    /// "Main method" for world panel. 
    /// Any start up code should probably go here
    /// </summary>
    public WorldPanel()
    {
    }


    /// <summary>
    /// A setter for current world state 
    /// Note: This only exisits so the edited world object can be passed to the view
    /// Interacts with:  OnFrame and MainPage Startup method. 
    /// </summary>
    /// <param name="world"></param>
    public void SetWorld(World world)
    {
        theWorld = world;
    }

    /// <summary>
    /// A setter for current player's ID.
    /// Note: This only exisits so the player can always be focused on screen
    /// Interacts with:  OnFrame and MainPage Startup method. 
    /// </summary>
    /// <param name="ID"></param>
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
        // start up condition
        if (!initializedForDrawing)
            InitializeDrawing();

        //undo previous transition from last frame
        canvas.ResetState();

        // draw background
        canvas.DrawImage(background, (-theWorld.size / 2), (-theWorld.size / 2), theWorld.size, theWorld.size);


        lock (theWorld)
        {

            if (playerID != -1)
            {
                Snake currentPlayer = theWorld.Players[playerID];
                float playerX = (float)currentPlayer.body.First().GetX();
                float playerY = (float)currentPlayer.body.First().GetY();

                //centereing view on snake
                float translationX = -playerX + (viewSize / 2);
                float translationY = -playerY + (viewSize / 2);
                canvas.Translate(translationX, translationY);
            }

            // Draw Snakes
            foreach (var p in theWorld.Players.Values)
                DrawObjectWithTransform(canvas, p,
                    p.body[0].GetX(), p.body[0].GetY(), p.dir.ToAngle(),
                    SnakeSegmentDrawer);

            //Draw Walls
            foreach (var p in theWorld.Walls.Values)
            {
                // if wall layer is drawn in the x direction
                if (p.p1.GetX() == p.p2.GetX())
                {
                    double distance = Math.Abs((p.p2.GetY() - p.p1.GetY()));
                    int numberOfWalls = (int)(distance / 50);

                    double pointY = p.p1.GetY();
                    if (p.p1.GetY() < p.p2.GetY())
                    {
                        for (int i = 0; i < numberOfWalls; i++)
                        {
                            DrawObjectWithTransform(canvas, p, p.p1.GetX(), pointY, 0, WallDrawer);
                            pointY += 50;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < numberOfWalls; i++)
                        {
                            DrawObjectWithTransform(canvas, p, p.p1.GetX(), pointY, 0, WallDrawer);
                            pointY -= 50;
                        }
                    }

                }

                // if wall layer drawn in the Y direction 
                if (p.p1.GetY() == p.p2.GetY())
                {
                    // - 50 here because we drew the first walls initially, this could be unneccessary. 
                    double distance = Math.Abs((p.p2.GetX() - p.p1.GetX()));
                    int numberOfWalls = (int)(distance / 50);

                    double pointX = p.p1.GetX();
                    if (p.p1.GetX() < p.p2.GetX())
                    {
                        for (int i = 0; i < numberOfWalls; i++)
                        {
                            DrawObjectWithTransform(canvas, p, pointX, p.p1.GetY(), 0, WallDrawer);
                            // increment upwards by 50. 
                            pointX += 50;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < numberOfWalls; i++)
                        {
                            DrawObjectWithTransform(canvas, p, pointX, p.p1.GetY(), 0, WallDrawer);
                            // decrement downwards by 50
                            pointX -= 50;
                        }
                    }

                }
            }

            // Draw Powerups
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
        float width = wall.Width;
        float height = wall.Height;
        // Images are drawn starting from the top-left corner.
        // So if we want the image centered on the player's location, we have to offset it
        // by half its size to the left (-width/2) and up (-height/2)
        canvas.DrawImage(wall, (width / 2), (height / 2), width, height);

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

        Color[] colors = { Colors.Red, Colors.Blue, Colors.Green, Colors.Indigo, Colors.HotPink, Colors.Lavender, Colors.LightGreen, Colors.Orange };

        Snake snake = o as Snake;
        int snakeSegmentLength = 20;

        int snakeSize = snakeSegmentLength + (snake.score * 20);


        canvas.StrokeSize = 10;
        canvas.StrokeColor = colors[playerID % 8];
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(0, 0, 0, -snakeSize);




        //displaying name and score
        string nameScore = $"{snake.name} - {snake.score}";
        canvas.FontColor = Colors.White;
        canvas.FontSize = 18;
        canvas.Font = Font.Default;
        canvas.DrawString(nameScore, -20, -10 - snakeSize, 380, 100, HorizontalAlignment.Justified, VerticalAlignment.Top);

        //explosion
        if (snake.died == true)
        {
            canvas.StrokeSize = 10;
            canvas.FillColor = Colors.Red;
            canvas.DrawEllipse(0, 0, 80, 80);

        }

        //exploring timeout -- can be deleted eventually
        if (snake.alive == false)
        {
            canvas.FontColor = Colors.Black;
            canvas.FontSize = 30;
            canvas.Font = Font.Default;
            canvas.DrawString("false", 0, 0, 380, 100, HorizontalAlignment.Left, VerticalAlignment.Top);

        }
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
