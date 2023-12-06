using Microsoft.VisualBasic;
using NetworkUtil;
using System.Reflection;
using System.Drawing;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace SnakeGame;


public class Server
{
    // always lock before modifying or iterating through any of these
    private Dictionary<long, SocketState> clients;
    private readonly World theWorld;

    // Setting values treated as constants
    private long MS_Per_Frame = 0;
    private int Respawn_Rate = 0;
    private double Snake_Speed = 6;
    private int World_Size = 0;
    private int Max_Players = 0;
    private int Max_Powerups = 0;
    private int Powerup_Respawn_Delay = 0;
    private int Snake_Starting_Length = 0;
    private int Snake_Growth_Rate = 0;
    private int Powerup_Eaten_Delay = 0;

    //Basic Movement Vectors
    private Vector2D UP = new Vector2D(0, -1);
    private Vector2D DOWN = new Vector2D(0, 1);
    private Vector2D LEFT = new Vector2D(-1, 0);
    private Vector2D RIGHT = new Vector2D(1, 0);
    private Dictionary<Snake, int> scoreKeeper;


    //TO-DO
    //location for snake to respawn needs to be random, and can not be on top of walls


    /// <summary>
    /// Starts the server, loads xml then continuously outputs frames per second. 
    /// </summary>
    /// <param name="args">Arguments</param>
    static void Main(string[] args)
    {
        Server server = new Server(0);
        server.ParseSettingsXMLFile();
        server.StartServer();

        server.StartFPSCounter();
    }

    /// <summary>
    /// Constructs a server object
    /// </summary>
    /// <param name="s">size of the world</param>
    public Server(int s)
    {
        theWorld = new World(s);
        clients = new Dictionary<long, SocketState>();
    }

    /////////////////////////////////////////////////////////////////////////////////
    ///// SERVER TO CLIENT METHODS 
    ///////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Starts the server and beginins listening for new TCP connections
    /// </summary>
    public void StartServer()
    {
        Networking.StartServer(NewClientConnected, 11000);
        Console.WriteLine("Server is running");
    }

    /// <summary>
    /// This is a delegate callback passed to the networking class to handle a new client connecting. 
    /// Change the callback for the socketState to a new method that recieves the player's name, then ask for data. 
    /// </summary>
    /// <param name="state">Curent state of a client's socket</param>
    private void NewClientConnected(SocketState state)
    {
        if (state.ErrorOccurred)
        {
            RemoveClient(state);
            return;
        }

        // 1)Change the callback for  the SocketState to a new method that recieves the player's name
        state.OnNetworkAction = RecievePlayerName;

        // 2)Ask for Data
        Networking.GetData(state);
    }

    /// <summary>
    /// This is a delegate that implements the server's part of the initial handshake. 
    /// 
    /// Make a new Snake with the given name and unique ID (Recommended using the SocketState'sID). 
    /// Then change the callback to a method that handles command requests from the client. 
    /// Then send the startup info to the client. 
    /// Then add the client's socket to a list of all clients. 
    /// Then ask for data. 
    /// 
    /// Note: it is important that the server sends the startup infor before adding the client to the list of all clients. This 
    /// guarentees that the startup info is sent before any world info. Remember that the server is running a loop on a separate thread 
    /// that may send world info to the list of clients at any time. 
    /// </summary
    /// <param name="state">Curent state of a client's socket</param>
    private void RecievePlayerName(SocketState state)
    {
        if (state.ErrorOccurred)
        {
            RemoveClient(state);
            return;
        }

        // ProcessMessage(state);

        string playerName = state.GetData().Trim();
        // 1) Make a new Snake with the given name and unique ID (Recommended using the SocketState'sID). 
        Snake snake = new((int)state.ID, playerName);

        //Display connection status
        Console.WriteLine("Client (" + state.ID + ") " + playerName + " Connected");

        // 2) Change the callback to a method that handles command requests from the client. 
        state.OnNetworkAction = HandleSnakeMovementCommands; // After this is called, all Networking.Send calls from the client will be move commands

        // 3) Send the startup info to the client.
        SendStartUpJson(state, snake);

        // 4) Add the client's socket to a list of all clients.
        lock (clients)
        {
            clients[state.ID] = state;
        }

        // 5) Ask for Data
        Networking.GetData(state);
    }

