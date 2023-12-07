using NetworkUtil;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Xml;

namespace SnakeGame;

/// <summary>
/// <Author>Abbey Lasater</Author>
/// <Author>Parker Middleton</Author>
/// <Date>December 6th, 2023</Date>
/// 
/// The server class provides basic mechanics for the multiplayer snake game while also 
/// gracefully maintaining connections to clients using the MVC. 
/// 
/// </summary>
public class Server
{
    /// <summary>
    /// List of active connections
    /// </summary>
    private readonly Dictionary<long, SocketState> clients;
    /// <summary>
    /// State of the World
    /// </summary>
    private readonly World theWorld;

    private long MS_Per_Frame = 0;
    private int Respawn_Rate = 0;
    private double Snake_Speed = 6;
    private int World_Size = 0;
    private int Max_Powerups = 0;
    private int Powerup_Respawn_Timer = 0;
    private int Powerup_Respawn_Delay = 0;
    private int Snake_Starting_Length = 0;
    private int Snake_Growth_Rate = 0;

    //Basic Movement Vectors
    private readonly Dictionary<int, Vector2D> Directions = new();

    //Individual directions
    private readonly Vector2D UP = new(0, -1);
    private readonly Vector2D DOWN = new(0, 1);
    private readonly Vector2D LEFT = new(-1, 0);
    private readonly Vector2D RIGHT = new(1, 0);


    /// <summary>
    /// Begins the program, initiates multiple running components  
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
    /// <param name="s">World Size</param>
    public Server(int s)
    {
        theWorld = new World(s);
        clients = new Dictionary<long, SocketState>();
        Directions.Add(0, UP);
        Directions.Add(1, DOWN);
        Directions.Add(2, LEFT);
        Directions.Add(3, RIGHT);
        Powerup_Respawn_Delay = Powerup_Respawn_Timer;
    }

    /////////////////////////////////////////////////////////////////////////////////
    ///// NETWORKING ORIENTED METHODS
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
        state.OnNetworkAction = RecievePlayerName;
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

        string playerName = state.GetData().Trim();

        //Make a new Snake with the given name and unique ID (Recommended using the SocketState'sID). 
        // Vector2D direction;
        //List<Vector2D> body;
        // GetValidPlayerStartingData(out direction, out body);
        Snake snake = new((int)state.ID, playerName, Respawn_Rate, 0);//, body, direction);

        //Display connection status
        Console.WriteLine("Client (" + state.ID + ") " + playerName + " Connected");

        //Change the callback to a method that handles command requests from the client. 
        state.OnNetworkAction = HandleSnakeMovementCommands;

        //Send the startup info to the client.
        SendStartUpJson(state, snake);

