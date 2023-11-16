using NetworkUtil;
using System.Numerics;
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
    public class GameController
    {
        [JsonInclude]
        string? moving;

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
        /// 
        /// </summary>
        /// <param name="messages"></param>
        public delegate void MessageHandler(IEnumerable<string> messages);
        public event MessageHandler? MessagesArrived;

        // A delegate and event to fire when the controller
        // has received and processed new info from the server
        public delegate void GameUpdateHandler();
        public event GameUpdateHandler? UpdateArrived; // some method that updates theWorld object with new stuff from the server. 


        /// <summary>
        /// represents the state of the server connection
        /// </summary>
        SocketState? theServer = null;


        /// <summary>
        /// Starts the process for connecting to the server. 
        /// </summary>
        /// <param name="ServerText"></param>
        public void Connect(string ServerText)
        {
            Networking.ConnectToServer(OnConnect, ServerText, 11000);
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

            // inform the view
            Connected?.Invoke();

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

                // build a list of messages to send to the view
                newMessages.Add(p);

                // Then remove it from the SocketState's growable buffer
                state.RemoveData(0, p.Length);
            }


            // parse JSON from server, 
            // send new world state
            // inform the view
            MessagesArrived?.Invoke(newMessages);

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




    }
}