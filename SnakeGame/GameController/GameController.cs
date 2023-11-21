using NetworkUtil;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SnakeGame
{
    /// <summary>
    /// The GameController is responsible for parsing information received from the NetworkController (USE HERE), 
    /// and updating the model. After updating the model, it should then inform (or invoke) the view that the world 
    /// has changed, so that it can redraw. It should happen via event. 
    /// 
    /// The GameController should also define methods that the View can invoke whenever certain user inputs occur,
    /// then decide which messages to send to the server, requesting that the snake turns. 
    /// </summary>
    /// 

    public class GameController
    {
        // for sending moving commands to the server. 
        [JsonInclude]
        string? moving;

        //represents world status
        private World theWorld = new World(1);

        //represents current playerID
        private int PlayerID = -1;

        //Represents server connection 
        SocketState? theServer = null;

        /// <summary>
        /// a delegate and event combo for talking back to the view and sending data to the server and model
        /// Note: parameters for the delegates here only are used to send data back to the view
        /// </summary>
        public delegate void ConnectedHandler();
        public event ConnectedHandler? Connected;

        /// <summary>
        /// Delegate and event combo for notifying the View of an error
        /// Note: Error message will be sent back to the View in this case
        /// </summary>
        /// <param name="error"></param>
        public delegate void ErrorHandler(string error);
        public event ErrorHandler? Error;

        /// <summary>
        /// Note: So far this is being used for the test purposes JSON window. Im not sure that we'll need this event 
        /// and delegate combo in the future. 
        /// </summary>
        /// <param name="messages"></param>
        public delegate void MessageHandler(IEnumerable<string> messages);
        public event MessageHandler? MessagesArrived;

        /// <summary>
        /// A delegate and event to fire when the controller
        /// has received and processed new info from the server
        /// </summary>
        public delegate void GameUpdateHandler();
        public event GameUpdateHandler? UpdateArrived;

        /// <summary>
        /// Starts the process for connecting to the server. 
        /// </summary>
        /// <param name="ServerText"></param>
        public void Connect(string ServerText, string nameText)
        {


            Networking.ConnectToServer(OnConnect, ServerText, 11000);
            MessageEntered(nameText);

        }

        /// <summary>
        /// Method to be invoked by the networking library when a connection is made
        /// </summary>
        /// <param name="state"></param>
        private void OnConnect(SocketState state)
        {
            if (state.ErrorOccurred)
            {
                // inform the view
                Error?.Invoke("Error connecting to server");
                return;
            }

            theServer = state;

            // Start an event loop to receive messages from the server
            state.OnNetworkAction = ReceiveMessage;
            Networking.GetData(state);
        }

        /// <summary>
        /// Method to be invoked by the networking library when 
        /// data is available
        /// </summary>
        /// <param name="state"></param>
        private void ReceiveMessage(SocketState state)
        {
            if (state.ErrorOccurred)
            {
                // inform the view
                Error?.Invoke("Lost connection to server");
                return;
            }
            ProcessMessages(state);

            // Continue the event loop
            // state.OnNetworkAction has not been changed, 
            // so this same method (ReceiveMessage) 
            // will be invoked when more data arrives
            Networking.GetData(state);
        }

        /// <summary>
        /// Process any buffered messages separated by '\n'
        /// Then inform the view
        /// </summary>
        /// <param name="state"></param>
        private void ProcessMessages(SocketState state)
        {
            string totalData = state.GetData();
            string[] parts = Regex.Split(totalData, @"(?<=[\n])");

            // Loop until we have processed all messages.
            // We may have received more than one.

            List<string> newMessages = new List<string>();

            foreach (string p in parts)
            {
                // Ignore empty strings added by the regex splitter
                if (p.Length == 0)
                    continue;
                // The regex splitter will include the last string even if it doesn't end with a '\n',
                // So we need to ignore it if this happens. 
                if (p[p.Length - 1] != '\n')
                    break;
                if (int.TryParse(p, out int numberNotJSON))
                {
                    if (PlayerID == -1)
                    {
                        PlayerID = numberNotJSON;
                    }
                    else
                    {
                        theWorld = new World(numberNotJSON);
                    }
                }
                else
                {
                    JsonUpdater(p);
                }

                // build a list of messages to send to the view
                newMessages.Add(p);

                // Then remove it from the SocketState's growable buffer
                state.RemoveData(0, p.Length);
            }


            // inform the view
            MessagesArrived?.Invoke(newMessages); // leave this for now for testing

        }

        /// <summary>
        /// Closes the connection with the server
        /// </summary>
        public void Close()
        {
            theServer?.TheSocket.Close();
        }

        /// <summary>
        /// Send a message to the server
        /// </summary>
        /// <param name="message"></param>
        public void MessageEntered(string message)
        {
            if (theServer is not null)
                Networking.Send(theServer.TheSocket, message + "\n");
        }

        /// <summary>
        /// Extracts JSON and turns data into objects that are then added to the world.
        /// </summary>
        /// <param name="state"></param>
        private void JsonUpdater(string data)
        {
            try
            {

                JsonDocument.Parse(data);
                lock (theWorld)
                {

                    if (data.Contains("snake"))
                    {

                        Snake? snake = JsonSerializer.Deserialize<Snake>(data);
                        if (!theWorld.Players.ContainsKey(snake!.snake))
                        {
                            theWorld!.Players.Add(snake!.snake, snake);
                        }
                        else
                        {
                            theWorld.Players[snake!.snake] = snake;
                        }
                    }
                    else if (data.Contains("wall"))
                    {

                        Wall? wall = JsonSerializer.Deserialize<Wall>(data);
                        theWorld!.Walls.Add(wall!.wall, wall);
                    }
                    else if (data.Contains("power"))
                    {

                        Powerup? powerup = JsonSerializer.Deserialize<Powerup>(data);
                        if (!theWorld.Powerups.ContainsKey(powerup!.power))
                        {
                            theWorld!.Powerups.Add(powerup!.power, powerup);
                        }
                        else
                        {
                            theWorld!.Powerups[powerup!.power] = powerup;
                        }

                    }
                    //if the client has recieved the information about world size and playerID
                    if (theWorld.Players.Count > 0 && theWorld.size > 0)
                    {
                        UpdateArrived?.Invoke();
                    }

                }
            }
            catch (JsonException)
            {
                //TODO: Something to tell the client that the JSON wasnt caught, just for testing purposes.
            }
        }

        /// <summary>
        /// Getter for the World object. 
        /// </summary>
        /// <returns>World State</returns>
        public World GetWorld()
        {
            return theWorld!;
        }

        /// <summary>
        /// Getter for current Player ID as sent by the server. 
        /// </summary>
        /// <returns></returns>
        public int GetPlayerID()
        {
            return PlayerID;
        }

        // Needs to be of the form: {"moving":"left"}
        public void SetDirection(string direction)
        {

            MessageEntered("{\"moving\":\"" + direction + "\"}");

        }


    }
}