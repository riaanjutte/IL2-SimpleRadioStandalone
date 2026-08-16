---
title: Audio troubleshooting
description: Diagnose echo, muffled audio, missing microphone input, and choppy received voice.
---

## Echo while transmitting

Set optional microphone output to **No Mic Output / Passthrough**. Microphone passthrough deliberately sends your own microphone to an output device and can sound like an echo or sidetone.

Also check that another voice application is not monitoring the same microphone.

## Muffled received audio

SRS applies radio effects to received voice. Confirm the selected output device and speaker boost, then compare normal Windows audio through the same headset.

Use moderate speaker boost. Very high boost can make clipping and radio effects harsher and can make other Windows sounds excessively loud.

## Microphone is not detected

1. Confirm the microphone works in Windows Sound settings.
2. Select it explicitly on the General tab.
3. Use **Audio Preview**.
4. Check Windows microphone privacy permissions for desktop applications.
5. If required, enable **Allow more input devices**, restart SRS, and test again.

## Choppy or clipped received voice

The client records rolling incoming-audio diagnostics and logs potentially degraded conditions. If reconnecting does not recover the audio, use the **Restart SRS** binding or restart the client.

For recurring problems, provide `clientlog.txt` from the affected session. Note whether every channel was affected, whether all speakers sounded bad, and whether restarting SRS immediately fixed it.

## Empty transmission sounds

Hearing TX/RX clicks without intelligible voice can be caused by another player's silent or very quiet microphone, a brief packet stream, or a failed capture device. If it repeatedly comes from the same player, ask that player to run Audio Preview and check the selected microphone.