        //Add the client's socket to a list of all clients.
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
    /// <param name="state">SocketState ID</param>
    private void RemoveClient(SocketState state)
    {
        lock (theWorld)
        {
            Console.WriteLine("Client (" + state.ID + ") " + theWorld.Players[(int)state.ID].name + " Disconnected");
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
    /// Sends start up data for a client to connect via JSON
    /// </summary>
    /// <param name="state">Client connection</param>
    /// <param name="snake">Client snake</param>
    private void SendStartUpJson(SocketState state, Snake snake)
    {
        Networking.Send(state.TheSocket, snake.snake + "\n");
        Networking.Send(state.TheSocket, World_Size.ToString() + "\n");
        lock (theWorld)
        {
            foreach (Wall wall in theWorld.Walls.Values)
            {
                string AsJSON = JsonSerializer.Serialize(wall);
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
            //Process the command
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

        //ask for more data
        Networking.GetData(state);
    }

    /////////////////////////////////////////////////////////////////////////////////////////
    /// WORLD STATE ORIENTED METHODS
    ////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Starts the FPS counter and continues the loop on forever. 
    /// </summary>
    public void StartFPSCounter()
    {
        System.Diagnostics.Stopwatch fpsWatch = new();
        System.Diagnostics.Stopwatch watch = new();
        while (true)
        {
            int FPS = 0;
            fpsWatch.Start();
            while (fpsWatch.ElapsedMilliseconds < 1000)
            {
                watch.Start();
                while (watch.ElapsedMilliseconds < MS_Per_Frame)
                {

                }
                watch.Restart();

                UpdatePlayerAndPowerupCounts();
                UpdateSnakeMovement();
                CheckForCollisions();
                CheckForWrapAround();
                CheckForPlayerRespawns();
                MakeSurePowerupsAreAtMaximum();
                FPS++;

            }
            Console.WriteLine("FPS: " + FPS);
            fpsWatch.Restart();
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////
    /// SNAKE MOVEMENT ORIENTED METHODS
    /////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Keeps the snake's head moving and keeps the tail following the second to last snake segment
    /// provided that the snakes length is not 2 segments long
    /// </summary>
    private void UpdateSnakeMovement()
    {
        lock (theWorld)
        {
            foreach (Snake snake in theWorld.Players.Values)
            {
                if (snake.alive)
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
                    if (snake.GrowthRate == 0)
                    {
                        // if there is only two segments in the list, their velocites are the same, therefore no action necessary 
                        if (snake.body.Count > 2)
                        {
                            velocityVector = GetSnakesTailDirectionVector(snake);
                            snake.body[0] += velocityVector;

                            // if the snake tail and the segment before it have the same position then remove the tail 
                            if (Vector2D.EqualsOtherVectorWithinRange(snake.body[0], snake.body[1], 3))
                            {
                                snake.body.RemoveAt(0);
                            }
                        }
                        else
                        {
                            snake.body[0] += velocityVector;
                        }
                    }
                    else
                    {
                        snake.GrowthRate--;
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
    /// Determines if a wrap around has occoured and adjusts the snake accordingly
    /// </summary>
    private void CheckForWrapAround()
    {
        foreach (Snake snake in theWorld.Players.Values)
        {
            double range = World_Size / 2;
            List<Vector2D> updatedBody = new List<Vector2D>();
            double xCord = 0;
            double yCord = 0;

            if (snake.body.First().GetX() > range || snake.body.First().GetX() < -range || snake.body.First().GetY() > range || snake.body.First().GetY() < -range)
            {
                foreach (Vector2D segment in snake.body)
                {
                    //x-coord
                    if (snake.dir.GetX() != 0 && segment.GetX() > 0)
                    {
                        xCord = (segment.GetX() * -1) + Snake_Starting_Length + 1;
                    }
                    else if (snake.dir.GetX() != 0 && segment.GetX() < 0)
                    {
                        xCord = (segment.GetX() * -1) - Snake_Starting_Length - 1;
                    }
                    else
                    {
                        xCord = segment.GetX();
                    }

                    //Y-coord
                    if (snake.dir.GetY() != 0 && segment.GetY() > 0)
                    {
                        yCord = (segment.GetY() * -1) + Snake_Starting_Length + 1;
                    }
                    else if (snake.dir.GetY() != 0 && segment.GetY() < 0)
                    {
                        yCord = (segment.GetY() * -1) - Snake_Starting_Length - 1;
                    }
                    else
                    {
                        yCord = segment.GetY();
                    }
                    Vector2D newSegment = new(xCord, yCord);
                    updatedBody.Add(newSegment);
                    updatedBody = ReverseList(updatedBody);
                }
                snake.body = updatedBody;
            }
        }
    }

    /// <summary>
    /// Upon WrapAround, reverses the order of the body array.
    /// </summary>
    /// <param name="ListToReverse">Body</param>
    /// <returns>A reversed List</returns>
    public List<Vector2D> ReverseList(List<Vector2D> ListToReverse)
    {
        int count = ListToReverse.Count;
        List<Vector2D> newList = new List<Vector2D>();

        for (int i = count - 1; i >= 0; i--)
        {
            newList.Add(ListToReverse[i]);
        }

        return newList;
    }

    /////////////////////////////////////////////////////////////////////////////////////
    /// COLLISION ORIENTED METHODS 
    /////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Checks for any kind of collision
    /// </summary>
    private void CheckForCollisions()
    {
        lock (theWorld)
        {
            foreach (Snake snake in theWorld.Players.Values)
            {
                if (snake.alive)
                {
                    if (CheckForWallCollision(snake.body.Last(), 0) || CheckForPlayerVsPlayerCollision(snake) || CheckForSelfCollision(snake))
                    {
                        snake.died = true;
                        snake.alive = false;
                        snake.GrowthRate = 0;
                    }

                    if (CheckForPowerupCollision(snake))
                    {
                        snake.score++;
                        snake.GrowthRate += Snake_Growth_Rate;
                        if (Powerup_Respawn_Delay <= 0) Powerup_Respawn_Delay = Powerup_Respawn_Timer;
                    }
                }
                else
                {
                    snake.died = false;
                    snake.RespawnRate--;
                }
            }
        }
    }

    /// <summary>
    /// Checks for a snake and powerup collision on a given frame
    /// </summary>
    /// <param name="snake">A snake present in the world</param>
    /// <returns>Boolean</returns>
    private bool CheckForPowerupCollision(Snake snake)
    {
        lock (theWorld)
        {
            foreach (Powerup power in theWorld.Powerups.Values)
            {
                if (snake.alive)
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
                        return true;
                    }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Checks for a Wall collisions given a specific vector with a given buffer. 
    /// Users of this method should pass zero as the buffer when referring to a direct collision, but should 
    /// pass a buffer of variable size if an additional boundary surrounding the wall should be counted as a collision. 
    /// </summary>
    /// <param name="obj">A vector that is being tested for collision</param>
    /// <param name="buffer">extra range that should be counted as a collision barrier</param>
    /// <returns>Boolean</returns>
    private bool CheckForWallCollision(Vector2D obj, int buffer)
    {
        lock (theWorld)
        {
            foreach (Wall wall in theWorld.Walls.Values)
            {
                double wallStartX = wall.p1.GetX() - 25 - buffer;
                double wallEndX = wall.p2.GetX() + 25 + buffer;
                double wallStartY = wall.p1.GetY() - 25 - buffer;
                double wallEndY = wall.p2.GetY() + 25 + buffer;

                double oppWallStartX = wall.p1.GetX() + 25 + buffer;
                double oppWallEndX = wall.p2.GetX() - 25 - buffer;
                double oppWallStartY = wall.p1.GetY() + 25 + buffer;
                double oppWallEndY = wall.p2.GetY() - 25 - buffer;


                if ((obj.GetX() >= wallStartX && obj.GetX() <= wallEndX &&
                     obj.GetY() >= wallStartY && obj.GetY() <= wallEndY) ||
                    (obj.GetX() <= oppWallStartX && obj.GetX() >= oppWallEndX &&
                     obj.GetY() <= oppWallStartY && obj.GetY() >= oppWallEndY))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Determines if a snake has hit another snake on a given frame
    /// </summary>
    /// <param name="snake">A snake in the world</param>
    /// <returns>Boolean</returns>
    private bool CheckForPlayerVsPlayerCollision(Snake snake)
    {
        Snake currentSnake = snake;
        lock (theWorld)
        {
            // iterate through all current snakes
            foreach (Snake otherSnake in theWorld.Players.Values)
            {
                if (!currentSnake.Equals(otherSnake) && (currentSnake.alive && otherSnake.alive))
                {
                    // head on head collision
                    Vector2D currentHeadLocation = currentSnake.body.Last();
                    int currentCollisionRadius = currentSnake.GetCollisonRadius();
                    Vector2D otherHeadLocation = otherSnake.body.Last();
                    int otherCollisionRadius = currentSnake.GetCollisonRadius();
                    double distance = Vector2D.DistanceBetweenTwoVectors(currentHeadLocation, otherHeadLocation);
                    int totalRadius = currentCollisionRadius + otherCollisionRadius;
                    // if snakes collide head on, it is decided at random who will die.
                    if (distance <= totalRadius)
                    {
                        Random dieDecider = new();
                        if (dieDecider.Next(0, 1) == 1)
                        {
                            return true;
                        }
                        else
                        {
                            otherSnake.died = true;
                            otherSnake.alive = false;
                            return true;
                        }
                    }
                    // head on body
                    for (int i = otherSnake.body.Count - 1; i > 0; i--)
                    {
                        double leftX = otherSnake.body[i].GetX() - 5;
                        double leftY = otherSnake.body[i].GetY() - 5;
                        double rightX = otherSnake.body[i - 1].GetX() + 5;
                        double rightY = otherSnake.body[i - 1].GetY() + 5;

                        double minX;
                        double maxX;
                        if (leftX < rightX)
                        {
                            minX = leftX;
                            maxX = rightX;
                        }
                        else
                        {
                            minX = rightX;
                            maxX = leftX;
                        }

                        double minY;
                        double maxY;
                        if (leftY < rightY)
                        {
                            minY = leftY;
                            maxY = rightY;
                        }
                        else
                        {
                            minY = rightY;
                            maxY = leftY;
                        }
                        if (currentSnake.body.Last().GetY() >= minY && currentSnake.body.Last().GetY() <= maxY &&
                            currentSnake.body.Last().GetX() >= minX && currentSnake.body.Last().GetX() <= maxX)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Makes sure that powerups and snake vectors dont spawn on top of other snakes 
    /// </summary>
    /// <param name="vect">A given vector</param>
    /// <returns>Boolean</returns>
    private bool CheckForSnakeBodyCollisionOnSpawn(Vector2D vect)
    {
        // head on body
        lock (theWorld)
        {
            foreach (Snake snake in theWorld.Players.Values)
            {
                for (int i = snake.body.Count - 1; i > 0; i--)
                {
                    double leftX = snake.body[i].GetX() - 5;
                    double leftY = snake.body[i].GetY() - 5;
                    double rightX = snake.body[i - 1].GetX() + 5;
                    double rightY = snake.body[i - 1].GetY() + 5;

                    double minX;
                    double maxX;
                    if (leftX < rightX)
                    {
                        minX = leftX;
                        maxX = rightX;
                    }
                    else
                    {
                        minX = rightX;
                        maxX = leftX;
                    }

                    double minY;
                    double maxY;
                    if (leftY < rightY)
                    {
                        minY = leftY;
                        maxY = rightY;
                    }
                    else
                    {
                        minY = rightY;
                        maxY = leftY;
                    }

                    if (vect.GetY() >= minY && vect.GetY() <= maxY &&
                        vect.GetX() >= minX && vect.GetX() <= maxX)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Determines if the snake has hit itself on a given frame
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private bool CheckForSelfCollision(Snake snake)
    {
        lock (theWorld)
        {
            if (snake.body.Count > 3 && snake.alive)
            {
                // head on body
                for (int i = snake.body.Count - 3; i > 0; i--)
                {
                    double leftX = snake.body[i].GetX() - 5;
                    double leftY = snake.body[i].GetY() - 5;
                    double rightX = snake.body[i - 1].GetX() + 5;
                    double rightY = snake.body[i - 1].GetY() + 5;

                    double minX;
                    double maxX;
                    if (leftX < rightX)
                    {
                        minX = leftX;
                        maxX = rightX;
                    }
                    else
                    {
                        minX = rightX;
                        maxX = leftX;
                    }

                    double minY;
                    double maxY;
                    if (leftY < rightY)
                    {
                        minY = leftY;
                        maxY = rightY;
                    }
                    else
                    {
                        minY = rightY;
                        maxY = leftY;
                    }

                    if (snake.body.Last().GetY() > minY && snake.body.Last().GetY() < maxY &&
                        snake.body.Last().GetX() > minX && snake.body.Last().GetX() < maxX)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /////////////////////////////////////////////////////////////////////////////////////
    /// RESPAWN ORIENTED METHODS
    ////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Checks to see if there are any currently dead players that need to be respawned
    /// </summary>
    private void CheckForPlayerRespawns()
    {
        lock (theWorld)
        {
            foreach (Snake snake in theWorld.Players.Values)
            {
                if ((!snake.alive) && snake.RespawnRate == 0)
                {
                    Vector2D headDirection;
                    List<Vector2D> body;
                    headDirection = new();
                    body = new();
                    bool isInvalidLocation = false;
                    do
                    {
                        // randomize the direction 
                        Random directionRandomizer = new Random();
                        int directionPicker = directionRandomizer.Next(0, 3);
                        headDirection = Directions.GetValueOrDefault(directionPicker)!;

                        // randomize the spawn location x and y 
                        Random locationRandomizer = new Random();

                        // randomize the location but make sure that the snake never spawns off a ledge, subtract its starting length to prevent
                        int Xcoord = locationRandomizer.Next((-World_Size / 2) + Snake_Starting_Length, (World_Size / 2) - Snake_Starting_Length);
                        int Ycoord = locationRandomizer.Next((-World_Size / 2) + Snake_Starting_Length, (World_Size / 2) - Snake_Starting_Length);

                        Vector2D headLocation = new Vector2D(Xcoord, Ycoord);

                        // derive the tails location based on those 2 variables
                        Vector2D tailLocation = headLocation - (headDirection * (double)Snake_Starting_Length);
                        body = new List<Vector2D>() { tailLocation, headLocation };

                        foreach (Vector2D segment in body)
                        {
                            if (CheckForSnakeBodyCollisionOnSpawn(segment))
                            {
                                isInvalidLocation = true;
                            }
                            else if (CheckForWallCollision(segment, Snake_Starting_Length))
                            {
                                isInvalidLocation = true;
                            }
                            else
                            {
                                isInvalidLocation = false;
                            }
                        }
                    } while (isInvalidLocation);

                    // resets the current player
                    snake.dir = headDirection;
                    snake.RespawnRate = Respawn_Rate;
                    snake.body = body;
                    snake.alive = true;
                    snake.died = false;
                    snake.score = 0;
                }
            }
        }
    }

    /// <summary>
    /// Consistantly maintians a XML desired amount of powerups on the board.
    /// </summary>
    private void MakeSurePowerupsAreAtMaximum()
    {
        Random rand = new Random();
        int ID = rand.Next();
        if (theWorld.Powerups.Count < Max_Powerups)
        {
            if (Powerup_Respawn_Delay == 0)
            {
                for (int i = 0; i < Max_Powerups - theWorld.Powerups.Count; i++)
                {
                    ID += 12;
                    Powerup powerup = new Powerup(ID, GeneratePowerupSpawnLocation(), false);
                    theWorld.Powerups.Add(ID, powerup);
                }
            }
            else
            {
                Powerup_Respawn_Delay--;
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
        Random rand = new();
        Vector2D location;
        bool isInvalidSpawn;
        do
        {
            int Xcoord = rand.Next(-World_Size / 2, World_Size / 2);
            int Ycoord = rand.Next(-World_Size / 2, World_Size / 2);
            location = new Vector2D(Xcoord, Ycoord);

            // see if the desired location is valid with an addtional 100 pixel buffer that 
            // makes sure powerups dont spawn close to walls           
            if (CheckForWallCollision(location, 25) || CheckForSnakeBodyCollisionOnSpawn(location))
            {
                isInvalidSpawn = true;
            }
            else
            {
                isInvalidSpawn = false;
            }
        } while (isInvalidSpawn);

        return location;
    }

    /// <summary>
    /// This is the method invoked every iteration through the frame loop. 
    /// Update the world then send it to each client
    /// Checks if players and powerups have died, then removes them from their repsective lists before sending that data to the client
    /// </summary>
    private void UpdatePlayerAndPowerupCounts()
    {
        lock (theWorld)
        {
            //send snake and powerup data to client 
            foreach (Snake snake in theWorld.Players.Values)
            {
                try
                {
                    string JsonSnake = JsonSerializer.Serialize(snake);
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
                    SendToAllClients(JsonPowerup);
                }
                catch (JsonException e)
                {
                    Console.WriteLine("Error Parsing Powerup JSON: " + e);
                }
            }

            //removes disconected snakes and died powerups
            IEnumerable<int> IDsOfSnakesToRemove = theWorld.Players.Values.Where(x => x.dc).Select(x => x.snake);
            foreach (int snakeID in IDsOfSnakesToRemove)
                theWorld.Players.Remove(snakeID);

            IEnumerable<int> IDsOfPowerupsToRemove = theWorld.Powerups.Values.Where(x => x.died).Select(x => x.power);
            foreach (var powerups in IDsOfPowerupsToRemove)
                theWorld.Powerups.Remove(powerups);
        }
    }

    //////////////////////////////////////////////////////////////////////////
    /// XML ORIENTED METHODS
    //////////////////////////////////////////////////////////////////////////

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
        string snakeSpeedTag = "//SnakeSpeed";
        string snakeStartingLengthTag = "//SnakeStartingLength";
        string snakeGrowthRateTag = "//SnakeGrowthRate";
        string powerupRespawnDelayTag = "//PowerupRespawnDelay";

        // Select the XML node using the tag name
        XmlNode respawnRateNode = xmlDoc.SelectSingleNode(respawnRateTag)!;
        XmlNode msPerFrameNode = xmlDoc.SelectSingleNode(msPerFrameTag)!;
        XmlNode universeSizeNode = xmlDoc.SelectSingleNode(universeSizeTag)!;
        XmlNode powerupCapNode = xmlDoc.SelectSingleNode(powerupCapTag)!;
        XmlNode snakeSpeedNode = xmlDoc.SelectSingleNode(snakeSpeedTag)!;
        XmlNode snakeStartingLengthNode = xmlDoc.SelectSingleNode(snakeStartingLengthTag)!;
        XmlNode snakeGrowthRateNode = xmlDoc.SelectSingleNode(snakeGrowthRateTag)!;
        XmlNode powerupRespawnDelayNode = xmlDoc.SelectSingleNode(powerupRespawnDelayTag)!;


        if (respawnRateNode != null && int.TryParse(respawnRateNode.InnerText, out int respawnRate))
        {
            Console.WriteLine($"Parsed CheckForPlayerRespawns Rate: {respawnRate}");
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
            Powerup_Respawn_Timer = powerupRespawnDelay;
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




