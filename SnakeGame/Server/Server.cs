using NetworkUtil;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Text.Json;


namespace SnakeGame;

internal class Server
{
    private Dictionary<long, SocketState> clients;
    private World theWorld;
    private Random rand = new();
    

    public delegate void ServerUpdateHandler(IEnumerable<Snake> snake, IEnumerable<Powerup> powerups, IEnumerable<Wall> walls);
    public event ServerUpdateHandler ServerUpdate;

    private long msPerFrame = 24;
    private int maxPlayers = 50;
    private int maxPowerups = 50;
    private int size;

    private int PlayerID = 0;
    private string Name;
    private int nextPowID = 0;
    private int nextWallID = 0;

    static void Main(string[] args)
    {
        Server server = new Server(900);
        server.StartServer();

        Console.Read();
    }

    public Server(int s)
    {
        size = s;
        theWorld = new World(size); 
        clients = new Dictionary<long, SocketState>();
    }

    public void Run()
    {
        System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
        watch.Start();
        Console.Write(watch.ElapsedMilliseconds);
        while (true)
        {
            while (watch.ElapsedMilliseconds < msPerFrame)
            { /* empty loop body */ }
            
            watch.Restart();

            Update();

            ServerUpdate?.Invoke(theWorld.Players.Values, theWorld.Powerups.Values, theWorld.Walls.Values);
        }
    }

    private void Update()
    {
        IEnumerable<int> playersToRemove = theWorld.Players.Values.Where(x => !x.died).Select(x => x.snake);
        IEnumerable<int> powsToRemove = theWorld.Powerups.Values.Where(x => !x.died).Select(x => x.power);
        while (theWorld.Players.Count < maxPlayers)
        { 
            //Snake snake = new Snake(nextPlayerID++,"nameholder", list, direction, 0, false, true, false, true);
            //theWorld.Players.Add(snake.snake, snake);
        }

        while (theWorld.Powerups.Count < maxPowerups)
        {
            Vector2D powerup = new(-3, -2);
            Powerup p = new Powerup(nextPowID++, powerup, false);
            theWorld.Powerups.Add(p.power, p);
        }
    }

    public void StartServer()
    {
        Networking.StartServer(NewClientConnected, 11000);
        Console.WriteLine("Server is running");
        
    }

    private void NewClientConnected(SocketState state)
    {
        if (state.ErrorOccurred)
            return;

       // state.OnNetworkAction =;
        Networking.GetData(state);
        PlayerID++; //each time there is a new connection increase PlayerID by one.
    }
    //receives player name and then commands as strings -- up, down, left, right
    //once a players name is received, sends the player id and size of the world back
    private void ReceiveMessage(SocketState state)
    {
        if (state.ErrorOccurred)
        {
            RemoveClient(state.ID);
            return;
        }

        Name = state.GetData(); //receive player name from client

        //need to reply with the player ID and the size of the world (hard-coding at 900 for now)
        size = 900;
        SendMessage(state, $"{PlayerID} + \n{size}"); //once this is called, have server send state of world
    }

    //commenting out for now -- process movement commands 
    //private void ProcessMessage(SocketState state)
    //{
    //    string totalData = state.GetData();
    //    string[] parts = Regex.Split(totalData, @"(?<=[\n])");

    //    foreach (string p in parts)
    //    {
    //        if (p.Length == 0)
    //            continue;

    //        if (p[p.Length - 1] != '\n')
    //            break;

    //        Console.WriteLine("Received message from client " + state.ID + ": \"" + p.Substring(0, p.Length - 1) + "\"");

    //        state.RemoveData(0, p.Length);

    //        HashSet<long> disconnectedClients = new HashSet<long>();

    //        foreach (SocketState client in clients.Values)
    //        {
    //            if (client != null && client.ID != state.ID)
    //            {
    //                if (!Networking.Send(client.TheSocket!, "Message from client " + state.ID + ": " + p))
    //                    disconnectedClients.Add(client.ID);
    //            }
    //        }

    //        foreach (long id in disconnectedClients)
    //            RemoveClient(id);
    //    }
    //}

    private string JsonWorld()
    {
        //Snakes, powerups, and walls to JSON -- i hardcoded values just testing things out.
        Vector2D direction = new(-1, 0);
        Vector2D head = new(3, 1);
        Vector2D tail = new(1, 1); 
        List<Vector2D> bodyList = new();
        bodyList.Add(tail);
        bodyList.Add(head);

        List<JsonDocument> worldState = new List<JsonDocument>();
        foreach (var player in theWorld.Players.Values)
        {
            var snake = new
            {
                snake = PlayerID,
                name = Name,
                body = bodyList,
                dir = direction,
                score = 0,
                died = false,
                alive = true,
                dc = false,
                join = true
            };
            worldState.Add(JsonDocument.Parse(JsonSerializer.Serialize(snake)));
        }
        foreach (var wall in theWorld.Walls.Values)
        {
            Vector2D start = new(10, 0);
            Vector2D finish = new(10, 2);
           
            var walls = new
            {
                wall = 1,
                p1 = start,
                p2 = finish 
            };
            worldState.Add(JsonDocument.Parse(JsonSerializer.Serialize(walls)));
        }
        foreach (var powerup in theWorld.Powerups.Values)
        {
            Vector2D poweruppoint = new(5, 0);
           
            var powerups = new
            {
                power = 1,
                loc = poweruppoint,
                died = false
            };
            worldState.Add(JsonDocument.Parse(JsonSerializer.Serialize(powerups)));
        }
        return JsonSerializer.Serialize(worldState.Select(doc => doc.RootElement));
    }

    private void SendMessage(SocketState s, string message)
    {
        byte[] messageBytes = Encoding.UTF8.GetBytes(message);
        // Begin sending the message
        Console.WriteLine(message);
        //s.BeginSend(messageBytes, 0, messageBytes.Length, SocketFlags.None, SendCallback, s);

        //have server send state of world in json form
    }
    private void SendCallback(IAsyncResult ar)
    {
        Socket s = (Socket)ar.AsyncState!;
        // Nothing much to do here, just conclude the send operation so the socket is happy.
        s.EndSend(ar);
    }


    private void RemoveClient(long id)
    {
        Console.WriteLine("Client " + id + " disconnected");
        clients.Remove(id);
    }
}
