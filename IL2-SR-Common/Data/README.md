# Airfield code data

`AirfieldCodes.json` maps known IL-2 Great Battles airfield-name variants to concise, stable three-letter codes for the Pilot Roster.

The table currently covers Kuban, Moscow, Normandy, Rhineland, Stalingrad, the Western Front, Velikie Luki, and Odessa. Lapino uses Moscow locations, while East 1944 and East 1945 duplicate Stalingrad and Rhineland data respectively.

The primary airfield lists are derived from the maintained [`PWCGDeveloper/PWCGCampaign`](https://github.com/PWCGDeveloper/PWCGCampaign) location data. Combat Box mission data supplies additional community-map names and operational aliases.

## Maintenance rules

- Keep every code exactly three uppercase ASCII letters.
- Codes must be unique within a map. The same physical airfield may use the same code on more than one map.
- Put alternate spellings and historical names in the same entry's `names` array.
- Name matching is case-insensitive and ignores accents, punctuation, underscores, and whitespace.
- The resolver also removes common coalition prefixes, historical field identifiers such as `B-78`, and operational suffixes such as `BSP`, `FSP`, `FWD`, and `JFB`.
- Unknown names fall back to the first three normalized letters, so newly introduced fields remain visible until the table is updated.

Server roster producers should send the full airfield name. The client performs the lookup and retains the full supplied name for its tooltip.
