---
title: Radio behavior
description: Configure coalition security, channels, realistic transmission, collisions, and priority transmitters.
---

Server-controlled radio features are advertised to connected clients. Current clients apply supported changes immediately; older clients can continue basic voice communication but may ignore newer display or audio-effect flags.

## Coalition and spectator access

Enable **Secure Coalition Radios** to separate normal radio traffic by the coalition reported by IL-2:

```ini
[General Settings]
COALITION_AUDIO_SECURITY=true
```

When enabled, blue and red users on the same channel do not share a radio domain. Collision detection follows the same separation.

Enable **Spectator Audio Disabled** when spectators should not participate in normal coalition radio traffic:

```ini
SPECTATORS_AUDIO_DISABLED=true
```

Test these settings with at least one blue, red, and spectator client before opening a public event. Coalition security depends on each client receiving valid IL-2 telemetry.

## Global lobby frequencies

`GLOBAL_LOBBY_FREQUENCIES` accepts comma-separated AM frequencies in MHz:

```ini
GLOBAL_LOBBY_FREQUENCIES=248.22,249.50
```

These frequencies intentionally reach connected users outside normal coalition and tuning restrictions. Do not use them for coalition-private traffic.

## Realistic TX behavior

With `IRL_RADIO_TX=true`, a client transmitting on an AM or FM radio cannot receive another transmission on that same selected radio at the same time. The client's other radio remains available.

This is a half-duplex operating rule, not a collision sound effect. Use [RX collision effects](#rx-collision-effects) when overlapping transmitters should interfere with each other.

## Radio count and channel limit

`SECOND_RADIO_ENABLED=false` removes Radio 2 support advertised by the server. `CHANNEL_LIMIT` controls the highest selectable channel; the server UI offers 5, 10, 15, 20, or 25.

Current clients visually disable buttons above the server limit. Older clients may still draw all buttons, so coordinate client versions before an event that depends on the restriction.

## Custom channel names

Select **EDIT** beside **Channel Names**, or add entries under `[Channel Names]`:

```ini
[Channel Names]
1=Command
2=Tower/ATC
6=Strike Package
```

Names are available for all 25 channels and do not change radio frequencies. Current clients use them in the overlay, tooltips, and spoken channel announcements.

Channel 1 has one dynamic exception: while a friendly RCI is on duty, it displays `RCI Radar Control`. The configured name is restored when the RCI becomes inactive.

## Majority squad labels

Enable **Squad Channel Labels** to append a detected squad tag to eligible channel names:

```ini
[General Settings]
SHOW_SQUAD_CHANNEL_LABELS=true
```

A tag appears only when:

- the channel is above channel 2;
- at least two friendly pilots are tuned to it; and
- one recognized squad holds a strict majority.

Ties, solo pilots, and channels 1 and 2 retain their normal names. A custom server name remains the base label, so `Strike Package` can become `Strike Package - TBAS` without losing the configured purpose.

## RX collision effects

Enable collision effects when simultaneous transmissions on the same channel should interfere instead of cleanly mixing:

```ini
[General Settings]
RADIO_COLLISION_EFFECTS=true
```

The server detects overlapping transmissions from different senders on the same frequency and modulation within a short activity window. It marks the affected packets, and supporting clients generate the garbled/interference effect locally. Intercom and invalid or disabled radio frequencies are excluded.

:::note[Mixed client versions]
The server remains compatible with older clients, but an older client may ignore the collision marker and continue mixing overlapping audio cleanly.
:::

## Priority transmitters

Priority protection is active only when RX collision effects are enabled. Configure exact SRS pilot names as a comma-separated list:

```ini
RADIO_COLLISION_EFFECTS=true
PRIORITY_TRANSMITTER_NAMES=Axis Command,Allies Command,Axis Airfield,Allies Airfield
```

Matching is trimmed and case-insensitive, but otherwise exact. Restart the server after changing the list.

When a priority transmitter overlaps an ordinary transmitter on the same channel:

- the priority audio remains clear;
- the ordinary overlapping packets are blocked by the server;
- protection remains inside the applicable coalition domain when coalition security is enabled.

Two priority transmitters are not prioritized against each other and can still overlap. Use unique, controlled bot or service-account names so ordinary users cannot accidentally receive priority treatment.

## Deployment checklist

1. Decide whether coalition and spectator restrictions are required.
2. Set the channel limit and names before publishing the radio plan.
3. Test both radios with current clients.
4. If enabling collision effects, test ordinary-versus-ordinary and priority-versus-ordinary overlap.
5. Verify spectator, neutral, red, and blue behavior where applicable.
6. Keep a copy of the working `server.cfg` before changing event settings.
