---
title: Game compatibility
description: Use one SRS installation with IL-2 Great Battles and IL-2 Korea.
---

SRS for IL-2 Community Edition supports both **IL-2 Great Battles** and **IL-2 Korea** from one installation.

| Capability | Great Battles | Korea |
| --- | --- | --- |
| Game detection | Supported | Supported |
| Telemetry configuration and repair | Supported | Supported |
| Server auto-connect | Supported | Supported |
| Radio and intercom telemetry | Supported | Supported |
| Radio overlay and focus handling | Supported | Supported |
| Telemetry diagnostics | Supported | Supported |

## One installation

Do not install a separate SRS copy for each game. Install SRS once in the recommended application folder:

```text
C:\Program Files\IL2-SimpleRadio-Standalone
```

The installer and client detect each installed game independently and check the corresponding `startup.cfg`. User settings, profiles, favourites, and key bindings are shared from:

```text
%AppData%\IL2-SRS
```

## Standalone and Steam installations

Detection is based on known installation records, saved installer paths, and running game processes. If a game installation is not detected, use **Help → Telemetry Diagnostics** and include its results in a support request.

## Client and server compatibility

Release notes identify features that need an updated server. Normal voice communication remains compatible across the current Community Edition release family, but server-controlled features such as Pilot Roster data or experimental collision effects require server support.
