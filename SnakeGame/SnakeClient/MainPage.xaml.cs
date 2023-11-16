namespace SnakeGame;
/*
 Parker's Notes: 
I spent a good amount of time tonight reading through the requirements and 
gathering about different tasks for this project. The specifications for this assignments 
are semi vauge, which just means that it'll be helpful to create our own TODO list. 

Takaways from the PS8 assignment, Piazza posts and various documentatinos 

*This project will contain a number of different projects, all of which should be of typev class library .NET7.0
of course with the exception of SnakeClient which will act as the view. 

*Breakdown of MVC in terms of projects
View: 
SnakeClient -> entire folder, but mainly this file, MainPage.xaml.cs 
            -> this should not contain a reference to NetowkController since it shouldnt handle any network stuff
            -> The "view project" should create the player's view, or GUI for the game. So it should 
                1)Draw objects in the game and the player's names and scores 
                2)Draw the GUI controls, such as text boxes and buttons
                3)Register basic event handlers for user inputs and controller events. However, 
                  these handlers should mostly just invoke a method in the controller and shouldnt
                  do anything sophisticated. 
            -> Minimum requirements
                1) Allow a player to declare a name and chose a server by IP address or host name
                2) Provide a way to display connection errors and to retry connecting. 
                3) Draw the scene, including snakes, pwoerups, etc... 
                    *At least the first 8 players should be drawn with a unique color or graphic that 
                    identifys that player. Beyond 8 players, you can reuse exisiting assets.
                    *The name of each player should be displayed above their snake.
            

Controller:
GameController.cs  -> Handles processing the information received by the server and updates the model, 
                      informs the view to when to redraw. Look to lecture 19 on how to do this. 
                   -> From PS8 instructions: This will contain logic for parsing the data recieved by the server,
                      updating the model accordingly, and anything else you think belongs here. Key press handlers 
                      in your view should be "landing points" only, and should invoke controller methods that contain heavy logic.
Control commands -> names matter 
    "Moving" - a string representation of whether the player wants to move or not, and the desired direction.
    Possible values are: "none", "up", "left", "down", "right".

NetworkController.cs -> This is already done from PS7. It is important to note that "OnNetworkAction"
                        delegates provided to the networking library will be methods in your snake client. 
                        
    

Model:           -> The classes below can be used by both the client and the server
                    the client's needs are simpler than what the server will need. 
                    This means that in PS8 the model will be passive and only updated by what the 
                    server sends to the client. 
World.cs -> Not from directions, but im thinking this is an object that 
    takes in a list of snakes, walls and powerups? 

Snake.cs -> consists of the following fields (names are important)
    "snake" - an int representing the snake's unique ID.  
    "name" - a string representing the player's name.
    "body" - a List<Vector2D> representing the entire body of the snake. (See below for description of Vector2D).
    Each point in this list represents one vertex of the snake's body, where consecutive vertices make up a straight 
    segment of the body. The first point of the list gives the location of the snake's tail, and the last gives the 
    location of the snake's head. 
    "dir" - a Vector2D representing the snake's orientation. This will always be an axis-aligned vector (purely horizontal or vertical). 
    This can be inferred from other information, but some clients may find it useful.
    "score" - an int representing the player's score (the number of powerups it has eaten)
    "died" - a bool indicating if the snake died on this frame. This will only be true on the exact frame in which the snake died. 
    You can use this to determine when to start drawing an explosion. 
    "alive" - a bool indicating whether a snake is alive or dead. This is helpful for not drawing a snake between the time that it
    dies and the time that it respawns.
    "dc" - a bool indicating if the player controlling that snake disconnected on that frame. The server will send the snake with this
    flag set to true only once, then it will discontinue sending that snake for the rest of the game. You can use this to remove 
    disconnected players from your model.
    "join" - a bool indicating if the player joined on this frame. This will only be true for one frame. 
    This field may not be needed, but may be useful for certain additional View related features.
        
Wall.cs -> Consists of the following fields, names matter 
    "wall" - an int representing the wall's unique ID.
    "p1" - a Vector2D representing one endpoint of the wall.
    "p2" - a Vector2D representing the other endpoint of the wall.
You can assume the following about all walls -> 
    They will always be axis-aligned (purely horizontal or purely vertical, never diagonal). 
    This means p1 and p2 will have either the same x value or the same y value.
    The length between p1 and p2 will always be a multiple of the wall width (50 units).
    The endpoints of the wall can be anywhere (not just multiples of 50), as long as the distance between them is a multiple of 50.
    The order of p1 and p2 is irrelevant (they can be top to bottom, bottom to top, left to right, or right to left).
    Walls can overlap and intersect each other.

PowerUp.cs -> Consists of the following fields, names matter
    "power" - an int representing the powerup's unique ID.
    "loc" - a Vector2D representing the location of the powerup.
    "died" - a bool indicating if the powerup "died" (was collected by a player) on this frame. 
    The server will send the dead powerups only once.


NetworkProtocol: (Copied from PS8 instructions) 
1) Establish a socket connection to the server on port 11000 
2) upon connection, send a single '\n' terminated string representing the player's name. 
   -> The name should be no longer than 16 characters, not including the new line character
3) The server will then send 2 strings representing integer numbers, each terminated by '\n'
   first, the player's unique ID
   second, the size of the world representing borht the width and the height. Since the game world will 
   always be a square, this is a single numeber. 
4) The server will then send all of the walls as JSON objects, each separated by a '\n'. 
5) The server will then continually send the current state of the rest of the game on every frame. 
   The objects are represented by strings containing a JSON onject, in no particular order. 
   Each object ends with a '\n' character. There is no gaurantee that all objects will be included in a single network 
   send/recieve operation. 
6) At any time after receiving its ID, the world size, and the walls, the client can send a command to request to the server. 
   The client shall not send any command requests to the server before receiving its player ID, world size and walls. A command 
   request is a '\n' terminated string containing a JSON object representing the possible operations a client can request. 
   A well-behaved client should not send more than one command request per object frame. 
7) If a snake or powerup is eliminated, the server sends the "dead representation of that object as part of the world on the 
   next frame. If a client connects in-between two frames (A and B), where on Frame A an object was alive, and on Frame B, the 
   object dies, the client may receive the "dead" object without ever having seen the alive version of the object. The client
   must be able to handle this gracefully. 
8) All messages in the communication protocol(both ways) are terminated by a '\n'.


*In general each class should be "self contained" and represent one concern of the overall system. 

*TA Jo's advice on starting the project 
"The first thing that I would try to accomplish is the entire handshake process. 
This means that you should be able to connect to the server, initiate the handshake,
and then receive your player ID, world size, and wall locations back. 
It would then be wise to create your world information (players, food, etc) data structures and classes
so that you can deserialize and store the information continuously sent after the handshake."
 

 */


