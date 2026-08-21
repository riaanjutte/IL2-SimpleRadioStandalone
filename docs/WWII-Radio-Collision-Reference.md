# WWII AM Radio Collision Reference

## Historical behavior

WWII aircraft voice radios were generally half-duplex AM sets. Pressing push-to-talk switched the set from receive to transmit, so a transmitting pilot normally could not hear incoming radio traffic until releasing the switch.

When two stations transmitted on the same channel at the same time:

- Non-transmitting pilots tuned to that channel received both signals.
- Similar-strength signals could produce overlapping speech, garbling, distortion, and a whistle or growl.
- The tone resulted from the small frequency difference between the two AM carrier signals.
- A much stronger signal could dominate, although AM did not provide the clean capture effect associated with FM.
- The transmitting pilots generally did not hear the collision through their radios while holding push-to-talk.

The physical collision existed wherever the overlapping radio signals reached a receiver. The audible effect therefore belonged primarily to listeners, not to the transmitting pilots.

## Recommended SRS model

1. Detect two or more incoming transmissions that a client can receive on the same channel.
2. Apply the collision effect to the overlapping incoming audio for that listening client.
3. Do not trigger a local collision effect merely because the local pilot is transmitting.
4. When realistic half-duplex behavior is enabled, mute incoming radio audio while the local pilot holds push-to-talk.
5. Continue treating the local pilot's transmission as a collision participant for other receiving clients.
6. Exclude intercom traffic because it does not use the shared radio channel.
7. Keep signal-strength behavior separate until radio attenuation is implemented. Without attenuation, use a neutral same-strength collision model.

## Sources

- [Instruction Book (Operating) for Radio Set SCR-522-A](https://radionerds.com/images/c/c6/SCR-522_Instruction_Book.pdf) - describes normal receive operation, pressing push-to-transmit to transmit, and releasing it to receive again.
- [NASA: Reducing Interference in ATC Voice Communication](https://ntrs.nasa.gov/citations/20090020584) - explains that simultaneous AM transmissions produce combined speech and an audio heterodyne caused by differences between carrier frequencies.
