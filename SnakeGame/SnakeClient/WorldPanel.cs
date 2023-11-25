using IImage = Microsoft.Maui.Graphics.IImage;
#if MACCATALYST
using Microsoft.Maui.Graphics.Platform;
#else
using Microsoft.Maui.Graphics.Win2D;
#endif
using Color = Microsoft.Maui.Graphics.Color;
using System.Reflection;
using Font = Microsoft.Maui.Graphics.Font;

namespace SnakeGame;

/// <summary>
/// <Author>Abbey Lasater</Author>
/// <Author>Parker Middleton</Author>
/// <Date>November 24th, 2023</Date>
/// 
/// A class for drawing the world state of an ongoing snake game. 
/// This class creates the view for the project by drawing every object in 
/// "theWorld" on every frame sent by the server. 
/// 
/// </summary>
public class WorldPanel : IDrawable
{
    private IImage tile1;
    private IImage tile2;
    private IImage tile3;
    private IImage tile4;
    private IImage powerup;
    private IImage background;
    private IImage logo;
    private IImage[] tiles;

    /// <summary>
    /// Delegate for drawing various objects to the canvas all in the same fashion. 
    /// </summary>
    /// <param name="o">Object to be drawn to the canvas</param>
    /// <param name="canvas">Graphical Canvas</param>
    public delegate void ObjectDrawer(object o, ICanvas canvas);
    private World theWorld;
    private int playerID;
    private int viewSize = 900;
    private bool initializedForDrawing = false;
    private Color[][] colorPattern = { new Color[] {Colors.Red,Colors.PaleVioletRed},
                                  new Color[]{Colors.Blue, Colors.Aqua},
                                    new Color[]{Colors.DarkOrange, Colors.Orange},
                                    new Color []{Colors.Green, Colors.LightGreen},
                                    new Color []{Colors.Yellow, Colors.LightGoldenrodYellow},
                                    new Color[] {Colors.Purple, Colors.MediumPurple},
                                    new Color[]{Colors.Crimson, Colors.White },
                                    new Color[]{Colors.Snow, Colors.LightGrey}
             };

    /// <summary>
    /// Initializes images from assembly stream. 
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
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
    /// Constructor for WorldPanel. 
    /// </summary>
    public WorldPanel()
    {

    }

    /// <summary>
    /// A setter for current world state 
    /// </summary>
    /// <param name="world"> World Object </param>
    public void SetWorld(World world)
    {
        theWorld = world;
    }

    /// <summary>
    /// A setter for current player's ID.
    /// </summary>
    /// <param name="ID"> Player ID </param>
    public void SetPlayerID(int ID)
    {
        playerID = ID;
    }

    /// <summary>
    /// Helper method that loads all images and stores different tiles into an array. 
    /// </summary>
    private void InitializeDrawing()
    {
        tile1 = loadImage("neontile2.jpg");
        tile2 = loadImage("neontile3.jpg");
        tile3 = loadImage("neontile4.jpg");
        tile4 = loadImage("neontile5.jpg");
        powerup = loadImage("neonpowerup.png");
        background = loadImage("cosmic.png");
        logo = loadImage("neonsnakeslogo.png");
        initializedForDrawing = true;
        tiles = new IImage[] { tile1, tile2, tile3, tile4 };
    }

    /// <summary>
    /// Draw method that gets called on every frame. Constantly drawing the world state. 
    /// </summary>
    /// <param name="canvas">Graphical Canvas</param>
    /// <param name="dirtyRect">Dimentions data for the graphical view of the draw method</param>
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // start up condition
        if (!initializedForDrawing)
            InitializeDrawing();

        //undo previous transition from last frame
        canvas.ResetState();
      
        //Prevents race condition by locking critical selection 
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
                    // draw individual segment
                    for (int i = 1; i < snake.body.Count; i++)
                    {

                        Vector2D segmentStart = snake.body[i - 1]; //last actually first 
                        Vector2D segmentEnd = snake.body[i];
                        int currentPlayerID = snake.snake;
                        Color[] scheme = colorPattern[currentPlayerID % 8];

                        //Primary color
                        canvas.StrokeColor = scheme[0];
                        canvas.StrokeDashPattern = null;
                        canvas.StrokeSize = 10;
                        canvas.StrokeLineCap = LineCap.Round;
                        canvas.StrokeLineJoin = LineJoin.Round;
                        canvas.DrawLine((float)segmentStart.X, (float)segmentStart.Y, (float)segmentEnd.X, (float)segmentEnd.Y);

                        //Secondary color
                        canvas.StrokeColor = scheme[1];
                        canvas.StrokeDashPattern = new float[] { 2, 4 };
                        canvas.DrawLine((float)segmentEnd.X, (float)segmentEnd.Y, (float)segmentStart.X, (float)segmentStart.Y);
                        //Eyes
                        canvas.StrokeColor = Colors.Black;
                        canvas.StrokeSize = 2;
                        canvas.StrokeDashPattern = new float[] { 0.6f, 2.2f };
                        canvas.DrawLine((float)segmentEnd.X + 2.5f, (float)segmentEnd.Y + 2.5f, (float)segmentEnd.X - 2.5f, (float)segmentEnd.Y - 2.5f);
                        //Name and Score
                        string nameScore = $"{snake.name} - {snake.score}";
                        canvas.FontColor = Colors.White;
                        canvas.FontSize = 18;
                        canvas.Font = Font.Default;
                        canvas.DrawString(nameScore, (float)snake.body.Last().GetX() - 25, (float)snake.body.Last().GetY(), 380, 100, HorizontalAlignment.Justified, VerticalAlignment.Top);
                    }


                    if (snake.died == true | snake.alive == false)
                    {
                        canvas.FillColor = Colors.Red;
                        canvas.FillCircle((float)snake.body.Last().GetX(), (float)snake.body.Last().GetY() - 25, 50);
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
            else
            {
                canvas.DrawImage(logo, 0, 0, 900, 900);
            }
        }
    }

    /// <summary>
    /// Draw walls at specified location. 
    /// </summary>
    /// <param name="o">The wall to be draw</param>
    /// <param name="canvas">Graphical Canvas</param>
    private void WallDrawer(object o, ICanvas canvas)
    {
        Wall w = o as Wall;
        float width = 50;
        float height = 50;
        canvas.DrawImage(tiles[w.wall % 3], -(width / 2), -(height / 2), width, height);
    }

    /// <summary>
    /// Draws powerup objects to the view
    /// </summary>
    /// <param name="o">The powerup to be drawn</param>
    /// <param name="canvas">Graphical Canvas</param>
    private void PowerupDrawer(object o, ICanvas canvas)
    {
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
        canvas.SaveState();
        canvas.Translate((float)worldX, (float)worldY);
        canvas.Rotate((float)angle);
        drawer(o, canvas);
        canvas.RestoreState();
    }

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
