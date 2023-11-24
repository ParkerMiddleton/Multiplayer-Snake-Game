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
using PointF = Microsoft.Maui.Graphics.PointF;
using Microsoft.Maui;
using System.Net;
using Font = Microsoft.Maui.Graphics.Font;
using SizeF = Microsoft.Maui.Graphics.SizeF;
using System.Numerics;
using Microsoft.Maui.Controls;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;
using System.Text;
//using Microsoft.UI.Xaml.Controls;

namespace SnakeGame;

public class WorldPanel : IDrawable
{
    private IImage tile1; 
    private IImage tile2; 
    private IImage tile3; 
    private IImage tile4;
    private IImage powerup;
    private IImage background;

   private IImage[] tiles; 

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
    private Color[] colors = {Colors.Red, Colors.Yellow, Colors.AliceBlue, Colors.OrangeRed, Colors.WhiteSmoke,
    Colors.Turquoise, Colors.Tan, Colors.Magenta , Colors.Aqua, Colors.Maroon, Colors.Moccasin};
    private Color[][] colorPattern = { new Color[] {Colors.Red,Colors.PaleVioletRed},
                                  new Color[]{Colors.Blue, Colors.Aqua},
                                    new Color[]{Colors.DarkOrange, Colors.Orange},
                                    new Color []{Colors.Green, Colors.LightGreen},
                                    new Color []{Colors.Yellow, Colors.LightGoldenrodYellow},
                                    new Color[] {Colors.Purple, Colors.MediumPurple},
                                    new Color[]{Colors.Red, Colors.Orange, Colors.Yellow, Colors.Green, Colors.Blue, Colors.Purple},
                                    new Color[]{Colors.Snow, Colors.LightGrey}
             };

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
        tile1 = loadImage("neontile2.jpg");
        tile2 = loadImage("neontile3.jpg");
        tile3 = loadImage("neontile4.jpg");
        tile4 = loadImage("neontile5.jpg");
        powerup = loadImage("neonpowerup.png");



        background = loadImage("swirlybackground.jpg");
        initializedForDrawing = true;
        tiles = new IImage[] {tile1, tile2, tile3, tile4};
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // start up condition
        if (!initializedForDrawing)
            InitializeDrawing();

        //undo previous transition from last frame
        canvas.ResetState();

