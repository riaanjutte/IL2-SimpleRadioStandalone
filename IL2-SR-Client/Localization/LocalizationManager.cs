using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Localization
{
    public sealed class LanguageOption
    {
        public LanguageOption(string code, string displayName)
        {
            Code = code;
            DisplayName = displayName;
        }

        public string Code { get; }
        public string DisplayName { get; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public static class LocalizationManager
    {
        private const string DefaultLanguage = "en";
        private const string LocalizationFolder = "Localization";

        private static readonly List<LanguageOption> Languages = new List<LanguageOption>
        {
            new LanguageOption("en", "English"),
            new LanguageOption("de", "Deutsch"),
            new LanguageOption("fr", "Francais"),
            new LanguageOption("es", "Espanol")
        };

        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            {"General", "General"},
            {"Setup", "Setup"},
            {"Microphone", "Microphone"},
            {"Audio Preview", "Audio Preview"},
            {"Stop Preview", "Stop Preview"},
            {"Speakers & Optional Mic Output", "Speakers & Optional Mic Output"},
            {"Speaker Boost:", "Speaker Boost:"},
            {"Server", "Server"},
            {"Connect", "Connect"},
            {"Connecting...", "Connecting..."},
            {"Disconnect", "Disconnect"},
            {"Show Server Settings", "Show Server Settings"},
            {"Toggle Radio Overlay", "Toggle Radio Overlay"},
            {"Toggle Client List", "Toggle Client List"},
            {"Current Profile:", "Current Profile:"},
            {"Connected Clients:", "Connected Clients:"},
            {"Patreon - Support SRS", "Patreon - Support SRS"},
            {"Controls", "Controls"},
            {"Rescan Input Devices", "Rescan Input Devices"},
            {"Device", "Device"},
            {"Button", "Button"},
            {"Favourites", "Favourites"},
            {"Settings", "Settings"},
            {"Global Settings", "Global Settings"},
            {"Language", "Language"},
            {"Theme", "Theme"},
            {"Light", "Light"},
            {"Dark", "Dark"},
            {"Use Windows setting", "Use Windows setting"},
            {"Auto Connect Prompt", "Auto Connect Prompt"},
            {"Auto Connect Mismatch Prompt", "Auto Connect Mismatch Prompt"},
            {"Reset Radio Overlay", "Reset Radio Overlay"},
            {"Reset", "Reset"},
            {"Hide Overlay Taskbar Item", "Hide Overlay Taskbar Item"},
            {"Auto Start Radio Overlay", "Auto Start Radio Overlay"},
            {"Auto Refocus IL2", "Auto Refocus IL2"},
            {"Allow More Input Devices", "Allow More Input Devices"},
            {"Microphone Automatic Gain Control", "Microphone Automatic Gain Control"},
            {"Microphone Noise Suppression", "Microphone Noise Suppression"},
            {"Minimise to tray", "Minimise to tray"},
            {"Start minimised", "Start minimised"},
            {"Check for beta updates", "Check for beta updates"},
            {"Play connection sounds", "Play connection sounds"},
            {"Require Admin", "Require Admin"},
            {"Show Transmitter Name (Requires Server ON too)", "Show Transmitter Name (Requires Server ON too)"},
            {"Profile", "Profile"},
            {"Create Profile", "Create Profile"},
            {"Create", "Create"},
            {"Cancel", "Cancel"},
            {"Rename", "Rename"},
            {"Please Enter a Profile Name", "Please Enter a Profile Name"},
            {"Copy Profile", "Copy Profile"},
            {"Rename Profile", "Rename Profile"},
            {"Delete Profile", "Delete Profile"},
            {"Profile Settings", "Profile Settings"},
            {"Radio Rx Effects", "Radio Rx Effects"},
            {"Radio Tx Effects", "Radio Tx Effects"},
            {"Start", "Start"},
            {"End", "End"},
            {"Radio Switch works as Push To Talk (PTT)", "Radio Switch works as Push To Talk (PTT)"},
            {"Enable Radio Voice Effect", "Enable Radio Voice Effect"},
            {"Enable Clipping Effect (Requires Radio effects on!)", "Enable Clipping Effect (Requires Radio effects on!)"},
            {"Enable Text to Speech (beta)", "Enable Text to Speech (beta)"},
            {"Text To Speech Volume", "Text To Speech Volume"},
            {"Selected Radio Muted Volume", "Selected Radio Muted Volume"},
            {"Wrap Next Radio", "Wrap Next Radio"},
            {"Push to Talk Release Delay (ms)", "Push to Talk Release Delay (ms)"},
            {"Intercom Audio Channel", "Intercom Audio Channel"},
            {"Intercom Volume", "Intercom Volume"},
            {"First Radio Audio Channel", "First Radio Audio Channel"},
            {"Second Radio Audio Channel", "Second Radio Audio Channel"},
            {"Help", "Help"},
            {"Help & About", "Help & About"},
            {"Speaker", "Speaker"},
            {"Boost", "Boost"},
            {"Please preview your audio first", "Please preview your audio first"},
            {" using the preview option to make sure that your microphone and speakers are configured correctly.", " using the preview option to make sure that your microphone and speakers are configured correctly."},
            {"using the preview option to make sure that your microphone and speakers are configured correctly.", "using the preview option to make sure that your microphone and speakers are configured correctly."},
            {"Speaker boost can be set higher to counteract the volume reduction caused by clipping and the radio effects. If speaker boost is too high - you may have issues where other windows sounds are too loud. Reduce the boost to fix this.", "Speaker boost can be set higher to counteract the volume reduction caused by clipping and the radio effects. If speaker boost is too high - you may have issues where other windows sounds are too loud. Reduce the boost to fix this."},
            {"Troubleshooting", "Troubleshooting"},
            {"If you have any issues with the SRS please get help on the dedicated Discord channel ", "If you have any issues with the SRS please get help on the dedicated Discord channel "},
            {"If you have any issues with the SRS please get help on the dedicated Discord channel", "If you have any issues with the SRS please get help on the dedicated Discord channel"},
            {"here", "here"},
            {" and make sure to follow the information in #common-issues first before asking for help in #support", " and make sure to follow the information in #common-issues first before asking for help in #support"},
            {"and make sure to follow the information in #common-issues first before asking for help in #support", "and make sure to follow the information in #common-issues first before asking for help in #support"},
            {"Community Edition source code", "Community Edition source code"},
            {"The Community Edition source code and releases are available on ", "The Community Edition source code and releases are available on "},
            {"The Community Edition source code and releases are available on", "The Community Edition source code and releases are available on"},
            {"GitHub", "GitHub"},
            {"Input Device not detected?", "Input Device not detected?"},
            {"If your input device isn't detected - try enabling \"Allow more input devices\" and restart the client.", "If your input device isn't detected - try enabling \"Allow more input devices\" and restart the client."},
            {"If ALL devices then stop working - turn the setting off and please post you clientlog.txt on the dedicated SRS ", "If ALL devices then stop working - turn the setting off and please post you clientlog.txt on the dedicated SRS "},
            {"If ALL devices then stop working - turn the setting off and please post you clientlog.txt on the dedicated SRS", "If ALL devices then stop working - turn the setting off and please post you clientlog.txt on the dedicated SRS"},
            {"Discord Channel", "Discord Channel"},
            {" in order for me to help you add the device to the whitelist.", " in order for me to help you add the device to the whitelist."},
            {"in order for me to help you add the device to the whitelist.", "in order for me to help you add the device to the whitelist."},
            {"Control", "Control"},
            {"None", "None"},
            {"Set", "Set"},
            {"Clear", "Clear"},
            {"Left", "Left"},
            {"Equal", "Equal"},
            {"Right", "Right"},
            {"Modifier", "Modifier"},
            {"ON", "ON"},
            {"OFF", "OFF"},
            {"Show", "Show"},
            {"Quit", "Quit"},
            {"Restart Required", "Restart Required"},
            {"Please restart SRS for the language change to take effect.", "Please restart SRS for the language change to take effect."},
            {"Input Devices Rescanned", "Input Devices Rescanned"},
            {"New input devices can now be used.", "New input devices can now be used."},
            {"Invalid IP or Host Name!", "Invalid IP or Host Name!"},
            {"Host Name Error", "Host Name Error"},
            {"IL2-SRS Client", "IL2-SRS Client"},
            {"IL2-SimpleRadio", "IL2-SimpleRadio"},
            {"Il-2", "Il-2"},
            {"Connected Clients", "Connected Clients"},
            {"Add new configuration", "Add new configuration"},
            {"Name", "Name"},
            {"ServerAddress", "ServerAddress"},
            {"Remove Selected", "Remove Selected"},
            {"ServerAddress/port", "ServerAddress/port"},
            {"Is Default", "Is Default"},
            {"Server Settings", "Server Settings"},
            {"Coalition Security", "Coalition Security"},
            {"Unknown", "Unknown"},
            {"Spectator Audio", "Spectator Audio"},
            {"IRL Radio Tx Behaviour", "IRL Radio Tx Behaviour"},
            {"Show Tuned Client Count", "Show Tuned Client Count"},
            {"Show Transmitter Name", "Show Transmitter Name"},
            {"Server Version", "Server Version"},
            {"Channel Limit", "Channel Limit"},
            {"Second Radio Enabled", "Second Radio Enabled"},
            {"Close", "Close"},
            {"DISABLED", "DISABLED"},
            {"ENABLED", "ENABLED"},
            {"CREW INTERCOM", "CREW INTERCOM"},
            {"Not Connected", "Not Connected"},
            {"CHN {0}", "CHN {0}"},
            {"CHN 1", "CHN 1"},
            {"Go to saved address tab", "Go to saved address tab"},
            {"VOIP", "VOIP"},
            {"Selected Radio", "Selected Radio"},
            {"Channel 1", "Channel 1"},
            {"Channel 2", "Channel 2"},
            {"Channel 3", "Channel 3"},
            {"Channel 4", "Channel 4"},
            {"Channel 5", "Channel 5"},
            {"Window Opacity", "Window Opacity"},
            {"Select First Radio", "Select First Radio"},
            {"Select Second Radio", "Select Second Radio"},
            {"Push To Talk - PTT", "Push To Talk - PTT"},
            {"Select Intercom", "Select Intercom"},
            {"Overlay Toggle", "Overlay Toggle"},
            {"Radio Channel Up", "Radio Channel Up"},
            {"Radio Channel Down", "Radio Channel Down"},
            {"Radio Channel 1", "Radio Channel 1"},
            {"Radio Channel 2", "Radio Channel 2"},
            {"Radio Channel 3", "Radio Channel 3"},
            {"Radio Channel 4", "Radio Channel 4"},
            {"Radio Channel 5", "Radio Channel 5"},
            {"Radio Channel 6", "Radio Channel 6"},
            {"Radio Channel 7", "Radio Channel 7"},
            {"Radio Channel 8", "Radio Channel 8"},
            {"Radio Channel 9", "Radio Channel 9"},
            {"Radio Channel 10", "Radio Channel 10"},
            {"Radio Channel 11", "Radio Channel 11"},
            {"Radio Channel 12", "Radio Channel 12"},
            {"Select Next Radio / Intercom", "Select Next Radio / Intercom"},
            {"Select Previous Radio / Intercom", "Select Previous Radio / Intercom"},
            {"Read Status (TTS on required)", "Read Status (TTS on required)"},
            {"Mute / Unmute Selected Radio", "Mute / Unmute Selected Radio"},
            {"Mute / Unmute Other Radio", "Mute / Unmute Other Radio"},
            {"Mute / Unmute Both Radios", "Mute / Unmute Both Radios"},
            {"RCI", "RCI"},
            {"No RCI active", "RCI not active"},
            {"Friendly RCI active", "RCI - Friendly only"},
            {"Enemy RCI active", "RCI - Opposition only"},
            {"Both sides have RCI active", "RCI - Both coallitions"},
            {"Request callsign CHN 2", "Request callsign CHN 2"},
            {"Callsign: {0}", "Callsign: {0}"},
            {"RCO On Duty : {0}", "RCO On Duty : {0}"},
            {"RCI active", "RCI active"}
        };

        private static readonly Dictionary<string, Dictionary<string, string>> Translations =
            new Dictionary<string, Dictionary<string, string>>
            {
                {"en", English},
                {"de", new Dictionary<string, string>
                {
                    {"General", "Allgemein"},
                    {"Setup", "Einrichtung"},
                    {"Microphone", "Mikrofon"},
                    {"Audio Preview", "Audiovorschau"},
                    {"Stop Preview", "Vorschau stoppen"},
                    {"Speakers & Optional Mic Output", "Lautsprecher & optionaler Mikrofonausgang"},
                    {"Speaker Boost:", "Lautsprecher-Verstaerkung:"},
                    {"Server", "Server"},
                    {"Connect", "Verbinden"},
                    {"Connecting...", "Verbinden..."},
                    {"Disconnect", "Trennen"},
                    {"Show Server Settings", "Servereinstellungen anzeigen"},
                    {"Toggle Radio Overlay", "Funk-Overlay umschalten"},
                    {"Toggle Client List", "Clientliste umschalten"},
                    {"Current Profile:", "Aktuelles Profil:"},
                    {"Connected Clients:", "Verbundene Clients:"},
                    {"Patreon - Support SRS", "Patreon - SRS unterstuetzen"},
                    {"Controls", "Steuerung"},
                    {"Rescan Input Devices", "Eingabegeraete neu suchen"},
                    {"Device", "Geraet"},
                    {"Button", "Taste"},
                    {"Favourites", "Favoriten"},
                    {"Settings", "Einstellungen"},
                    {"Global Settings", "Globale Einstellungen"},
                    {"Language", "Sprache"},
                    {"Theme", "Design"},
                    {"Light", "Hell"},
                    {"Dark", "Dunkel"},
                    {"Use Windows setting", "Windows-Einstellung verwenden"},
                    {"Auto Connect Prompt", "Auto-Verbindungsabfrage"},
                    {"Auto Connect Mismatch Prompt", "Abfrage bei abweichender Auto-Verbindung"},
                    {"Reset Radio Overlay", "Funk-Overlay zuruecksetzen"},
                    {"Reset", "Zuruecksetzen"},
                    {"Hide Overlay Taskbar Item", "Overlay in Taskleiste ausblenden"},
                    {"Auto Start Radio Overlay", "Funk-Overlay automatisch starten"},
                    {"Auto Refocus IL2", "IL2 automatisch fokussieren"},
                    {"Allow More Input Devices", "Mehr Eingabegeraete erlauben"},
                    {"Microphone Automatic Gain Control", "Automatische Mikrofonverstaerkung"},
                    {"Microphone Noise Suppression", "Mikrofon-Rauschunterdrueckung"},
                    {"Minimise to tray", "In Infobereich minimieren"},
                    {"Start minimised", "Minimiert starten"},
                    {"Check for beta updates", "Nach Beta-Updates suchen"},
                    {"Play connection sounds", "Verbindungstoene abspielen"},
                    {"Require Admin", "Administratorrechte verlangen"},
                    {"Show Transmitter Name (Requires Server ON too)", "Sendernamen anzeigen (Server muss auch EIN sein)"},
                    {"Profile", "Profil"},
                    {"Create Profile", "Profil erstellen"},
                    {"Create", "Erstellen"},
                    {"Cancel", "Abbrechen"},
                    {"Rename", "Umbenennen"},
                    {"Please Enter a Profile Name", "Bitte Profilnamen eingeben"},
                    {"Copy Profile", "Profil kopieren"},
                    {"Rename Profile", "Profil umbenennen"},
                    {"Delete Profile", "Profil loeschen"},
                    {"Profile Settings", "Profileinstellungen"},
                    {"Radio Rx Effects", "Funk-Rx-Effekte"},
                    {"Radio Tx Effects", "Funk-Tx-Effekte"},
                    {"Start", "Start"},
                    {"End", "Ende"},
                    {"Radio Switch works as Push To Talk (PTT)", "Funkumschalter funktioniert als Push To Talk (PTT)"},
                    {"Enable Radio Voice Effect", "Funkstimmen-Effekt aktivieren"},
                    {"Enable Clipping Effect (Requires Radio effects on!)", "Clipping-Effekt aktivieren (Funk-Effekte erforderlich!)"},
                    {"Enable Text to Speech (beta)", "Text-to-Speech aktivieren (Beta)"},
                    {"Text To Speech Volume", "Text-to-Speech-Lautstaerke"},
                    {"Selected Radio Muted Volume", "Lautstaerke fuer stummes Funkgeraet"},
                    {"Wrap Next Radio", "Naechstes Funkgeraet umbrechen"},
                    {"Push to Talk Release Delay (ms)", "Push-to-Talk Loslassverzoegerung (ms)"},
                    {"Intercom Audio Channel", "Intercom-Audiokanal"},
                    {"Intercom Volume", "Intercom-Lautstaerke"},
                    {"First Radio Audio Channel", "Erster Funk-Audiokanal"},
                    {"Second Radio Audio Channel", "Zweiter Funk-Audiokanal"},
                    {"Help", "Hilfe"},
                    {"Help & About", "Hilfe & Info"},
                    {"Speaker", "Lautsprecher"},
                    {"Boost", "Verstaerkung"},
                    {"Please preview your audio first", "Bitte pruefen Sie zuerst Ihre Audiovorschau"},
                    {" using the preview option to make sure that your microphone and speakers are configured correctly.", " mit der Vorschauoption, um sicherzustellen, dass Mikrofon und Lautsprecher korrekt eingerichtet sind."},
                    {"using the preview option to make sure that your microphone and speakers are configured correctly.", "mit der Vorschauoption, um sicherzustellen, dass Mikrofon und Lautsprecher korrekt eingerichtet sind."},
                    {"Speaker boost can be set higher to counteract the volume reduction caused by clipping and the radio effects. If speaker boost is too high - you may have issues where other windows sounds are too loud. Reduce the boost to fix this.", "Die Lautsprecher-Verstaerkung kann erhoeht werden, um die Lautstaerkereduzierung durch Clipping und Funkeffekte auszugleichen. Wenn sie zu hoch ist, koennen andere Windows-Sounds zu laut sein. Reduzieren Sie die Verstaerkung, um dies zu beheben."},
                    {"Troubleshooting", "Fehlerbehebung"},
                    {"If you have any issues with the SRS please get help on the dedicated Discord channel ", "Wenn Sie Probleme mit SRS haben, holen Sie bitte Hilfe im dedizierten Discord-Kanal "},
                    {"If you have any issues with the SRS please get help on the dedicated Discord channel", "Wenn Sie Probleme mit SRS haben, holen Sie bitte Hilfe im dedizierten Discord-Kanal"},
                    {"here", "hier"},
                    {" and make sure to follow the information in #common-issues first before asking for help in #support", " und folgen Sie zuerst den Informationen in #common-issues, bevor Sie in #support um Hilfe bitten"},
                    {"and make sure to follow the information in #common-issues first before asking for help in #support", "und folgen Sie zuerst den Informationen in #common-issues, bevor Sie in #support um Hilfe bitten"},
                    {"Community Edition source code", "Community Edition Quellcode"},
                    {"The Community Edition source code and releases are available on ", "Der Quellcode und die Releases der Community Edition sind verfuegbar auf "},
                    {"The Community Edition source code and releases are available on", "Der Quellcode und die Releases der Community Edition sind verfuegbar auf"},
                    {"GitHub", "GitHub"},
                    {"Input Device not detected?", "Eingabegeraet nicht erkannt?"},
                    {"If your input device isn't detected - try enabling \"Allow more input devices\" and restart the client.", "Wenn Ihr Eingabegeraet nicht erkannt wird - aktivieren Sie \"Mehr Eingabegeraete erlauben\" und starten Sie den Client neu."},
                    {"If ALL devices then stop working - turn the setting off and please post you clientlog.txt on the dedicated SRS ", "Wenn ALLE Geraete dann nicht mehr funktionieren - deaktivieren Sie die Einstellung und posten Sie bitte Ihre clientlog.txt im dedizierten SRS "},
                    {"If ALL devices then stop working - turn the setting off and please post you clientlog.txt on the dedicated SRS", "Wenn ALLE Geraete dann nicht mehr funktionieren - deaktivieren Sie die Einstellung und posten Sie bitte Ihre clientlog.txt im dedizierten SRS"},
                    {"Discord Channel", "Discord-Kanal"},
                    {" in order for me to help you add the device to the whitelist.", " damit ich Ihnen helfen kann, das Geraet zur Whitelist hinzuzufuegen."},
                    {"in order for me to help you add the device to the whitelist.", "damit ich Ihnen helfen kann, das Geraet zur Whitelist hinzuzufuegen."},
                    {"Control", "Steuerung"},
                    {"None", "Keine"},
                    {"Set", "Setzen"},
                    {"Clear", "Loeschen"},
                    {"Left", "Links"},
                    {"Equal", "Gleich"},
                    {"Right", "Rechts"},
                    {"Modifier", "Modifikator"},
                    {"ON", "EIN"},
                    {"OFF", "AUS"},
                    {"Show", "Anzeigen"},
                    {"Quit", "Beenden"},
                    {"Restart Required", "Neustart erforderlich"},
                    {"Please restart SRS for the language change to take effect.", "Bitte starten Sie SRS neu, damit die Sprachaenderung wirksam wird."},
                    {"Input Devices Rescanned", "Eingabegeraete neu eingelesen"},
                    {"New input devices can now be used.", "Neue Eingabegeraete koennen jetzt verwendet werden."},
                    {"Invalid IP or Host Name!", "Ungueltige IP oder Hostname!"},
                    {"Host Name Error", "Hostname-Fehler"},
                    {"IL2-SRS Client", "IL2-SRS Client"},
                    {"IL2-SimpleRadio", "IL2-SimpleRadio"},
                    {"Il-2", "Il-2"},
                    {"Connected Clients", "Verbundene Clients"},
                    {"Add new configuration", "Neue Konfiguration hinzufuegen"},
                    {"Name", "Name"},
                    {"ServerAddress", "Serveradresse"},
                    {"Remove Selected", "Auswahl entfernen"},
                    {"ServerAddress/port", "Serveradresse/Port"},
                    {"Is Default", "Ist Standard"},
                    {"Server Settings", "Servereinstellungen"},
                    {"Coalition Security", "Koalitionssicherheit"},
                    {"Unknown", "Unbekannt"},
                    {"Spectator Audio", "Zuschauer-Audio"},
                    {"IRL Radio Tx Behaviour", "Reales Funk-Tx-Verhalten"},
                    {"Show Tuned Client Count", "Anzahl abgestimmter Clients anzeigen"},
                    {"Show Transmitter Name", "Sendernamen anzeigen"},
                    {"Server Version", "Serverversion"},
                    {"Channel Limit", "Kanallimit"},
                    {"Second Radio Enabled", "Zweites Funkgeraet aktiviert"},
                    {"Close", "Schliessen"},
                    {"DISABLED", "DEAKTIVIERT"},
                    {"ENABLED", "AKTIVIERT"},
                    {"CREW INTERCOM", "CREW INTERCOM"},
                    {"Not Connected", "Nicht verbunden"},
                    {"CHN {0}", "KANAL {0}"},
                    {"CHN 1", "KANAL 1"},
                    {"Go to saved address tab", "Zum Tab gespeicherte Adressen"},
                    {"VOIP", "VOIP"},
                    {"Selected Radio", "Ausgewaehltes Funkgeraet"},
                    {"Channel 1", "Kanal 1"},
                    {"Channel 2", "Kanal 2"},
                    {"Channel 3", "Kanal 3"},
                    {"Channel 4", "Kanal 4"},
                    {"Channel 5", "Kanal 5"},
                    {"Window Opacity", "Fensterdeckkraft"},
                    {"Select First Radio", "Erstes Funkgeraet waehlen"},
                    {"Select Second Radio", "Zweites Funkgeraet waehlen"},
                    {"Push To Talk - PTT", "Push To Talk - PTT"},
                    {"Select Intercom", "Intercom waehlen"},
                    {"Overlay Toggle", "Overlay umschalten"},
                    {"Radio Channel Up", "Funkkanal hoch"},
                    {"Radio Channel Down", "Funkkanal runter"},
                    {"Radio Channel 1", "Funkkanal 1"},
                    {"Radio Channel 2", "Funkkanal 2"},
                    {"Radio Channel 3", "Funkkanal 3"},
                    {"Radio Channel 4", "Funkkanal 4"},
                    {"Radio Channel 5", "Funkkanal 5"},
                    {"Radio Channel 6", "Funkkanal 6"},
                    {"Radio Channel 7", "Funkkanal 7"},
                    {"Radio Channel 8", "Funkkanal 8"},
                    {"Radio Channel 9", "Funkkanal 9"},
                    {"Radio Channel 10", "Funkkanal 10"},
                    {"Radio Channel 11", "Funkkanal 11"},
                    {"Radio Channel 12", "Funkkanal 12"},
                    {"Select Next Radio / Intercom", "Naechstes Funkgeraet / Intercom waehlen"},
                    {"Select Previous Radio / Intercom", "Vorheriges Funkgeraet / Intercom waehlen"},
                    {"Read Status (TTS on required)", "Status vorlesen (TTS erforderlich)"},
                    {"Mute / Unmute Selected Radio", "Ausgewaehltes Funkgeraet stumm / laut"},
                    {"Mute / Unmute Other Radio", "Anderes Funkgeraet stumm / laut"},
                    {"Mute / Unmute Both Radios", "Beide Funkgeraete stumm / laut"},
                    {"RCI", "RCI"},
                    {"No RCI active", "RCI nicht aktiv"},
                    {"Friendly RCI active", "RCI - Friendly only"},
                    {"Enemy RCI active", "RCI - Opposition only"},
                    {"Both sides have RCI active", "RCI - Both coallitions"},
                    {"Request callsign CHN 2", "Request callsign CHN 2"},
                    {"Callsign: {0}", "Callsign: {0}"},
                    {"RCO On Duty : {0}", "RCO On Duty : {0}"},
                    {"RCI active", "RCI aktiv"}
                }},
                {"fr", new Dictionary<string, string>
                {
                    {"General", "General"},
                    {"Setup", "Configuration"},
                    {"Microphone", "Microphone"},
                    {"Audio Preview", "Apercu audio"},
                    {"Stop Preview", "Arreter l'apercu"},
                    {"Speakers & Optional Mic Output", "Haut-parleurs et sortie micro optionnelle"},
                    {"Speaker Boost:", "Amplification haut-parleur :"},
                    {"Server", "Serveur"},
                    {"Connect", "Connexion"},
                    {"Connecting...", "Connexion..."},
                    {"Disconnect", "Deconnexion"},
                    {"Show Server Settings", "Afficher les parametres serveur"},
                    {"Toggle Radio Overlay", "Basculer l'overlay radio"},
                    {"Toggle Client List", "Basculer la liste clients"},
                    {"Current Profile:", "Profil actuel :"},
                    {"Connected Clients:", "Clients connectes :"},
                    {"Patreon - Support SRS", "Patreon - soutenir SRS"},
                    {"Controls", "Commandes"},
                    {"Rescan Input Devices", "Rescanner les peripheriques d'entree"},
                    {"Device", "Peripherique"},
                    {"Button", "Bouton"},
                    {"Favourites", "Favoris"},
                    {"Settings", "Parametres"},
                    {"Global Settings", "Parametres globaux"},
                    {"Language", "Langue"},
                    {"Theme", "Theme"},
                    {"Light", "Clair"},
                    {"Dark", "Sombre"},
                    {"Use Windows setting", "Utiliser le parametre Windows"},
                    {"Auto Connect Prompt", "Demande de connexion auto"},
                    {"Auto Connect Mismatch Prompt", "Demande si connexion auto differente"},
                    {"Reset Radio Overlay", "Reinitialiser l'overlay radio"},
                    {"Reset", "Reinitialiser"},
                    {"Hide Overlay Taskbar Item", "Masquer l'overlay dans la barre des taches"},
                    {"Auto Start Radio Overlay", "Demarrer l'overlay radio automatiquement"},
                    {"Auto Refocus IL2", "Refocaliser IL2 automatiquement"},
                    {"Allow More Input Devices", "Autoriser plus de peripheriques d'entree"},
                    {"Microphone Automatic Gain Control", "Gain automatique du microphone"},
                    {"Microphone Noise Suppression", "Reduction du bruit du microphone"},
                    {"Minimise to tray", "Reduire dans la zone de notification"},
                    {"Start minimised", "Demarrer reduit"},
                    {"Check for beta updates", "Verifier les mises a jour beta"},
                    {"Play connection sounds", "Jouer les sons de connexion"},
                    {"Require Admin", "Exiger les droits admin"},
                    {"Show Transmitter Name (Requires Server ON too)", "Afficher le nom de l'emetteur (serveur aussi sur ON)"},
                    {"Profile", "Profil"},
                    {"Create Profile", "Creer un profil"},
                    {"Create", "Creer"},
                    {"Cancel", "Annuler"},
                    {"Rename", "Renommer"},
                    {"Please Enter a Profile Name", "Veuillez saisir un nom de profil"},
                    {"Copy Profile", "Copier le profil"},
                    {"Rename Profile", "Renommer le profil"},
                    {"Delete Profile", "Supprimer le profil"},
                    {"Profile Settings", "Parametres du profil"},
                    {"Radio Rx Effects", "Effets radio Rx"},
                    {"Radio Tx Effects", "Effets radio Tx"},
                    {"Start", "Debut"},
                    {"End", "Fin"},
                    {"Radio Switch works as Push To Talk (PTT)", "Le selecteur radio sert de Push To Talk (PTT)"},
                    {"Enable Radio Voice Effect", "Activer l'effet voix radio"},
                    {"Enable Clipping Effect (Requires Radio effects on!)", "Activer l'effet de saturation (effets radio requis !)"},
                    {"Enable Text to Speech (beta)", "Activer la synthese vocale (beta)"},
                    {"Text To Speech Volume", "Volume de la synthese vocale"},
                    {"Selected Radio Muted Volume", "Volume radio coupee selectionnee"},
                    {"Wrap Next Radio", "Boucler la radio suivante"},
                    {"Push to Talk Release Delay (ms)", "Delai de relachement Push to Talk (ms)"},
                    {"Intercom Audio Channel", "Canal audio intercom"},
                    {"Intercom Volume", "Volume intercom"},
                    {"First Radio Audio Channel", "Premier canal audio radio"},
                    {"Second Radio Audio Channel", "Deuxieme canal audio radio"},
                    {"Help", "Aide"},
                    {"Help & About", "Aide et a propos"},
                    {"Speaker", "Haut-parleur"},
                    {"Boost", "Amplification"},
                    {"Please preview your audio first", "Veuillez d'abord ecouter l'apercu audio"},
                    {" using the preview option to make sure that your microphone and speakers are configured correctly.", " avec l'option d'apercu pour verifier que le microphone et les haut-parleurs sont bien configures."},
                    {"using the preview option to make sure that your microphone and speakers are configured correctly.", "avec l'option d'apercu pour verifier que le microphone et les haut-parleurs sont bien configures."},
                    {"Speaker boost can be set higher to counteract the volume reduction caused by clipping and the radio effects. If speaker boost is too high - you may have issues where other windows sounds are too loud. Reduce the boost to fix this.", "L'amplification haut-parleur peut etre augmentee pour compenser la baisse de volume causee par la saturation et les effets radio. Si elle est trop elevee, les autres sons Windows peuvent etre trop forts. Reduisez l'amplification pour corriger cela."},
                    {"Troubleshooting", "Depannage"},
                    {"If you have any issues with the SRS please get help on the dedicated Discord channel ", "Si vous avez des problemes avec SRS, demandez de l'aide sur le canal Discord dedie "},
                    {"If you have any issues with the SRS please get help on the dedicated Discord channel", "Si vous avez des problemes avec SRS, demandez de l'aide sur le canal Discord dedie"},
                    {"here", "ici"},
                    {" and make sure to follow the information in #common-issues first before asking for help in #support", " et suivez d'abord les informations dans #common-issues avant de demander de l'aide dans #support"},
                    {"and make sure to follow the information in #common-issues first before asking for help in #support", "et suivez d'abord les informations dans #common-issues avant de demander de l'aide dans #support"},
                    {"Community Edition source code", "Code source Community Edition"},
                    {"The Community Edition source code and releases are available on ", "Le code source et les versions de la Community Edition sont disponibles sur "},
                    {"The Community Edition source code and releases are available on", "Le code source et les versions de la Community Edition sont disponibles sur"},
                    {"GitHub", "GitHub"},
                    {"Input Device not detected?", "Peripherique d'entree non detecte ?"},
                    {"If your input device isn't detected - try enabling \"Allow more input devices\" and restart the client.", "Si votre peripherique d'entree n'est pas detecte, activez \"Autoriser plus de peripheriques d'entree\" et redemarrez le client."},
                    {"If ALL devices then stop working - turn the setting off and please post you clientlog.txt on the dedicated SRS ", "Si TOUS les peripheriques cessent alors de fonctionner, desactivez ce parametre et publiez votre clientlog.txt sur le SRS dedie "},
                    {"If ALL devices then stop working - turn the setting off and please post you clientlog.txt on the dedicated SRS", "Si TOUS les peripheriques cessent alors de fonctionner, desactivez ce parametre et publiez votre clientlog.txt sur le SRS dedie"},
                    {"Discord Channel", "Canal Discord"},
                    {" in order for me to help you add the device to the whitelist.", " afin que je puisse vous aider a ajouter le peripherique a la liste blanche."},
                    {"in order for me to help you add the device to the whitelist.", "afin que je puisse vous aider a ajouter le peripherique a la liste blanche."},
                    {"Control", "Commande"},
                    {"None", "Aucun"},
                    {"Set", "Definir"},
                    {"Clear", "Effacer"},
                    {"Left", "Gauche"},
                    {"Equal", "Centre"},
                    {"Right", "Droite"},
                    {"Modifier", "Modificateur"},
                    {"ON", "ON"},
                    {"OFF", "OFF"},
                    {"Show", "Afficher"},
                    {"Quit", "Quitter"},
                    {"Restart Required", "Redemarrage requis"},
                    {"Please restart SRS for the language change to take effect.", "Veuillez redemarrer SRS pour appliquer le changement de langue."},
                    {"Input Devices Rescanned", "Peripheriques d'entree rescannes"},
                    {"New input devices can now be used.", "Les nouveaux peripheriques d'entree peuvent maintenant etre utilises."},
                    {"Invalid IP or Host Name!", "IP ou nom d'hote invalide !"},
                    {"Host Name Error", "Erreur de nom d'hote"},
                    {"IL2-SRS Client", "Client IL2-SRS"},
                    {"IL2-SimpleRadio", "IL2-SimpleRadio"},
                    {"Il-2", "Il-2"},
                    {"Connected Clients", "Clients connectes"},
                    {"Add new configuration", "Ajouter une configuration"},
                    {"Name", "Nom"},
                    {"ServerAddress", "Adresse serveur"},
                    {"Remove Selected", "Supprimer la selection"},
                    {"ServerAddress/port", "Adresse serveur/port"},
                    {"Is Default", "Par defaut"},
                    {"Server Settings", "Parametres serveur"},
                    {"Coalition Security", "Securite coalition"},
                    {"Unknown", "Inconnu"},
                    {"Spectator Audio", "Audio spectateur"},
                    {"IRL Radio Tx Behaviour", "Comportement Tx radio reel"},
                    {"Show Tuned Client Count", "Afficher le nombre de clients accordes"},
                    {"Show Transmitter Name", "Afficher le nom de l'emetteur"},
                    {"Server Version", "Version serveur"},
                    {"Channel Limit", "Limite de canal"},
                    {"Second Radio Enabled", "Deuxieme radio activee"},
                    {"Close", "Fermer"},
                    {"DISABLED", "DESACTIVE"},
                    {"ENABLED", "ACTIVE"},
                    {"CREW INTERCOM", "CREW INTERCOM"},
                    {"Not Connected", "Non connecte"},
                    {"CHN {0}", "CANAL {0}"},
                    {"CHN 1", "CANAL 1"},
                    {"Go to saved address tab", "Aller a l'onglet des adresses enregistrees"},
                    {"VOIP", "VOIP"},
                    {"Selected Radio", "Radio selectionnee"},
                    {"Channel 1", "Canal 1"},
                    {"Channel 2", "Canal 2"},
                    {"Channel 3", "Canal 3"},
                    {"Channel 4", "Canal 4"},
                    {"Channel 5", "Canal 5"},
                    {"Window Opacity", "Opacite de la fenetre"},
                    {"Select First Radio", "Selectionner la premiere radio"},
                    {"Select Second Radio", "Selectionner la deuxieme radio"},
                    {"Push To Talk - PTT", "Push To Talk - PTT"},
                    {"Select Intercom", "Selectionner l'intercom"},
                    {"Overlay Toggle", "Basculer l'overlay"},
                    {"Radio Channel Up", "Canal radio suivant"},
                    {"Radio Channel Down", "Canal radio precedent"},
                    {"Radio Channel 1", "Canal radio 1"},
                    {"Radio Channel 2", "Canal radio 2"},
                    {"Radio Channel 3", "Canal radio 3"},
                    {"Radio Channel 4", "Canal radio 4"},
                    {"Radio Channel 5", "Canal radio 5"},
                    {"Radio Channel 6", "Canal radio 6"},
                    {"Radio Channel 7", "Canal radio 7"},
                    {"Radio Channel 8", "Canal radio 8"},
                    {"Radio Channel 9", "Canal radio 9"},
                    {"Radio Channel 10", "Canal radio 10"},
                    {"Radio Channel 11", "Canal radio 11"},
                    {"Radio Channel 12", "Canal radio 12"},
                    {"Select Next Radio / Intercom", "Selectionner radio suivante / intercom"},
                    {"Select Previous Radio / Intercom", "Selectionner radio precedente / intercom"},
                    {"Read Status (TTS on required)", "Lire l'etat (TTS requis)"},
                    {"Mute / Unmute Selected Radio", "Couper / retablir la radio selectionnee"},
                    {"Mute / Unmute Other Radio", "Couper / retablir l'autre radio"},
                    {"Mute / Unmute Both Radios", "Couper / retablir les deux radios"},
                    {"RCI", "RCI"},
                    {"No RCI active", "RCI inactif"},
                    {"Friendly RCI active", "RCI - Friendly only"},
                    {"Enemy RCI active", "RCI - Opposition only"},
                    {"Both sides have RCI active", "RCI - Both coallitions"},
                    {"Request callsign CHN 2", "Request callsign CHN 2"},
                    {"Callsign: {0}", "Callsign: {0}"},
                    {"RCO On Duty : {0}", "RCO On Duty : {0}"},
                    {"RCI active", "RCI actif"}
                }},
                {"es", new Dictionary<string, string>
                {
                    {"General", "General"},
                    {"Setup", "Configuracion"},
                    {"Microphone", "Microfono"},
                    {"Audio Preview", "Vista previa de audio"},
                    {"Stop Preview", "Detener vista previa"},
                    {"Speakers & Optional Mic Output", "Altavoces y salida opcional de microfono"},
                    {"Speaker Boost:", "Refuerzo de altavoz:"},
                    {"Server", "Servidor"},
                    {"Connect", "Conectar"},
                    {"Connecting...", "Conectando..."},
                    {"Disconnect", "Desconectar"},
                    {"Show Server Settings", "Mostrar ajustes del servidor"},
                    {"Toggle Radio Overlay", "Alternar superposicion de radio"},
                    {"Toggle Client List", "Alternar lista de clientes"},
                    {"Current Profile:", "Perfil actual:"},
                    {"Connected Clients:", "Clientes conectados:"},
                    {"Patreon - Support SRS", "Patreon - apoyar SRS"},
                    {"Controls", "Controles"},
                    {"Rescan Input Devices", "Volver a buscar dispositivos de entrada"},
                    {"Device", "Dispositivo"},
                    {"Button", "Boton"},
                    {"Favourites", "Favoritos"},
                    {"Settings", "Ajustes"},
                    {"Global Settings", "Ajustes globales"},
                    {"Language", "Idioma"},
                    {"Theme", "Tema"},
                    {"Light", "Claro"},
                    {"Dark", "Oscuro"},
                    {"Use Windows setting", "Usar configuracion de Windows"},
                    {"Auto Connect Prompt", "Aviso de conexion automatica"},
                    {"Auto Connect Mismatch Prompt", "Aviso si la conexion automatica no coincide"},
                    {"Reset Radio Overlay", "Restablecer superposicion de radio"},
                    {"Reset", "Restablecer"},
                    {"Hide Overlay Taskbar Item", "Ocultar superposicion en la barra de tareas"},
                    {"Auto Start Radio Overlay", "Iniciar superposicion de radio automaticamente"},
                    {"Auto Refocus IL2", "Reenfocar IL2 automaticamente"},
                    {"Allow More Input Devices", "Permitir mas dispositivos de entrada"},
                    {"Microphone Automatic Gain Control", "Control automatico de ganancia del microfono"},
                    {"Microphone Noise Suppression", "Supresion de ruido del microfono"},
                    {"Minimise to tray", "Minimizar a la bandeja"},
                    {"Start minimised", "Iniciar minimizado"},
                    {"Check for beta updates", "Buscar actualizaciones beta"},
                    {"Play connection sounds", "Reproducir sonidos de conexion"},
                    {"Require Admin", "Requerir administrador"},
                    {"Show Transmitter Name (Requires Server ON too)", "Mostrar nombre del transmisor (tambien requiere servidor ON)"},
                    {"Profile", "Perfil"},
                    {"Create Profile", "Crear perfil"},
                    {"Create", "Crear"},
                    {"Cancel", "Cancelar"},
                    {"Rename", "Renombrar"},
                    {"Please Enter a Profile Name", "Introduce un nombre de perfil"},
                    {"Copy Profile", "Copiar perfil"},
                    {"Rename Profile", "Renombrar perfil"},
                    {"Delete Profile", "Eliminar perfil"},
                    {"Profile Settings", "Ajustes de perfil"},
                    {"Radio Rx Effects", "Efectos Rx de radio"},
                    {"Radio Tx Effects", "Efectos Tx de radio"},
                    {"Start", "Inicio"},
                    {"End", "Fin"},
                    {"Radio Switch works as Push To Talk (PTT)", "El selector de radio funciona como Push To Talk (PTT)"},
                    {"Enable Radio Voice Effect", "Activar efecto de voz de radio"},
                    {"Enable Clipping Effect (Requires Radio effects on!)", "Activar efecto de recorte (requiere efectos de radio!)"},
                    {"Enable Text to Speech (beta)", "Activar texto a voz (beta)"},
                    {"Text To Speech Volume", "Volumen de texto a voz"},
                    {"Selected Radio Muted Volume", "Volumen silenciado de radio seleccionada"},
                    {"Wrap Next Radio", "Volver al inicio al avanzar radio"},
                    {"Push to Talk Release Delay (ms)", "Retardo al soltar Push to Talk (ms)"},
                    {"Intercom Audio Channel", "Canal de audio de intercomunicador"},
                    {"Intercom Volume", "Volumen de intercomunicador"},
                    {"First Radio Audio Channel", "Primer canal de audio de radio"},
                    {"Second Radio Audio Channel", "Segundo canal de audio de radio"},
                    {"Help", "Ayuda"},
                    {"Help & About", "Ayuda y acerca de"},
                    {"Speaker", "Altavoz"},
                    {"Boost", "Refuerzo"},
                    {"Please preview your audio first", "Primero prueba la vista previa de audio"},
                    {" using the preview option to make sure that your microphone and speakers are configured correctly.", " usando la vista previa para asegurarte de que el microfono y los altavoces estan bien configurados."},
                    {"using the preview option to make sure that your microphone and speakers are configured correctly.", "usando la vista previa para asegurarte de que el microfono y los altavoces estan bien configurados."},
                    {"Speaker boost can be set higher to counteract the volume reduction caused by clipping and the radio effects. If speaker boost is too high - you may have issues where other windows sounds are too loud. Reduce the boost to fix this.", "El refuerzo del altavoz puede aumentarse para compensar la reduccion de volumen causada por el recorte y los efectos de radio. Si es demasiado alto, otros sonidos de Windows pueden sonar demasiado fuertes. Reduce el refuerzo para corregirlo."},
                    {"Troubleshooting", "Solucion de problemas"},
                    {"If you have any issues with the SRS please get help on the dedicated Discord channel ", "Si tienes problemas con SRS, pide ayuda en el canal dedicado de Discord "},
                    {"If you have any issues with the SRS please get help on the dedicated Discord channel", "Si tienes problemas con SRS, pide ayuda en el canal dedicado de Discord"},
                    {"here", "aqui"},
                    {" and make sure to follow the information in #common-issues first before asking for help in #support", " y asegurate de seguir primero la informacion de #common-issues antes de pedir ayuda en #support"},
                    {"and make sure to follow the information in #common-issues first before asking for help in #support", "y asegurate de seguir primero la informacion de #common-issues antes de pedir ayuda en #support"},
                    {"Community Edition source code", "Codigo fuente de Community Edition"},
                    {"The Community Edition source code and releases are available on ", "El codigo fuente y las versiones de Community Edition estan disponibles en "},
                    {"The Community Edition source code and releases are available on", "El codigo fuente y las versiones de Community Edition estan disponibles en"},
                    {"GitHub", "GitHub"},
                    {"Input Device not detected?", "Dispositivo de entrada no detectado?"},
                    {"If your input device isn't detected - try enabling \"Allow more input devices\" and restart the client.", "Si no se detecta tu dispositivo de entrada, activa \"Permitir mas dispositivos de entrada\" y reinicia el cliente."},
                    {"If ALL devices then stop working - turn the setting off and please post you clientlog.txt on the dedicated SRS ", "Si TODOS los dispositivos dejan de funcionar, desactiva esta opcion y publica tu clientlog.txt en el SRS dedicado "},
                    {"If ALL devices then stop working - turn the setting off and please post you clientlog.txt on the dedicated SRS", "Si TODOS los dispositivos dejan de funcionar, desactiva esta opcion y publica tu clientlog.txt en el SRS dedicado"},
                    {"Discord Channel", "Canal de Discord"},
                    {" in order for me to help you add the device to the whitelist.", " para que pueda ayudarte a anadir el dispositivo a la lista blanca."},
                    {"in order for me to help you add the device to the whitelist.", "para que pueda ayudarte a anadir el dispositivo a la lista blanca."},
                    {"Control", "Control"},
                    {"None", "Ninguno"},
                    {"Set", "Asignar"},
                    {"Clear", "Borrar"},
                    {"Left", "Izquierda"},
                    {"Equal", "Centro"},
                    {"Right", "Derecha"},
                    {"Modifier", "Modificador"},
                    {"ON", "ON"},
                    {"OFF", "OFF"},
                    {"Show", "Mostrar"},
                    {"Quit", "Salir"},
                    {"Restart Required", "Reinicio requerido"},
                    {"Please restart SRS for the language change to take effect.", "Reinicia SRS para aplicar el cambio de idioma."},
                    {"Input Devices Rescanned", "Dispositivos de entrada actualizados"},
                    {"New input devices can now be used.", "Ahora se pueden usar los nuevos dispositivos de entrada."},
                    {"Invalid IP or Host Name!", "IP o nombre de host no valido!"},
                    {"Host Name Error", "Error de nombre de host"},
                    {"IL2-SRS Client", "Cliente IL2-SRS"},
                    {"IL2-SimpleRadio", "IL2-SimpleRadio"},
                    {"Il-2", "Il-2"},
                    {"Connected Clients", "Clientes conectados"},
                    {"Add new configuration", "Agregar nueva configuracion"},
                    {"Name", "Nombre"},
                    {"ServerAddress", "Direccion del servidor"},
                    {"Remove Selected", "Quitar seleccionado"},
                    {"ServerAddress/port", "Direccion/puerto del servidor"},
                    {"Is Default", "Predeterminado"},
                    {"Server Settings", "Ajustes del servidor"},
                    {"Coalition Security", "Seguridad de coalicion"},
                    {"Unknown", "Desconocido"},
                    {"Spectator Audio", "Audio de espectador"},
                    {"IRL Radio Tx Behaviour", "Comportamiento Tx de radio real"},
                    {"Show Tuned Client Count", "Mostrar cantidad de clientes sintonizados"},
                    {"Show Transmitter Name", "Mostrar nombre del transmisor"},
                    {"Server Version", "Version del servidor"},
                    {"Channel Limit", "Limite de canal"},
                    {"Second Radio Enabled", "Segunda radio activada"},
                    {"Close", "Cerrar"},
                    {"DISABLED", "DESACTIVADO"},
                    {"ENABLED", "ACTIVADO"},
                    {"CREW INTERCOM", "CREW INTERCOM"},
                    {"Not Connected", "No conectado"},
                    {"CHN {0}", "CANAL {0}"},
                    {"CHN 1", "CANAL 1"},
                    {"Go to saved address tab", "Ir a la pestana de direcciones guardadas"},
                    {"VOIP", "VOIP"},
                    {"Selected Radio", "Radio seleccionada"},
                    {"Channel 1", "Canal 1"},
                    {"Channel 2", "Canal 2"},
                    {"Channel 3", "Canal 3"},
                    {"Channel 4", "Canal 4"},
                    {"Channel 5", "Canal 5"},
                    {"Window Opacity", "Opacidad de ventana"},
                    {"Select First Radio", "Seleccionar primera radio"},
                    {"Select Second Radio", "Seleccionar segunda radio"},
                    {"Push To Talk - PTT", "Push To Talk - PTT"},
                    {"Select Intercom", "Seleccionar intercomunicador"},
                    {"Overlay Toggle", "Alternar superposicion"},
                    {"Radio Channel Up", "Subir canal de radio"},
                    {"Radio Channel Down", "Bajar canal de radio"},
                    {"Radio Channel 1", "Canal de radio 1"},
                    {"Radio Channel 2", "Canal de radio 2"},
                    {"Radio Channel 3", "Canal de radio 3"},
                    {"Radio Channel 4", "Canal de radio 4"},
                    {"Radio Channel 5", "Canal de radio 5"},
                    {"Radio Channel 6", "Canal de radio 6"},
                    {"Radio Channel 7", "Canal de radio 7"},
                    {"Radio Channel 8", "Canal de radio 8"},
                    {"Radio Channel 9", "Canal de radio 9"},
                    {"Radio Channel 10", "Canal de radio 10"},
                    {"Radio Channel 11", "Canal de radio 11"},
                    {"Radio Channel 12", "Canal de radio 12"},
                    {"Select Next Radio / Intercom", "Seleccionar radio siguiente / intercomunicador"},
                    {"Select Previous Radio / Intercom", "Seleccionar radio anterior / intercomunicador"},
                    {"Read Status (TTS on required)", "Leer estado (requiere TTS)"},
                    {"Mute / Unmute Selected Radio", "Silenciar / reactivar radio seleccionada"},
                    {"Mute / Unmute Other Radio", "Silenciar / reactivar otra radio"},
                    {"Mute / Unmute Both Radios", "Silenciar / reactivar ambas radios"},
                    {"RCI", "RCI"},
                    {"No RCI active", "RCI no activo"},
                    {"Friendly RCI active", "RCI - Friendly only"},
                    {"Enemy RCI active", "RCI - Opposition only"},
                    {"Both sides have RCI active", "RCI - Both coallitions"},
                    {"Request callsign CHN 2", "Request callsign CHN 2"},
                    {"Callsign: {0}", "Callsign: {0}"},
                    {"RCO On Duty : {0}", "RCO On Duty : {0}"},
                    {"RCI active", "RCI activo"}
                }}
            };

        public static string CurrentLanguage { get; private set; } = DefaultLanguage;

        public static IReadOnlyList<LanguageOption> SupportedLanguages => Languages;

        public static void Initialize(GlobalSettingsStore settings)
        {
            LoadExternalTranslations();

            var configuredLanguage = settings.GetClientSetting(GlobalSettingsKeys.Language).RawValue;
            if (string.IsNullOrWhiteSpace(configuredLanguage))
            {
                configuredLanguage = DetectSystemLanguage();
                settings.SetClientSetting(GlobalSettingsKeys.Language, configuredLanguage);
            }

            ApplyLanguage(configuredLanguage);
        }

        public static void ApplyLanguage(string languageCode)
        {
            CurrentLanguage = NormalizeLanguage(languageCode);

            var culture = new CultureInfo(CurrentLanguage);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }

        public static string DetectSystemLanguage()
        {
            return NormalizeLanguage(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        }

        public static string NormalizeLanguage(string languageCode)
        {
            var normalized = NormalizeLanguageCode(languageCode);
            return Translations.ContainsKey(normalized) ? normalized : DefaultLanguage;
        }

        private static string NormalizeLanguageCode(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return DefaultLanguage;
            }

            return languageCode.Trim().ToLowerInvariant().Split('-', '_')[0];
        }

        public static LanguageOption GetLanguageOption(string languageCode)
        {
            var normalized = NormalizeLanguage(languageCode);
            return Languages.First(language => language.Code == normalized);
        }

        public static string Get(string englishText)
        {
            if (string.IsNullOrEmpty(englishText))
            {
                return englishText;
            }

            string translated;
            if (TryGetTranslation(englishText, out translated))
            {
                return translated;
            }

            var text = englishText.Trim();
            if (text.Length == 0)
            {
                return englishText;
            }

            if (TryGetTranslation(text, out translated))
            {
                var leadingWhitespace = englishText.Substring(0, englishText.Length - englishText.TrimStart().Length);
                var trailingWhitespace = englishText.Substring(englishText.TrimEnd().Length);
                return leadingWhitespace + translated + trailingWhitespace;
            }

            return englishText;
        }

        public static string Format(string englishFormat, params object[] args)
        {
            return string.Format(CultureInfo.CurrentUICulture, Get(englishFormat), args);
        }

        public static void LocalizeElement(DependencyObject root)
        {
            if (root == null)
            {
                return;
            }

            var children = GetLocalizableChildren(root);
            LocalizeObject(root);

            foreach (var child in children)
            {
                LocalizeElement(child);
            }
        }

        public static void LocalizeFlowDocument(FlowDocument document)
        {
            if (document == null)
            {
                return;
            }

            foreach (var block in document.Blocks.ToList())
            {
                LocalizeTextElement(block);
            }
        }

        private static void LocalizeObject(object item)
        {
            var window = item as Window;
            if (window != null && !string.IsNullOrEmpty(window.Title))
            {
                window.Title = Get(window.Title);
            }

            var dataGrid = item as DataGrid;
            if (dataGrid != null)
            {
                foreach (var column in dataGrid.Columns)
                {
                    var columnHeader = column.Header as string;
                    if (columnHeader != null)
                    {
                        column.Header = Get(columnHeader);
                    }
                }
            }

            var headeredContentControl = item as HeaderedContentControl;
            var header = headeredContentControl?.Header as string;
            if (header != null)
            {
                headeredContentControl.Header = Get(header);
            }

            var headeredItemsControl = item as HeaderedItemsControl;
            var itemsHeader = headeredItemsControl?.Header as string;
            if (itemsHeader != null)
            {
                headeredItemsControl.Header = Get(itemsHeader);
            }

            var contentControl = item as ContentControl;
            var content = contentControl?.Content as string;
            if (content != null)
            {
                contentControl.Content = Get(content);
            }

            var textBlock = item as TextBlock;
            if (textBlock != null)
            {
                textBlock.Text = Get(textBlock.Text);
            }

            var frameworkElement = item as FrameworkElement;
            var tooltip = frameworkElement?.ToolTip as string;
            if (tooltip != null)
            {
                frameworkElement.ToolTip = Get(tooltip);
            }
        }

        private static List<DependencyObject> GetLocalizableChildren(DependencyObject root)
        {
            var children = new List<DependencyObject>();
            var seen = new HashSet<DependencyObject>();

            try
            {
                var childCount = VisualTreeHelper.GetChildrenCount(root);
                for (var i = 0; i < childCount; i++)
                {
                    var child = VisualTreeHelper.GetChild(root, i);
                    if (child != null && seen.Add(child))
                    {
                        children.Add(child);
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Some WPF objects do not expose visual children during construction.
            }

            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>().ToList())
            {
                if (seen.Add(child))
                {
                    children.Add(child);
                }
            }

            return children;
        }

        private static void LocalizeTextElement(TextElement element)
        {
            var run = element as Run;
            if (run != null)
            {
                run.Text = Get(run.Text);
            }

            var paragraph = element as Paragraph;
            if (paragraph != null)
            {
                foreach (var inline in paragraph.Inlines.ToList())
                {
                    LocalizeTextElement(inline);
                }
            }

            var section = element as Section;
            if (section != null)
            {
                foreach (var block in section.Blocks.ToList())
                {
                    LocalizeTextElement(block);
                }
            }

            var span = element as Span;
            if (span != null)
            {
                foreach (var inline in span.Inlines.ToList())
                {
                    LocalizeTextElement(inline);
                }
            }
        }

        private static bool TryGetTranslation(string englishText, out string translated)
        {
            Dictionary<string, string> language;
            if (Translations.TryGetValue(CurrentLanguage, out language) &&
                language.TryGetValue(englishText, out translated))
            {
                return true;
            }

            return English.TryGetValue(englishText, out translated);
        }

        private static void LoadExternalTranslations()
        {
            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LocalizationFolder);
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (var filePath in Directory.GetFiles(directory, "*.resx"))
            {
                try
                {
                    var languageCode = GetLanguageCodeFromFileName(filePath);

                    Dictionary<string, string> translations;
                    if (!Translations.TryGetValue(languageCode, out translations))
                    {
                        translations = new Dictionary<string, string>();
                        Translations[languageCode] = translations;
                    }

                    var loadedTranslations = 0;
                    using (var reader = new ResXResourceReader(filePath))
                    {
                        foreach (DictionaryEntry entry in reader)
                        {
                            var key = entry.Key as string;
                            var value = entry.Value as string;
                            if (!string.IsNullOrEmpty(key) && value != null)
                            {
                                translations[key] = value;
                                loadedTranslations++;
                            }
                        }
                    }

                    if (loadedTranslations > 0)
                    {
                        AddOrUpdateLanguage(languageCode, GetLanguageDisplayName(languageCode));
                    }
                }
                catch
                {
                    // Ignore malformed community translation files and keep the built-in fallback.
                }
            }
        }

        private static string GetLanguageCodeFromFileName(string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var languageName = fileName.Contains(".")
                ? fileName.Substring(fileName.LastIndexOf(".", StringComparison.Ordinal) + 1)
                : fileName;

            return NormalizeLanguageCode(languageName);
        }

        private static string GetLanguageDisplayName(string languageCode)
        {
            var existingLanguage = Languages.FirstOrDefault(language => language.Code == languageCode);
            if (existingLanguage != null)
            {
                return existingLanguage.DisplayName;
            }

            try
            {
                return CultureInfo.GetCultureInfo(languageCode).NativeName;
            }
            catch (CultureNotFoundException)
            {
                return languageCode;
            }
        }

        private static void AddOrUpdateLanguage(string languageCode, string displayName)
        {
            var existingIndex = Languages.FindIndex(language => language.Code == languageCode);
            var languageOption = new LanguageOption(languageCode, displayName);

            if (existingIndex >= 0)
            {
                Languages[existingIndex] = languageOption;
                return;
            }

            Languages.Add(languageOption);
        }
    }
}
