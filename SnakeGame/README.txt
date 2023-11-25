# SnakeGame README

Author: Abbey Lasater, Parker Middleton -- byteBuddies_game
Date: 11-24-2023
PS8 -- Snake Game

## Overview

This README provides essential information about the SnakeGame project, its features, usage, and important design decisions.
It also includes instructions on how to use the application.

## Table of Contents

- Special Features
- General Usage
- Help Resources
- Design Decisions/ Code Resources/ Implementation Notes

## Special Feature

A unique feature of this SnakeGame application is its laser/neon theme. Rather than trailing through grass, the snake goes on adventures
through neon terrain, eating powerup blobs to grow in size and avoiding neon colored tiles. The snakes are also assigned a
random and unique color theme and users can customize their snakes name.

## General Usage and Help Resources

General information about how to use the SnakeGame and some functions the SnakeGame has.

The goal of the game is to navigate the snake to the powerups which, when eaten, increase the lenght of the snake. And to avoid
the colored tiles which, when hit, kill the snake -- indicated by a red circle.

1. Player Controls: You can navigate the snake by utilizing the following keys -- 'w', 'a', 's', and 'd'.
    - To go up, press 'w'.
    - To go left, press 'a'.
    - To go down, press 's'.
    - To go right, press 'd'.


2. Help: To receive a reminder on where the player controls are found and what they do, press the "Help" button at the top
of the screen, a tooltip with information on player controls will be displayed.

3. About: To find information on some of the design and code contributions, click the "About" button at the top of the screen. A
tooltip with information about contribution will be displayed.

4. Server: To enter a specific server that you would like to play this game on, locate the "server" input box at the top of the
screen and type in the name of the server you would like to play on.

5. Player Name: To enter a specific name you would like your snake to be, locate the "name" input box at the top of the screen and
type in the player name you would like others to see you as. If not input is made, a default name "player" will be assigned.

## Design Decisions/ Code Resources/ Implementation Notes

Design Decisions:
-UI Design: A unique laser theme was opted for as our final UI design. The background is full of colorful lasers, the powerups and
tiles are neon colored, and the snakes colors go hand in hand with this as well.
The snake was customized to have an eye, and to have an alternating color theme.

-MVC Design: A number of smaller classes were developed to help model game objects. The model classes include snake.cs, wall.cs, powerup.cs
and world.cs. These classes are essential for parsing incoming JSON from the server, which acts as the model for this project. 
The controller functions with one class: GameController.cs. This class talks directly to the server using our Networking API.
The view is encapsulated by several projects but mainly takes advantage of the worldPanel object. This class draws every frame that is 
recieved by the server. 

Code Resources:
This Application utilizes .NET 07 and .NET MAUI framework. 

Implementation Notes:
- We utilized Microsoft's .NET MAUI documentation to help develop the GUI, along with using class notes and example code.
All of which helped us create a user-friendly interface for the SnakeGame application.
- Error Handling: When thread issues, circular dependencies, or network connection exceptions are detected a popup is displayed
explaining the error. 

Known Bugs: 
-Upon losing connection to the game via server being closed/shut off. The user will get a message about the disconnection and state that 
the user must press "any key" to be able to reconnect. This is because the enable tags are tied to the keyboard movement methods and connection methods
the xmal.cs file. We have tried to reset these commands whenever the DisplayErrorMessage method is called via event, however since the thread doesnt have 
access to the same parameters as the "ConnectClicked" and "OnTextChanged" methods, the compiler throws an error. 
To contribute to this problem, the dispatcher makes it really hard to use the 4th overload for DisplayAlert(), since the compiler wont allow for 
any variables to be created in their lambda call. So, taking in a "bool answer" for a DisplayAlert call, doesnt work without disabling the dispatcher. 
This violates the program requirements of being able to ask the user if they want to reconnect after an issue, it does still work, but only with the 
workaround described above.






