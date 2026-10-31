---
title: RCI Controller mode
description: Use SRS as a Combat Box Radar Controlled Intercept (RCI) officer without running IL-2.
---

RCI Controller mode lets a Combat Box RCI officer talk to pilots on the Combat Box SRS server without IL-2 running. It is for officers listed on the Combat Box RCI dashboard; pilots do not need it.

:::note[Not available yet]
RCI Controller mode becomes available once Combat Box starts issuing access codes. Until then, SRS hides the **RCI Controller** settings.
:::

While the mode is on:

- SRS uses your RCI codename. Pilots see you as `CODENAME___RCI`, and their clients show a friendly or enemy RCI as on duty.
- Your coalition comes from your access code, not from IL-2.
- SRS stays on the Combat Box server (`srs.combatbox.net:6002`). The server box and favourites are locked, and IL-2 cannot switch you to another server.
- You don't see the "request a callsign" prompt, and you don't count yourself as a friendly RCI.

## Getting an access code

Each officer has a personal access code, issued by the Combat Box RCI dashboard. It contains your codename, your coalition and an expiry date. A code only works in SRS until it expires. To change side, switch coalition on the dashboard and use the new code it gives you.

Access codes are signed by the dashboard. SRS can check them, but cannot create them.

## Turning the mode on

1. Open **Settings > RCI Controller**.
2. Paste your access code into **Access code** and click **Use code**. This also switches **RCI Controller mode** on.
3. The status line shows who you are on air as, your coalition, and when the code expires.
4. The first time, SRS offers to create an **RCI Controller** profile. It copies your current key bindings and puts Radio 1 in your left ear and Radio 2 in your right.
5. Connect as usual.

Turn **RCI Controller mode** off to fly as a pilot again. Your pilot name and favourite servers come back unchanged.

## When a code expires

SRS checks your code when it starts, when you change it, and each time you connect. An expired code does not disconnect you during a session, but you need a new code before you can connect again. If SRS starts with an expired code, it warns you and starts with the mode off.
