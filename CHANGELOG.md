#### 1.4.11:
* Fixed console error spam when an enemy has an equipment but it's not an elite aspect
#### 1.4.10:
* Overloading:
    * Now teleports to the aim reticle when used by players instead of teleporting to AI ground nodes
* Glacial:
    * Added a team color indicator
    * Visual aura growth speed reduced to make it easier to notice where the enemy crystal landed
* Added MysticsRisky2Utils dependency
* Fixed Blazing aspect causing errors in the modded environment "A Moment, Hollow"
#### 1.4.9:
* Glacial:
	* Now spawns a 60m shockwave every 10 seconds that inflicts the health-reducing debuff for 12 seconds instead of constantly stacking the debuff
		* This change should make the ability not as annoying when used by enemies and keep its usefulness when used by players
#### 1.4.8:
* Blazing:
	* Homing Delay: ~~0.4s~~ ⇒ 0.7s
* Glacial:
	* Growth Time: ~~4s~~ ⇒ 6s
* Reuploaded the mod because the previous patch didn't have any effect
#### 1.4.7:
* Fixed a bug that caused console errors whenever someone used an equipment, which also prevented Glacial elites from using their ability
#### 1.4.6:
* Fixed Malachite Urchins not despawning if Malachite aspect is used again while Urchins are alive
* Fixed wrong max Malachite Urchin calculations that are based on enemy size, causing Golems, Beetle Guards etc. to have 3 Urchins instead of one. Now all enemies have 1 Urchin slot with the exception of extremely big ones such as the Wandering Vagrant and the Solus Control Unit
#### 1.4.5:
* Blazing:
    * Damage: ~~12x200%~~ ⇒ 12x300%
    * Firing Delay: ~~0.25s~~ ⇒ 0.1s
* Glacial:
    * Max Radius: ~~30m~~ ⇒ 60m
    * Ice Crystal Health: ~~80 (+24 per level)~~ ⇒ 110 (+33 per level)
    * Ice crystals no longer have proc immunity
* Malachite:
    * Urchins On Use: ~~3~~ ⇒ 1
    * Max Urchins: ~~9~~ ⇒ 1
    * Summoned urchin health and damage stats are now equal to 3x the stats of a regular Malachite Urchin
    * Summoned urchins no longer have proc immunity
* Celestine:
    * Cooldown: ~~15s~~ ⇒ 7s
* Perfected:
    * Cooldown: ~~90s~~ ⇒ 45s
#### 1.4.4:
* Fixed ground enemies falling through terrain after using the Overloading ability
#### 1.4.3:
* Fixed flying enemies teleporting to ground nodes using the Overloading ability
#### 1.4.2:
* Fixed Ice Crystals showing up as Time Crystals for clients
* Fixed Grandparents being stuck in Overloading blink infinitely, causing a softlock
#### 1.4.1:
* Now works on game version 1.1.1.2
* Fixed a bug with Perfected ability playing the buff preparing sound twice
#### 1.4.0:
* Now works on the Anniversary Update version of the game
* Added Perfected aspect ability
#### 1.3.0:
* Elite enemies no longer use aspects immediately after spawning
* Blazing:
    * Missiles no longer spread around the target position
    * Enemy missiles now target the entity they are currently attacking, player missiles now target the closest enemy to the crosshair
* Overloading:
    * Enemies now teleport to the entity they are currently attacking instead of teleporting to the closest target
* Glacial:
    * HP Reduction Per Second: ~~4%~~ ⇒ 15%
    * Radius: ~~16m~~ ⇒ 35m
    * Health: ~~200 (+60 per level)~~ ⇒ 80 (+24 per level)
    * Crystals Per Team: ~~5~~ ⇒ 3
    * Now shoots the crystal as a mortar projectile instead of spawning the crystal immediately
    * Now starts out at 0m effective radius and grows to maximum radius over time
    * Overlapping crystal auras do not apply the debuff multiple times in one frame anymore
    * Debuff now reduces health based on current health fraction. This means that if you had 50% health before getting debuffed, your current health will always be adjusted to be 50% of your maximum health minus curse.
* Fixed Blast Shower not clearing the Glacial crystal debuff
* Fixed Glacial crystals showing up as "The Planet" when pinged
* Fixed Overloading teleportation causing errors when trying to teleport to a Glacial debuff crystal
#### 1.2.2:
* Fixed ice curse causing errors with [Rein's Sniper](https://thunderstore.io/package/Rein/Sniper/)
* Added temporary fix for [Jarlyk's EquipmentDurability](https://thunderstore.io/package/Jarlyk/EquipmentDurability/)
#### 1.2.1:
* Fixed Artifact of Enigma rerolling aspects
* Fixed Glacial crystal debuff not wearing off in multiplayer
* Fixed Malachite aspect sometimes stopping spawning urchins on use
* Fixed Malachite Urchins not being treated as minions
#### 1.2.0:
* Blazing:
    * Firing Cooldown: ~~0.175s~~ ⇒ 0.3s
    * Missile Count: ~~3~~ ⇒ 6
    * Now follow the place where you were at the time of firing instead of tracking you
* Glacial:
    * Cooldown: ~~30s~~ ⇒ 45s
    * Added collision with characters
    * Removed the 90 second self-destruction timer
    * Now has a limit of 5 crystals per team
    * Now uses a single hidden timer instead of individual timers for each debuff stack, should improve performance
    * Debuff stacks now indicate the remaining duration of the debuff
#### 1.1.0:
* Blazing:
    * Proc Coefficient: ~~0.5~~ ⇒ 0.25
    * Lock-on Time: ~~2s~~ ⇒ 1.4
* Overloading:
    * Enemies now prioritize teleporting to the closest targets
* Glacial:
    * Health reduction per second: ~~3%~~ ⇒ 5%
    * Crystals self-destruct after 90 seconds to prevent map clutter
    * On-hit and on-death procs don't trigger on crystal wards anymore
* Malachite:
    * Malachite Urchins now last only 45 seconds
    * Using multiple times in a row will create more Malachite Urchins, up to 3x the normal amount
    * On-hit and on-death procs don't trigger on summoned Malachite Urchins anymore
* Celestine:
    * Cooldown: ~~30s~~ ⇒ 15s
    * Heal Fraction: ~~50%~~ ⇒ 33%
    * Now also heals the holder for 10% of their health
    * Changed colour and removed bloom to prevent confusion with Lepton Daisy's healing nova
* When you spawn in a stage, the pre-spawned enemies wouldn't use aspects for the first 6 seconds. This delay was increased to 6-12 seconds to prevent all pre-spawned Overloading elites from teleporting to you at once.
* Fixed Blazing missiles dealing 0% damage
