# Aspect Abilities
Adds on-use abilities to elite aspects.  
Can be configured to allow enemies to use aspects as well.

---
###### Blazing - fire a seeking missile (7s)
![Blazing ability preview. A Blazing Elder Lemurian firing missiles at fleeing Huntress.](https://i.imgur.com/fJBdO0S.png)

---
###### Overloading - instantly teleport to any point of the world (7s)
![Overloading ability preview. An Overloading Stone Titan standing in front of Mercenary, with a trail of blink effects behind it.](https://i.imgur.com/3nEkbBH.png)

---
###### Glacial - deploy a destructible ice crystal that locks skills of nearby enemies (45s)
![Glacial ability preview. A Glacial Wandering Vagrant floating above an ice crystal.](https://i.imgur.com/jIFvoX4.png)

---
###### Malachite - summon a Malachite Urchin that follows you and inherits your items (90s)
![Malachite ability preview. A Malachite Mini Mushrum with four Malachite Urchins above its head.](https://i.imgur.com/47oI6JS.png)

---
###### Celestine - grant temporary dodge chance to all allies inside the invisibility aura (45s)
![Celestine ability preview. A Celestine Alloy Worship Unit looking at cloaked Alloy Vulture allies, with a green buffing aura surrounding it.](https://i.imgur.com/3EbrAMr.png)

---
###### Perfected - temporarily encase yourself in Lunar Shell, preventing enemy attacks from dealing more than 10% of your maximum health as damage in a single hit (45s)
![Perfected ability preview. A Perfected Lunar Chimera golem surviving a punch from a Loader with Runald's Band.](https://i.imgur.com/vh9Jp8K.png)

---
###### Mending (DLC1) - regenerate health over a short period of time (30s)
![Mending ability preview. Mending REX with many green crosses coming out of it.](https://i.imgur.com/yBvKJ4d.png)

---
###### Voidtouched (DLC1) - reset your ability and buff cooldowns (7s)
![Voidtouched ability preview. Voidtouched Void Fiend firing two plasma missiles in a row instead of only one.](https://i.imgur.com/Aqt4xIF.png)

---
### Changelog:
#### 2.0.0:
* Now works on the 1.2.3 version of the game
* Most of the values in the mod are now configurable
* Added Risk of Options support
* Added abilities to DLC1 elite aspects
* Blazing:
	* Cooldown: ~~15s~~ ⇒ 7s
	* Missile Count: ~~12~~ ⇒ 1
	* Explosion Radius: ~~2m~~ ⇒ 10m
	* Damage (when used by players): ~~300%~~ ⇒ 700%
	* Damage (when used by enemies): ~~300%~~ ⇒ 100%
* Overloading:
	* Cooldown: ~~10s~~ ⇒ 7s
* Glacial:
	* Crystal HP: ~~110~~ ⇒ 160
	* Crystal Regen: ~~0 HP/s~~ ⇒ 5 HP/s
	* Debuff now locks skills instead of reducing maximum health
	* Crystal range now shrinks the less health it has
	* Removed max crystals per team cap
	* Crystals now self-destruct in 20 seconds
* Malachite:
	* Urchin Damage: ~~54~~ ⇒ 18
* Celestine:
	* Cooldown: ~~7s~~ ⇒ 45s
	* Now gives allies a temporary dodge chance buff instead of healing them
* Default stage requirement for enemies to use aspects:
	* Drizzle: ~~11~~ ⇒ 3
	* Rainstorm: ~~6~~ ⇒ 3
		* These changes should make the mod work more similarly to RoR1's hard elites system, where elites gained extra effects starting from stage 3 on Rainstorm & Drizzle and from stage 1 on Monsoon
* Enemies no longer use aspects by default. This feature is now manually enabled in the config
* Changed the mod GUID from `com.TheMysticSword.AspectAbilities` to `com.themysticsword.aspectabilities`
  
(Previous changelogs can be found [here](https://github.com/TheMysticSword/AspectAbilities/blob/main/CHANGELOG.md))
