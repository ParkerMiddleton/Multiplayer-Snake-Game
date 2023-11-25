namespace SnakeGame;

public partial class MainPage : ContentPage
{
    GameController gameController = new();

    public MainPage()
    {
        InitializeComponent();
        graphicsView.Invalidate();
        gameController.Error += DisplayErrorMessage; 
        gameController.UpdateArrived += OnFrame; 
        worldPanel.SetWorld(gameController.GetWorld());
        worldPanel.SetPlayerID(gameController.GetPlayerID());
    }

    private void DisplayErrorMessage(string error)
    {
        Dispatcher.Dispatch(() => DisplayAlert("Error", error, "Okay"));
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
            gameController.SetDirection("up");
        }
        else if (text == "a")
        {
            gameController.SetDirection("left");
        }
        else if (text == "s")
        {
            gameController.SetDirection("down");
        }
        else if (text == "d")
        {
            gameController.SetDirection("right");
        }
        entry.Text = "";
        gameController.SetDirection("none");
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
        string playerName = nameText.Text;
        gameController.Connect(serverText.Text, playerName);
        keyboardHack.Focus();
        connectButton.IsEnabled = false;
        serverText.IsEnabled = false;
        nameText.IsEnabled = false;

    }

    /// <summary>
    /// Use this method as an event handler for when the controller has updated the world
    /// </summary>
    public void OnFrame()
    {
        Dispatcher.Dispatch(() => graphicsView.Invalidate());
        //Always sending the current player and the state of the world to the view to be drawn.
        worldPanel.SetWorld(gameController.GetWorld());
        worldPanel.SetPlayerID(gameController.GetPlayerID()); 
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