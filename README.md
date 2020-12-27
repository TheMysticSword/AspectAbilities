# Aspect Abilities
Adds on-use effects to elite aspects. Enemies can use them starting from stage 11 on Drizzle, stage 6 on Rainstorm and stage 1 on Monsoon.

---
###### Blazing - release a barrage of seeking missiles (15s)
![](https://i.imgur.com/fJBdO0S.png)

---
###### Overloading - instantly teleport to any point of the world (15s)
![](https://i.imgur.com/3nEkbBH.png)

---
###### Glacial - deploy a destructible debuff ward that reduces the maximum health of every enemy inside it (30s)
![](https://i.imgur.com/jIFvoX4.png)

---
###### Malachite - summon Malachite Urchins that follow you and inherit your items (90s)
![](https://i.imgur.com/47oI6JS.png)

---
###### Celestine - heal all ellies inside the invisibility aura (15s)
![](https://i.imgur.com/3EbrAMr.png)

---
### Changelog:
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