        lock (theWorld)
        {

            //Dont draw until we have start data from the server
            if (playerID != -1 && theWorld.size > 0)
            {
                Snake currentPlayer = theWorld.Players[playerID];
                float playerX = (float)currentPlayer.body.Last().GetX();
                float playerY = (float)currentPlayer.body.Last().GetY();

                //Centereing view on snake
                float translationX = (-playerX) + (viewSize / 2);
                float translationY = (-playerY) + (viewSize / 2);
                canvas.Translate(translationX, translationY);

                //Draw background
                canvas.DrawImage(background, (-theWorld.size / 2), (-theWorld.size / 2), theWorld.size, theWorld.size);


                //Draw Snakes
                foreach (Snake snake in theWorld.Players.Values)
                {
                    // we are taking each segment in a snakes body now -- "body" - a List<Vector2D>
                    // representing the entire body of the snake. (See below for description of Vector2D).
                    // Each point in this list represents one vertex of the snake's body, where consecutive vertices
                    // make up a straight segment of the body. The first point of the list gives the location of the snake's tail,
                    // and the last gives the location of the snake's head. 


                    for (int i = 1; i < snake.body.Count; i++) //loop thru each segment 
                    {
                        Vector2D segmentStart = snake.body[i - 1]; //last actually first 
                        Vector2D segmentEnd = snake.body[i];

                        Color[] scheme = colorPattern[playerID % 8];
                      
                        canvas.StrokeColor = scheme[0];
                        canvas.StrokeDashPattern = null;
                        canvas.StrokeSize = 10;
                        canvas.StrokeLineCap = LineCap.Round;
                        canvas.StrokeLineJoin = LineJoin.Round;
                        canvas.DrawLine((float)segmentStart.X, (float)segmentStart.Y, (float)segmentEnd.X, (float)segmentEnd.Y);

                        canvas.StrokeColor = scheme[1];
                        canvas.StrokeDashPattern = new float[] { 2, 4 };
                        canvas.DrawLine((float)segmentEnd.X, (float)segmentEnd.Y, (float)segmentStart.X, (float)segmentStart.Y);
                        //eyes
                        canvas.StrokeColor = Colors.Black;
                        canvas.StrokeSize = 2;
                        canvas.StrokeDashPattern = new float[] { 0.5f, 2f };
                        canvas.DrawLine((float)segmentEnd.X + 2.5f , (float)segmentEnd.Y + 2.5f, (float)segmentEnd.X - 2.5f, (float)segmentEnd.Y - 2.5f);
                        



                        string nameScore = $"{snake.name} - {snake.score}";
                        canvas.FontColor = Colors.White;
                        canvas.FontSize = 18;
                        canvas.Font = Font.Default;
                        canvas.DrawString(nameScore, (float)snake.body.Last().GetX(), (float)snake.body.Last().GetY(), 380, 100, HorizontalAlignment.Justified, VerticalAlignment.Top);
                    }

                    
                    if (snake.died == true | snake.alive == false)
                    {

                        int i = 1;
                        while (i <= 100)
                        {
                            canvas.StrokeSize = 10;
                            canvas.StrokeColor = Colors.Red;
                            canvas.DrawEllipse((float)snake.body.Last().GetX(), (float)snake.body.Last().GetY(), 20 + i, 20 + i);
                            i += 4;
                        }
                    }
                }


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
                            for (int i = 0; i <= numberOfWalls; i++)
                            {
                                DrawObjectWithTransform(canvas, p, p.p1.GetX(), pointY, 0, WallDrawer);
                                pointY += 50;
                            }
                        }
                        else
                        {
                            for (int i = 0; i <= numberOfWalls; i++)
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
                            for (int i = 0; i <= numberOfWalls; i++)
                            {
                                DrawObjectWithTransform(canvas, p, pointX, p.p1.GetY(), 0, WallDrawer);
                                pointX += 50;
                            }
                        }
                        else
                        {
                            for (int i = 0; i <= numberOfWalls; i++)
                            {
                                DrawObjectWithTransform(canvas, p, pointX, p.p1.GetY(), 0, WallDrawer);
                                pointX -= 50;
                            }
                        }

                    }
                }

                // Draw Powerups
                foreach (var p in theWorld.Powerups.Values)
                {
                    if (p.died == false)
                    {
                        DrawObjectWithTransform(canvas, p, p.loc.GetX(),
                            p.loc.GetY(), 0,
                            PowerupDrawer);
                    }
                }
            }
        }
    }


    /// <summary>
    /// Draw walls at specified location. 
    /// </summary>
    /// <param name="o">The player to draw</param>
    /// <param name="canvas"></param>
    private void WallDrawer(object o, ICanvas canvas)
    {
        Wall w = o as Wall;
        float width = 50;
        float height = 50;
        
        canvas.DrawImage(tiles[w.wall % 3], -(width / 2), -(height / 2), width, height);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="o">The player to draw</param>
    /// <param name="canvas"></param>
    private void PowerupDrawer(object o, ICanvas canvas)
    {
        //Powerup p = o as Powerup;
        //float width = 10;
        //float outerWidth = 15;
        //canvas.FillColor = Colors.Orange;
        //canvas.FillEllipse(-(outerWidth / 2), -(outerWidth / 2), outerWidth, outerWidth);
        //canvas.FillColor = Colors.Green;
        //canvas.FillEllipse(-(width / 2), -(width / 2), width, width);

        float width = 16;
        float height = 16;
        canvas.DrawImage(powerup, -(width / 2), -(height / 2), width, height);
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
    public List<string> GetNameScore()
    {
        List<Snake> snakes = theWorld.Players.Values.ToList();
        for (int i = 0; i < snakes.Count-1; i++)
        {
            for (int j = 0; j < snakes.Count - 1 - i; j++)
            {
                if (snakes[j].score < snakes[j + 1].score)
                {
                    Snake temp = snakes[j];
                    snakes[j] = snakes[j + 1];
                    snakes[j + 1] = temp;
                }
            }
        }
        List<string> nameScore = snakes.Select(snake => $"{snake.name}: {snake.score}").ToList();

        return nameScore;
    }


}
