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

namespace SnakeGame;


public class WorldPanel : IDrawable
{
    private IImage wall;
    private IImage background;

    // A delegate for DrawObjectWithTransform
    // Methods matching this delegate can draw whatever they want onto the canvas
    public delegate void ObjectDrawer(object o, ICanvas canvas);

    private GraphicsView graphicsView = new();

    private int viewSize = 500; //  this should be sent by the server

    private World theWorld;


    // for use of drawing dummy data for the view
    Wall wall23 = new Wall(23, new Vector2D(755, -200), new Vector2D(755, -100));
    Wall wall22 = new Wall(22, new Vector2D(755, -200), new Vector2D(755, -100));
    
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

    private void InitializeDrawing()
    {
        wall = loadImage( "wallsprite.png" );
        background = loadImage( "background.png" );
        initializedForDrawing = true;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if ( !initializedForDrawing )
            InitializeDrawing();

        // undo previous transformations from last frame
        canvas.ResetState();
        
       // example code for how to draw
       // (the image is not visible in the starter code)
       canvas.DrawImage(wall, 0, 0, wall.Width, wall.Height);
        canvas.DrawImage(background, -250, 250, background.Width , background.Height);
    }

}