    /// <summary>
    /// Sends data to every active client in a thread safe way. 
    /// </summary>
    /// <param name="data">Some text to be sent</param>
    private void SendToAllClients(string data)
    {
        lock (clients)
        {
            foreach (SocketState client in clients.Values)
            {
                Networking.Send(client.TheSocket, data + '\n');
            }
        }
    }

    /// <summary>
    /// Removes the client (in a thread safe way) from the clients socket dictionary
    /// </summary>
    /// <param name="id">SocketState ID</param>
    private void RemoveClient(SocketState state)
    {
        lock (theWorld)
        {
            Console.WriteLine("Client (" + state.ID + ")" + theWorld.Players[(int)state.ID].name + " Disconnected");
            theWorld.Players[(int)state.ID].died = true;
            theWorld.Players[(int)state.ID].alive = false;
            theWorld.Players[(int)state.ID].dc = true;


        }
        lock (clients)
        {
            clients.Remove(state.ID);
        }
    }

    /// <summary>
    /// Private Helper function for sending initial connection JSON data to the client
    /// </summary>
    /// <param name="state">Client connection</param>
    /// <param name="snake">Client snake</param>
    private void SendStartUpJson(SocketState state, Snake snake)
    {
        // send player ID, world size, all walls
        Networking.Send(state.TheSocket, snake.snake + "\n");
        Console.WriteLine("Snake ID: " + snake.snake);
        Networking.Send(state.TheSocket, World_Size.ToString() + "\n");
        Console.WriteLine("World Size: " + World_Size.ToString());
        lock (theWorld)
        {
            // Console.WriteLine("All walls sent as JSON strings");
            foreach (Wall wall in theWorld.Walls.Values)
            {
                string AsJSON = JsonSerializer.Serialize(wall);

                // Console.Write(AsJSON + "\n");
                Networking.Send(state.TheSocket, AsJSON + '\n');
            }
            theWorld.Players.Add(snake.snake, snake);
        }
    }

    /// <summary>
    /// This is a delegate for processing client direction commands. 
    /// 
    /// Process the command then ask for more data. 
    /// 
    /// Note: In order to know which client the request came from , the SocketState must contain the player's ID.
    /// This is what the Id is for in the SocketState class. 
    /// </summary>
    /// <param name="state">Curent state of a client's socket</param>
    private void HandleSnakeMovementCommands(SocketState state)
    {
        if (state.ErrorOccurred)
        {
            RemoveClient(state);
            return;
        }

        lock (theWorld)
        {
            // 1) Process the command
            string movement = state.GetData();
            if (movement.Contains("up"))
            {
                if (!theWorld.Players[(int)state.ID].dir.Equals(UP)
                    && !theWorld.Players[(int)state.ID].dir.Equals(DOWN))
                {
                    double oldX = theWorld.Players[(int)state.ID].body.Last().GetX();
                    double oldY = theWorld.Players[(int)state.ID].body.Last().GetY();
                    Vector2D newHead = new(oldX, oldY);
                    theWorld.Players[(int)state.ID].body.Add(newHead);
                    theWorld.Players[(int)state.ID].dir = UP;

                }
                state.RemoveData(0, movement.Length);
            }
            else if (movement.Contains("left"))
            {
                if (!theWorld.Players[(int)state.ID].dir.Equals(LEFT)
                   && !theWorld.Players[(int)state.ID].dir.Equals(RIGHT))
                {
                    double oldX = theWorld.Players[(int)state.ID].body.Last().GetX();
                    double oldY = theWorld.Players[(int)state.ID].body.Last().GetY();
                    Vector2D newHead = new(oldX, oldY);
                    theWorld.Players[(int)state.ID].body.Add(newHead);
                    theWorld.Players[(int)state.ID].dir = LEFT;

                }
                state.RemoveData(0, movement.Length);
            }
            else if (movement.Contains("down"))
            {
                if (!theWorld.Players[(int)state.ID].dir.Equals(DOWN)
                   && !theWorld.Players[(int)state.ID].dir.Equals(UP))
                {
                    double oldX = theWorld.Players[(int)state.ID].body.Last().GetX();
                    double oldY = theWorld.Players[(int)state.ID].body.Last().GetY();
                    Vector2D newHead = new(oldX, oldY);
                    theWorld.Players[(int)state.ID].body.Add(newHead);
                    theWorld.Players[(int)state.ID].dir = DOWN;

                }
                state.RemoveData(0, movement.Length);
            }
            else if (movement.Contains("right"))
            {
                if (!theWorld.Players[(int)state.ID].dir.Equals(RIGHT)
                 && !theWorld.Players[(int)state.ID].dir.Equals(LEFT))
                {
                    double oldX = theWorld.Players[(int)state.ID].body.Last().GetX();
                    double oldY = theWorld.Players[(int)state.ID].body.Last().GetY();
                    Vector2D newHead = new(oldX, oldY);
                    theWorld.Players[(int)state.ID].body.Add(newHead);
                    theWorld.Players[(int)state.ID].dir = RIGHT;

                }
                state.RemoveData(0, movement.Length);
            }
        }

        // 2) ask for more data
        Networking.GetData(state);
    }