public partial class MainPage : ContentPage
{
    GameController gameController = new();

    public MainPage()
    {
        InitializeComponent();
        graphicsView.Invalidate();
    }

    void OnTapped(object sender, EventArgs args)
    {
        keyboardHack.Focus();
    }

    void OnTextChanged(object sender, TextChangedEventArgs args)
    {
        Entry entry = (Entry)sender;
        String text = entry.Text.ToLower();
        if (text == "w")
        {
            // Move up
        }
        else if (text == "a")
        {
            // Move left
        }
        else if (text == "s")
        {
            // Move down
        }
        else if (text == "d")
        {
            // Move right
        }
        entry.Text = "";
    }

    private void NetworkErrorHandler()
    {
        // dispatcher here? 
        DisplayAlert("Error", "Disconnected from server", "OK");
    }


    /// <summary>
    /// Event handler for the connect button
    /// We will put the connection attempt interface here in the view.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void ConnectClick(object sender, EventArgs args)
    {
        if (serverText.Text == "")
        {
            DisplayAlert("Error", "Please enter a server address", "OK");
            return;
        }
        if (nameText.Text == "")
        {
            DisplayAlert("Error", "Please enter a name", "OK");
            return;
        }
        if (nameText.Text.Length > 16)
        {
            DisplayAlert("Error", "Name must be less than 16 characters", "OK");
            return;
        }
        
        //Starts the connection process with the controller.
        gameController.Connect(serverText.Text);

       // DisplayAlert("Delete this", "Code to start the controller's connecting process goes here", "OK");

        keyboardHack.Focus();
    }

    /// <summary>
    /// Use this method as an event handler for when the controller has updated the world
    /// </summary>
    public void OnFrame()
    {
        Dispatcher.Dispatch(() => graphicsView.Invalidate());
    }

    private void ControlsButton_Clicked(object sender, EventArgs e)
    {
        DisplayAlert("Controls",
                     "W:\t\t Move up\n" +
                     "A:\t\t Move left\n" +
                     "S:\t\t Move down\n" +
                     "D:\t\t Move right\n",
                     "OK");
    }

    private void AboutButton_Clicked(object sender, EventArgs e)
    {
        DisplayAlert("About",
      "SnakeGame solution\nArtwork by Jolie Uk and Alex Smith\nGame design by Daniel Kopta and Travis Martin\n" +
      "Implementation by ...\n" +
        "CS 3500 Fall 2022, University of Utah", "OK");
    }

    private void ContentPage_Focused(object sender, FocusEventArgs e)
    {
        if (!connectButton.IsEnabled)
            keyboardHack.Focus();
    }
}