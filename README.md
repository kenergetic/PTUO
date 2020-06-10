# Pirate TU Optimizer

Third party game live simulator / guild and build management


# Description

This is a frankenstein's monster project that started as a guild inventory manager and organizer for the Tyrant Unleashed Optimizer application, and mutated over the years into a hundred other things.

To refactor it is to invite non-euclidian horrors onto the mortal plane. But if you're mucking around with the TU API, there are bits of code you might find useful!


# Table of.. okay so what is this and what happened

This started around 2016. I got tricked into playing a mobile game called Tyrant Unleashed by some kid on an airplane. That was unfortunate. 

In any case, the mobile game is a relatively simple and elegant card game, with a lot of shady p2w stuff attached. You collect cards, build a deck of up to 10 cards, and can play opponents at any time (offline, their defense decks are set). Once you play a card from your hand of 3 cards, it acts on its own. So besides building out your decks, you have ~8 decisions per game and the games are very fast and don't require waiting. 

There's a lot of metagaming around deck building, so someone made a CLI simulator for 'climbing' an optimized deck around a player's inventory of cards. Depending on what gauntlets you set upon it, it would build out a deck. Since the majority of guildmates did not want to install this simulator, guild officers had to chase players down for inventories, and try to sim. That's where the first iteration of this game in


# The first iteration

My friend, who shall remain nameless but is a jerk, kept player inventories on a google sheet. So the start of this application connected with that.. database, and pulled inventories, and had mechanisms to create a 'batch' of sims to chew through a bunch of player's sims at the same time. We also kept gauntlets up there, and he had some fancy code that let you look at a player's salvaged (destroyed) cards, because the developers let players reclaim these cards and periodically buffed old cards. 



# The second iteration

The developers exposed their API at some point, and through a player's hashed login, some pretty interesting API calls could come in. An interesting piece of data that came in that became dangerous was during a match, the order of your deck was returned with the API. Meaning, if you could show this to a player who invokes that, they'd have a slight edge in a match. 

That slight edge turned into something else. 



# The third iteration

Another developer and I figured out how to hijack the simulator tool to basically project what card to play mid-game. With the player's card order, and locking in which cards were played from the player and opponent's deck, one could project out what the best play was in X trials. It was an immense boost in winrate. The downside was it took a match in its vacuum, so any random effects from the actual match were not perfectly copied on each trial. However, it played better then basically any human in our guild, and made me feel bad because it proved in a court of law that I was not a great player at this game. We called this Rum Barrel, because we were a pirate guild and pirates do things better with rum, I think.


# Future iterations

Added other guild management tools, reporting (like tattling on who's been offline a week), and that's the state of the application so far. 

A bunch of fairly complex stuff from the API was integrated in - automating chore grinding, automating card building and gold buying (it was extremely painful to do in game), and creating a dynamic system that would read in card data and assign them 'power'. I used this power to filter out cards from inventories to speed up simulations, and to create 'seed decks' for player inventories based on several themes. (e.g. the card's faction, a specific strategy of card abilities, all fast cards, etc). This is because while the simulator climbed well, it could not climb out of a bad starting deck. 

The developers stopped developing for the game but allowed a player community to add cards with existing art - so I coded in ways for a pretty casual user to add 'custom cards' to the simulator and test out how new cards would play. 

To boost the winrate of Rum Barrel, I also started pulling and saving enemy decks (or what cards were visible). I also pulled inventories from other sources, including guild spies, who could examine defense decks through the API. There were some spy games going on in the meta, and for fighting rival guilds, we were fine exposing that data to each other. 


My main regret is this is a Winform, but I could not for the life of me figure out how to call a windows executable that needed filepaths in anything else that was user friendly. 