    /////////////////////////////////////////////////////////////////////////////////////////
    /// UPDATE AND WORLD STATE METHODS
    ////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Starts the FPS counter and continues the loop on forever. 
    /// </summary>
    public void StartFPSCounter()
    {
        System.Diagnostics.Stopwatch fpsWatch = new System.Diagnostics.Stopwatch();
        System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();

        while (true)
        {
            int FPS = 0;
            fpsWatch.Start();
            while (fpsWatch.ElapsedMilliseconds < 1000)
            {
                watch.Start();
                // wait until the next frame
                while (watch.ElapsedMilliseconds < MS_Per_Frame)
                { //empty here because we're timing the systems counter per Frame
                }

                //In place to make sure that a snake gets the desired amount of frames to grow when eating a powerup
                if (Powerup_Eaten_Delay > 0)
                    Powerup_Eaten_Delay--;

                FPS++;
                watch.Restart();
                Update();
                MoveSnake();
                Collision();
                //WrapAround();
                CheckForRespawns();
                MaintainPowerups();
            }
            Console.WriteLine("FPS: " + FPS);
            fpsWatch.Restart();
        }
    }

    /// <summary>
    /// Keeps the snake's head moving and keeps the tail following the second to last snake segment
    /// provided that the snakes length is not 2 segments long
    /// </summary>
    private void MoveSnake()
    {
        lock (theWorld)
        {
            foreach (Snake snake in theWorld.Players.Values)
            {
                //Compute the velocity of the snake's head based on the player's updated movement commmand
                Vector2D velocityVector = new();

                // Add velocity to the head's position
                if (snake.dir.Equals(UP))
                {
                    velocityVector = new Vector2D(0, -Snake_Speed);
                    snake.body[snake.body.Count - 1] += velocityVector;
                }
                else if (snake.dir.Equals(DOWN))
                {
                    velocityVector = new Vector2D(0, Snake_Speed);
                    snake.body[snake.body.Count - 1] += velocityVector;
                }
                else if (snake.dir.Equals(LEFT))
                {
                    velocityVector = new Vector2D(-Snake_Speed, 0);
                    snake.body[snake.body.Count - 1] += velocityVector;
                }
                else if (snake.dir.Equals(RIGHT))
                {
                    velocityVector = new Vector2D(Snake_Speed, 0);
                    snake.body[snake.body.Count - 1] += velocityVector;
                }

                //move the tail vertext by its velocity
                //The tail's velocity is towards the next vertex in the body with a speed equal to the snake's speed

                // if a snake has eaten a powerup, then the tail wont grow, making it longer.
                if (Powerup_Eaten_Delay == 0)
                {
                    // if there is only two segments in the list, their velocites are the same, therefore no action necessary 
                    if (snake.body.Count > 2)
                    {
                        velocityVector = GetSnakesTailDirectionVector(snake);
                        snake.body[0] += velocityVector;

                        // if the snake tail and the segment before it have the same position then remove the tail 
                        if (snake.body[0].Equals(snake.body[1]))
                        {
                            snake.body.RemoveAt(0);
                        }
                    }
                    else
                    {
                        snake.body[0] += velocityVector;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets the direction that the tail should go based on the previous body segment in the list. 
    /// </summary>
    /// <returns> Direction the Snake's Tail should travel</returns>
    private Vector2D GetSnakesTailDirectionVector(Snake s)
    {

        Vector2D secondToLastVector = new Vector2D(s.body[1]);
        Vector2D LastVector = new Vector2D(s.body[0]);
        float angle = Vector2D.AngleBetweenPoints(secondToLastVector, LastVector);
        switch (angle)
        {
            //Negative X
            case -90: return new Vector2D(-Snake_Speed, 0);
            //Positive Y
            case 0: return new Vector2D(0, -Snake_Speed);
            //Positive X
            case 90: return new Vector2D(Snake_Speed, 0);
            //Negative Y
            case 180: return new Vector2D(0, Snake_Speed);
        }
        return new Vector2D();
    }

    /// <summary>
    /// Checks for any kind of collision
    /// </summary>
    private void Collision()
    {
        lock (theWorld)
        {
            foreach (Snake snake in theWorld.Players.Values)
            {
                //Snake Collides with Wall
                foreach (Wall wall in theWorld.Walls.Values)
                {
                    Vector2D head = snake.body.Last();

                    //for walls drawn top to bottom and left to right
                    double wallStartX = wall.p1.GetX() - 25;
                    double wallEndX = wall.p2.GetX() + 25;
                    double wallStartY = wall.p1.GetY() - 25;
                    double wallEndY = wall.p2.GetY() + 25;

                    if (head.GetX() >= wallStartX && head.GetX() <= wallEndX &&
                    head.GetY() >= wallStartY && head.GetY() <= wallEndY)
                    {
                        snake.died = true;
                        snake.alive = false;
                    }

                    //for walls drawn bottom to top and right to left
                    wallStartX = wall.p1.GetX() + 25;
                    wallEndX = wall.p2.GetX() - 25;
                    wallStartY = wall.p1.GetY() + 25;
                    wallEndY = wall.p2.GetY() - 25;

                    if (head.GetX() <= wallStartX && head.GetX() >= wallEndX &&
                    head.GetY() <= wallStartY && head.GetY() >= wallEndY)
                    {
                        snake.died = true;
                        snake.alive = false;
                    }
                }

                //Snake Collides with powerup
                foreach (Powerup power in theWorld.Powerups.Values)
                {
                    int powerupCollisionRadius = power.GetCollisionRadius();
                    Vector2D powerupLocation = power.loc;
                    int snakeHeadCollisionRadius = snake.GetCollisonRadius();
                    Vector2D snakeHeadLocation = snake.body.Last();
                    double distance = Vector2D.DistanceBetweenTwoVectors(powerupLocation, snakeHeadLocation);
                    int totalRadius = powerupCollisionRadius + snakeHeadCollisionRadius;

                    if (distance <= totalRadius)
                    {
                        power.died = true;
                        snake.score++;
                        Powerup_Eaten_Delay = 24; // Grows the snake 

                    }
                }

                //Snake Collides with other Snake
                //TODO:
                SnakeSnakeCollision(snake);

                //Snake Collides with itself
                //TODO:

                //ZigZag movement from lecture?
                //TODO:
            }
        }
    }

    private void SnakeSnakeCollision(Snake snake) //heavent checked this and need to check if head collides with any point betweeen vectors of othersnake body
    {
        foreach (Snake otherSnake in theWorld.Players.Values)
        {
            if (snake != otherSnake)
            {
                // head - head collision
                Vector2D head = snake.body.Last();
                Vector2D otherSnakeHead = otherSnake.body.Last();

                if (head.Equals(otherSnake))
                {
                    snake.died = true;
                    snake.alive = false;
                    return;
                }

                // head - body collision
                for (int i = 0; i < otherSnake.body.Count - 1; i++)
                {
                    if (head.Equals(otherSnake.body[i]))
                    {
                        snake.died = true;
                        snake.alive = false;
                    }
                    else if (otherSnakeHead.Equals(snake.body[i]))
                    {
                        otherSnake.died = true;
                        otherSnake.alive = false;
                    }

                }
            }
        }
    }


    private void CheckForRespawns() //need to add logic that checks if objects are in way
    {
        foreach (Snake snake in theWorld.Players.Values)
        {
            if (snake.died == true)
            {
                //need to randomize snakes new location
                snake.score = 0; //set score back to zero
                int adjustedSize = Snake_Starting_Length;

                //Vector2D newHead = new(rand.Next(-800, 800), rand.Next(-800, 800));
                //Vector2D newTail = new(rand.Next(-800, 800), rand.Next(-800, 800));
                Vector2D newHead = new(1, 120); //hard coded for now because random is not working for some reason
                Vector2D newTail = new(1, 0);

                List<Vector2D> newBody = new List<Vector2D> { newTail, newHead };
                snake.dir = DOWN; // Starting downward
                snake.body = newBody;
                snake.alive = true;
                snake.died = false;
            }
        }
    }

    /// <summary>
    /// Consistantly maintians a desired amount of powerups on the board.
    /// </summary>
    private void MaintainPowerups()
    {
        Random rand = new Random(); // There has to be a better way to do this. This still has the chance to throw an exception
        int ID = rand.Next();
        if (theWorld.Powerups.Count < Max_Powerups)
        {
            for (int i = 0; i < Max_Powerups - theWorld.Powerups.Count; i++)
            {
                ID += 12;
                Powerup powerup = new Powerup(ID, GeneratePowerupSpawnLocation(), false);
                theWorld.Powerups.Add(ID, powerup);
            }
        }
    }

    /// <summary>
    /// Provides a Vector that doesnt collide with exisiting walls or snakes in the world. 
    /// X and Y values are randomly assigned but within the bounds of the world size. 
    /// </summary>
    /// <returns></returns>
    private Vector2D GeneratePowerupSpawnLocation()
    {
        Random rand = new Random();
        Vector2D location;
        bool invalidLocation;

        do
        {
            int Xcoord = rand.Next(-World_Size / 2, World_Size / 2);
            int Ycoord = rand.Next(-World_Size / 2, World_Size / 2);

            location = new Vector2D(Xcoord, Ycoord);
            invalidLocation = false;

            foreach (Wall wall in theWorld.Walls.Values)
            {


                double wallStartX = wall.p1.GetX() - 50;
                double wallEndX = wall.p2.GetX() + 50;
                double wallStartY = wall.p1.GetY() - 50;
                double wallEndY = wall.p2.GetY() + 50;

                double oppWallStartX = wall.p1.GetX() + 50;
                double oppWallEndX = wall.p2.GetX() - 50;
                double oppWallStartY = wall.p1.GetY() + 50;
                double oppWallEndY = wall.p2.GetY() - 50;

                if ((location.GetX() >= wallStartX && location.GetX() <= wallEndX &&
                     location.GetY() >= wallStartY && location.GetY() <= wallEndY) ||
                    (location.GetX() <= oppWallStartX && location.GetX() >= oppWallEndX &&
                     location.GetY() <= oppWallStartY && location.GetY() >= oppWallEndY))
                {
                    invalidLocation = true;
                    break;
                }
            }
        } while (invalidLocation);

        return location;
    }


    public Boolean WallCollision(Vector2D obj)
    {
        Boolean WallCollision = false;
        foreach (Wall wall in theWorld.Walls.Values)
        {
            double wallStartX = wall.p1.GetX() - 50;
            double wallEndX = wall.p2.GetX() + 50;
            double wallStartY = wall.p1.GetY() - 50;
            double wallEndY = wall.p2.GetY() + 50;

            double oppWallStartX = wall.p1.GetX() + 50;
            double oppWallEndX = wall.p2.GetX() - 50;
            double oppWallStartY = wall.p1.GetY() + 50;
            double oppWallEndY = wall.p2.GetY() - 50;

            if ((obj.GetX() >= wallStartX && obj.GetX() <= wallEndX &&
                 obj.GetY() >= wallStartY && obj.GetY() <= wallEndY) ||
                (obj.GetX() <= oppWallStartX && obj.GetX() >= oppWallEndX &&
                 obj.GetY() <= oppWallStartY && obj.GetY() >= oppWallEndY))
            {
                WallCollision = true;
            
            }
            WallCollision = false;
        }
        return WallCollision;
    }

    public Boolean PowerupCollision(Vector2D obj)
    {
        return true;
    }
    



    private void WrapAround()
    {
        foreach (Snake snake in theWorld.Players.Values)
        {
            double range = World_Size / 2;
            List<Vector2D> updatedBody = new List<Vector2D>();

            // tail to head [] []
            // 
            // ()    ()heaed
            // ()
            // () ()  <-   ()tail
            foreach (Vector2D segment in snake.body)
            {
                double newX = segment.GetX();
                double newY = segment.GetY();

                if (segment.GetX() > range)
                {
                    newX = -range;
                }
                else if (segment.GetX() < -range)
                {
                    newX = range;
                }

                if (segment.GetY() > range)
                {
                    newY = -range;
                }
                else if (segment.GetY() < -range)
                {
                    newY = range;
                }

                updatedBody.Add(new Vector2D(newX, newY));
            }
            snake.body = updatedBody;
        }
    }

    /// <summary>
    /// This is the method invoked every iteration through the frame loop. 
    /// Update the world then send it to each client
    /// </summary>
    private void Update()
    {
        lock (theWorld)
        {
            //Check if there is any dead snakes or powerups to remove, then remove them. 
            IEnumerable<int> IDsOfSnakesToRemove = theWorld.Players.Values.Where(x => x.died).Select(x => x.snake);
            foreach (int snakeID in IDsOfSnakesToRemove)
                theWorld.Players.Remove(snakeID);

            IEnumerable<int> IDsOfPowerupsToRemove = theWorld.Powerups.Values.Where(x => x.died).Select(x => x.power);
            foreach (var powerups in IDsOfPowerupsToRemove)
            {
                Console.WriteLine("A powerup has been removed");
                theWorld.Powerups.Remove(powerups);

            }

            //send snake and powerup data to client 
            foreach (Snake snake in theWorld.Players.Values)
            {
                try
                {
                    string JsonSnake = JsonSerializer.Serialize(snake);
                    //Console.Write(JsonSnake + "\n");
                    SendToAllClients(JsonSnake);
                }
                catch (JsonException e)
                {

                    Console.WriteLine("Error Parsing Snake JSON: " + e);
                }

            }

            foreach (Powerup powerup in theWorld.Powerups.Values)
            {
                try
                {
                    string JsonPowerup = JsonSerializer.Serialize(powerup);
                    // Console.Write(JsonPowerup + "\n");
                    SendToAllClients(JsonPowerup);

                }
                catch (JsonException e)
                {
                    Console.WriteLine("Error Parsing Powerup JSON: " + e);
                }
            }

        }
    }

    /// <summary>
    /// Parses XML file and gets settings for the server.
    /// </summary>
    private void ParseSettingsXMLFile()
    {
        string relativePath = "settings.xml";

        //First method of parsing XML is used here because the data that is parsed are just primitive types. 
        // Load the XML document from a file
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.Load(relativePath);

        // Specify the name of the custom tag
        string respawnRateTag = "//RespawnRate";
        string msPerFrameTag = "//MSPerFrame";
        string universeSizeTag = "//UniverseSize";
        string powerupCapTag = "//PowerupCap";
        string playerCapTag = "//PlayerCap";
        string snakeSpeedTag = "//SnakeSpeed";
        string snakeStartingLengthTag = "//SnakeStartingLength";
        string snakeGrowthRateTag = "//SnakeGrowthRate";
        string powerupRespawnDelayTag = "//PowerupRespawnDelay";

        // Select the XML node using the tag name
        XmlNode respawnRateNode = xmlDoc.SelectSingleNode(respawnRateTag)!;
        XmlNode msPerFrameNode = xmlDoc.SelectSingleNode(msPerFrameTag)!;
        XmlNode universeSizeNode = xmlDoc.SelectSingleNode(universeSizeTag)!;
        XmlNode powerupCapNode = xmlDoc.SelectSingleNode(powerupCapTag)!;
        XmlNode playerCapNode = xmlDoc.SelectSingleNode(playerCapTag)!;
        XmlNode snakeSpeedNode = xmlDoc.SelectSingleNode(snakeSpeedTag)!;
        XmlNode snakeStartingLengthNode = xmlDoc.SelectSingleNode(snakeStartingLengthTag)!;
        XmlNode snakeGrowthRateNode = xmlDoc.SelectSingleNode(snakeGrowthRateTag)!;
        XmlNode powerupRespawnDelayNode = xmlDoc.SelectSingleNode(powerupRespawnDelayTag)!;


        if (respawnRateNode != null && int.TryParse(respawnRateNode.InnerText, out int respawnRate))
        {
            Console.WriteLine($"Parsed CheckForRespawns Rate: {respawnRate}");
            this.Respawn_Rate = respawnRate;
        }
        if (msPerFrameNode != null && int.TryParse(msPerFrameNode.InnerText, out int msPerFrame))
        {
            Console.WriteLine($"Parsed MSPerFrame: {msPerFrame}");
            this.MS_Per_Frame = msPerFrame;
        }
        if (universeSizeNode != null && int.TryParse(universeSizeNode.InnerText, out int universeSize))
        {
            Console.WriteLine($"Parsed UniverseSize: {universeSize}");
            World_Size = universeSize;
        }
        if (powerupCapNode != null && int.TryParse(powerupCapNode.InnerText, out int powerupCap))
        {
            Console.WriteLine($"Parsed PowerupCap: {powerupCap}");
            Max_Powerups = powerupCap;
        }
        if (playerCapNode != null && int.TryParse(playerCapNode.InnerText, out int playerCap))
        {
            Console.WriteLine($"Parsed PlayerCap: {playerCap}");
            Max_Players = playerCap;
        }
        if (snakeSpeedNode != null && int.TryParse(snakeSpeedNode.InnerText, out int snakeSpeed))
        {
            Console.WriteLine($"Parsed Snake Speed: {snakeSpeed}");
            Snake_Speed = snakeSpeed;
        }
        if (snakeStartingLengthNode != null && int.TryParse(snakeStartingLengthNode.InnerText, out int snakeStartingLength))
        {
            Console.WriteLine($"Parsed Snake Starting Length: {snakeStartingLength}");
            Snake_Starting_Length = snakeStartingLength;
        }
        if (snakeGrowthRateNode != null && int.TryParse(snakeGrowthRateNode.InnerText, out int snakeGrowthRate))
        {
            Console.WriteLine($"Parsed Snake Growth Rate: {snakeGrowthRate}");
            Snake_Growth_Rate = snakeGrowthRate;
        }
        if (powerupRespawnDelayNode != null && int.TryParse(powerupRespawnDelayNode.InnerText, out int powerupRespawnDelay))
        {
            Console.WriteLine($"Parsed Powerup Respawn Delay: {powerupRespawnDelay}");
            Powerup_Respawn_Delay = powerupRespawnDelay;
        }


        //Parsing walls from file 
        //Different method here because Walls are custom objects
        DataContractSerializer wallSer = new DataContractSerializer(typeof(Wall));
        FileStream fs = new FileStream(relativePath, FileMode.Open);
        XmlDictionaryReader reader =
        XmlDictionaryReader.CreateTextReader(fs, new XmlDictionaryReaderQuotas());
        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    if (wallSer.IsStartObject(reader))
                    {
                        Wall p = (Wall)wallSer.ReadObject(reader)!;
                        lock (theWorld)
                        {
                            theWorld.Walls.Add(p.wall, p);
                        }

                    }
                    break;
            }
        }
    }

}





