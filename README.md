# AdminMenu

Implementation of a AdminMenu plugin for CS2 using CounterStrikeSharp  
<https://docs.cssharp.dev/>  
  
This plugin allowes to the admins:
- ban (with time or permanent)
- kick
- kill
- respawn
- rename
- set team for a player (and respawn if you need)
- drop weapon
- change map (RockTheVote addon or its maplist.txt file needed)
- bot add, kick
- set admin with level

There are 3 level for admins, lower can't use action on higher admins.

---
# requirements:  
- min. CounterStrikeSharp API Version: 1.0.318

---
# installation:  
Extract the folder to the `...\csgo\addons\counterstrikesharp\plugins\AdminMenu\` directory of the dedicated server.
- Uses ..\csgo\addons\counterstrikesharp\configs\admins.json and ..\csgo\addons\counterstrikesharp\configs\banned.json files, these saves the admin and banned players. See example files in the solution.
- For changemap it uses the maplist.txt file, from the RockTheVote addon (https://github.com/abnerfs/cs2-rockthevote)
