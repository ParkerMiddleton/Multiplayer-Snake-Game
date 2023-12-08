# SnakeServer README - PS0

Author: Abbey Lasater, Parker Middleton -- byteBuddies_game
Date: 12-17-23

#########################################################################
#                                                                       #
#                                                                       #
#                       SERVER - SNAKE GAME                             #
#                                                                       #
#                                                                       #
#########################################################################

#Requirements
Use with windows OS and .NET 7.0.

##Overview
This README provides essential information about the server side of the snake game as well as its its features, usage, and important design decisions.
It also includes instructions on how to use the application.

##Table of Contents
	- Networking
	- Gameplay Mechanics
	- Settings 
	- Known Issues

## Networking
Our server wears many hats, but one of its mains focuses is making sure that players can gracefully connect and disconnect to the game world at any time.
Upon starting the server, it loads in gameplay settings via XML file then begins listening for TCP connections from the client. 
When a client tries to connect it will send the player's name. The server will then send the new player's ID, world size and all wall data. This information never 
changes so it is only necessary to send it once. The server will then continuously send movement data for every snake in the world, as well as every powerup. 
This is sent via serailized JSON that is then deserialized by the client.


How often does the server decide to do this? This its next concern. 
The server will output how many frames happen per second, and accomplish various tasks on every frame. This is done via Infinite loop and stopwatch system counting.

All the while, the server will listen for player movement commands that are sent over via JSON, this will then update the clients snake data. 

## Gameplay Mechanics 
The server accomplishes many different tasks on every frame. 

	-UpdatePlayerAndPowerupCounts: This action will make sure that disconnected snakes, as well as dead objects, are taken out of the world state. This makes sure that 
	dead snakes and powerups are not being sent to the clients, or rather they are sent on one frame to inform the client to preform a death animation. 

	-UpdateSnakeMovement: This method makes sure that all snakes stay moving in their head's direction, and makes sure that their tails are always traveling in the direction
	of the segment in front of it. This makes sure that a snake can have as many twists and turns as its length provides. 

	-CheckForCollision: This method, checks for all types of collision each time its called. It is important to note that there is a difference between a player-caused
	collision and a spawn collision. Spawn collsions are not handled here, but detailed below. A player-caused collision can only happen via player movement. This method 
	checks to see if a player has made contact with a wall, another snake, itself, or a powerup. Various things happen when these collisions happen. A snake may die, an opposing 
	snake may die (such is the case with head on head collision) or a snake could grow in length. 

	-CheckForWrapAround: The player is allows to travel from one edge of the world to another and do so seamlessly. This method checks for snake head verticies that are at the 
	edge of the world space and acts accordingly. 

	-CheckForPlayerRespawns: This method makes sure that current not-alive players are circulated back into the world with a valid location that doesnt overlap with other players or walls.

	-MakeSurePowerupsAreAtMaximum: Powerup Caps are defined in the settings file, they are consistently added into the world with a specified delay and is always at cap. This method makes sure of that.

##Settings
Our server has an external XML File that allows users to customize specific qualities of the server. Perhaps the user would want to increase starting snake size to 300 pixels? This can happen within the 
settings.xml file. The server.cs file also parses data from this file upon starting the applicaion. It can not be modified after starting the program. 

## Known Issues
- Occasionally, a snake may spawn with its body over a powerup, this doesnt matter much if the head lands on top of the powerup, as the snake will just eat it, but sometimes (mainly only with high powerup caps)
there is the change that a snake will spawn over a given powerup. 
- Wrap around isnt where it should be. We tried many different iterations of the method, but couldnt quite nail it. Im sure given more time, this would have been implemented properly in this version. 
As of now the wrap around method just teleports the entire snake to the opposite side of the map, but doesnt leave the tail at the point of teleportation. 
- Performance: Sometimes when active players in the server are greater than 10. The server will dip in FPS. Sometimes this can be extreme depending on the number of powerups, but often times it is minimal. 
the lowest I personally have seen it go is 24. 