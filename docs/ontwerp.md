# Spike: ontwerp

Spike is een gratis open source waakhond voor Windows. Zodra iemand de computer probeert over te nemen met AnyDesk, TeamViewer, Snelle hulp of een ander meekijkprogramma, blaft Spike. Er verschijnt dan een groot scherm met de naam en het telefoonnummer van iemand die de gebruiker vertrouwt.

Bijgewerkt: 13 november 2026. Achtergrond en bronnen: `Documents\Tremors onderzoek\reports\Waakhond tegen helpdeskfraude.md`.

## 1. Uitgangspunten

1. **Elke vorm van meekijken geeft alarm.** We beoordelen niet of het "verdacht" is. Een meekijkprogramma dat start is genoeg.
2. **De gebruiker beslist, met twee knoppen.** "Ik vertrouw dit niet" (de rode knop) of "Ik heb Sanne aan de lijn, ze helpt me". Er is geen pincode.
3. **Herstarten zet alles terug.** De rode knop haalt de computer van het internet tot de volgende herstart. Sanne kan dus altijd adviseren: "herstart de computer".
4. **Alles blijft lokaal.** Er is geen server, geen account en geen abonnement. Spike stuurt nooit iets weg.
5. **Alles wordt vastgelegd.** Tijden, programma's, IP-adressen, bestanden en schermafbeeldingen gaan naar een map met bewijs voor politie en bank.
6. **Een oplichter kan Spike niet zomaar uitzetten.** De oplichter heeft dezelfde rechten als de gebruiker. Spike draait als Windows-service, met meer rechten dan dat.
7. **Spike herkent op inhoud, niet op naam.** Een hernoemd `factuur.exe` dat in werkelijkheid AnyDesk is, wordt herkend.

Buiten de kern vallen: RDP (vraagt te veel medewerking van het slachtoffer), nepmeldingen in de browser, telefoons en tablets en meldingen op afstand aan familie.

## 2. Opbouw

Eén bestand, `Spike.exe` (self-contained .NET 8, net als Tremors), in drie rollen:

| Rol | Start als | Rechten | Taak |
|---|---|---|---|
| **Wachter** | Windows-service "Spike" | LocalSystem | Processen, services en verbindingen bewaken. Programma's pauzeren en stoppen. Netwerk uit. Bewijs schrijven. De Hond bewaken |
| **Hond** | `Spike.exe` in de sessie van de gebruiker | Gebruiker | Tray-icoon, alarmscherm, blaf, schermafbeeldingen, muisbewaking |
| **Installateur** | `Spike.exe --install` / `--uninstall` | Admin (UAC), eenmalig door de helper | Service registreren, instellingen invullen, eventueel Snelle hulp verwijderen |

De service draait in sessie 0 en kan daar geen schermen tonen of schermafbeeldingen maken. Daarom bestaat de Hond. Wachter en Hond praten via een named pipe (`\\.\pipe\Spike`). De Wachter start de Hond in elke aangemelde sessie (WTSQueryUserToken plus CreateProcessAsUser) en start hem opnieuw als hij wordt afgesloten.

Mappen:
- `C:\Program Files\Spike\Spike.exe`
- `%ProgramData%\Spike\settings.json`: alleen admins en SYSTEM mogen schrijven. Een oplichter kan het nummer van Sanne dus niet vervangen door het zijne.
- `%ProgramData%\Spike\Bewijs\`: de gebruiker mag lezen, alleen SYSTEM mag schrijven en wissen.

## 3. Herkennen

### 3.1 De catalogus
Een ingebouwde lijst (`catalog.json`) met zo'n 60 meekijkprogramma's, afgeleid van [LOLRMM](https://github.com/magicsword-io/LOLRMM) (Apache 2.0, bronvermelding in NOTICE). Een script in `tools\` bouwt de lijst opnieuw op. Per programma:
- naam voor in de melding ("AnyDesk")
- oorspronkelijke bestandsnaam en productnaam uit de versie-informatie van het bestand
- naam van de ondertekenaar in het certificaat
- bekende servicenamen
- bekende installatiepaden en pakketnamen (Snelle hulp: `MicrosoftCorporationII.QuickAssist`)
- poorten en domeinen (alleen voor het logboek)
- logbestanden die als bewijs gekopieerd worden (AnyDesk: `connection_trace.txt`, `ad.trace`, `ad_svc.trace`)

### 3.2 Signalen
| # | Signaal | Hoe | Wanneer | Bij hernoemen |
|---|---|---|---|---|
| A | **Nieuw proces** met een bekende handtekening of versie-informatie | WMI `Win32_ProcessStartTrace` in de Wachter, plus elke 2 s een controle van alle processen als vangnet | Bij het starten | Werkt: handtekening en versie-informatie veranderen niet |
| B | **Nieuwe service** uit de catalogus | Windows-gebeurtenis 7045 (service geïnstalleerd) en controle van de servicelijst | Bij het installeren | Werkt: binaire inhoud wordt gecontroleerd zoals bij A |
| C | **Snelle hulp** gestart | Pakketpad onder `WindowsApps\MicrosoftCorporationII.QuickAssist*` | Bij het starten | Niet te hernoemen |
| D | **Iemand anders bestuurt de muis** | In de Hond: muisbewegingen met `LLMHF_INJECTED` (0x01) terwijl de echte muis stil ligt | Tijdens de sessie | Werkt voor elk programma, ook onbekende. **Eerst meten (fase 0)** |
| E | **Download** van een bekend installatiebestand | FileSystemWatcher op de map Downloads, inhoud gecontroleerd zoals bij A | Vóór het starten | Werkt |

A, B en C geven direct alarm. D geeft alarm als het een paar seconden aanhoudt en niet verklaard wordt door een programma dat bewust is toegestaan (schermtoetsenbord, Tremors, AutoHotkey). E geeft een zachtere waarschuwing: "U heeft AnyDesk gedownload. Belt er iemand die u dat vroeg?"

Microsoft-programma's die wel zijn ondertekend maar geen meekijkprogramma zijn (Teams, Edge), zitten niet in de catalogus. Schermdelen in Teams of Zoom valt buiten de kern.

## 4. Het alarm

Zodra A, B, C of D afgaat:

1. De Wachter **pauzeert** het meekijkprogramma: hij bevriest het proces (NtSuspendProcess). De oplichter ziet een stilstaand beeld en kan niets meer doen. Of dit de sessie netjes bevriest of laat vallen, meten we in fase 0.
2. De Hond toont **over alle schermen** een rood venster dat altijd bovenaan blijft. Spike blaft één keer. Het venster heeft geen kruisje en Escape of Alt+F4 sluiten het niet.
3. Op het scherm staat:

   > **Stop! Iemand probeert uw computer over te nemen.**
   > Programma: AnyDesk
   > Microsoft, de bank en de politie vragen dit nooit.
   >
   > [foto] **Bel eerst Sanne: 06-12 34 56 78**
   >
   > ( 🟥 Ik vertrouw dit niet: alles eruit )   ( Ik heb Sanne aan de lijn, ze helpt me )

4. **De knoppen reageren alleen op een echte hand.** Een klik of toetsaanslag met het kenmerk "nagebootst" (`LLMHF_INJECTED` 0x01 / `LLKHF_INJECTED` 0x10) telt niet. De oplichter kan dus niet op afstand op "Sanne helpt me" klikken.
5. Zonder keuze blijft het scherm staan en blijft het programma gepauzeerd.

### 4.1 De rode knop: "Ik vertrouw dit niet"
De Wachter doet in deze volgorde:
1. Alle processen uit de catalogus beëindigen.
2. Hun services stoppen en op "uitgeschakeld" zetten, zodat ze na een herstart niet vanzelf terugkomen.
3. Alle netwerkadapters uitschakelen. Dat is het enige wat een lopende verbinding zeker verbreekt; een firewallregel doet dat mogelijk niet. De Wachter onthoudt welke adapters hij uitzette en zet ze bij de volgende start van Windows weer aan.
4. De logbestanden van het programma naar de bewijsmap kopiëren.

Daarna toont de Hond:
> **Uw computer is nu van het internet af.** Er kan niemand meer meekijken.
> Leg de telefoon neer. Bel Sanne: 06-12 34 56 78.
> Na een herstart werkt het internet weer.

### 4.2 "Ik heb Sanne aan de lijn"
- Eén bevestiging: "Spreekt u nu echt met Sanne? Twijfelt u, hang dan op en bel Sanne zelf op 06-…" Daaronder [Ja, het is Sanne] en [Nee, alles eruit].
- Bij Ja gaat het programma weer verder. Spike toont een kleine oranje balk bovenin: "Sanne kijkt mee met AnyDesk". Het logboek en de schermafbeeldingen gaan gewoon door.
- Het alarm komt pas terug als hetzelfde programma stopt en later opnieuw start, of als er een ander meekijkprogramma bijkomt.

Er kunnen één tot drie helpers ingesteld worden (bijvoorbeeld twee kinderen). De knop noemt dan de eerste naam en het scherm toont alle nummers.

## 5. Bewijs

Per voorval een map `%ProgramData%\Spike\Bewijs\2026-11-13 14.05 AnyDesk\` met:

| Bestand | Inhoud | Bron |
|---|---|---|
| `logboek.txt` | Alles met tijd: wat gestart werd, de keuze van de gebruiker, wanneer het stopte | Wachter |
| `verbindingen.csv` | Elke 2 s: proces, lokaal en extern IP-adres, poort, begin en einde | Wachter, GetExtendedTcpTable en GetExtendedUdpTable |
| `processen.csv` | Elk proces dat tijdens de sessie start, met opdrachtregel en ouderproces (cmd, eventvwr, netstat, browser met bankadres) | Wachter |
| `bestanden.csv` | Aangemaakte, gewijzigde, hernoemde en verwijderde bestanden in het gebruikersprofiel tijdens de sessie | Wachter, FileSystemWatcher |
| `scherm\*.jpg` | Elke 3 s een schermafbeelding van alle schermen, verkleind naar 1280 px breed | Hond |
| `programma-logs\` | Kopieën van de eigen logbestanden van het meekijkprogramma (met o.a. het ID en IP-adres van de oplichter) | Wachter |
| `samenvatting.txt` | Eén pagina in gewone taal voor politie of bank: wanneer, welk programma, welke IP-adressen, welke keuze | Wachter, bij het einde van de sessie |

- Opname begint bij het alarm en loopt tot het meekijkprogramma stopt, ook na "Sanne helpt me".
- De gebruiker en een oplichter met dezelfde rechten kunnen niets wissen.
- Opruimen: mappen ouder dan 90 dagen of boven de 2 GB in totaal gaan weg, de oudste eerst. De samenvatting blijft altijd bewaard.
- Privacy: schermafbeeldingen kunnen bankgegevens bevatten. Ze blijven op de pc en de README zegt dat duidelijk.

## 6. Bescherming tegen uitzetten

| Poging | Wat Spike doet |
|---|---|
| Hond afsluiten via Taakbeheer | De Wachter start hem binnen 2 s opnieuw en schrijft het in het logboek |
| Service stoppen | Herstelacties staan op "altijd opnieuw starten". Een gewone gebruiker mag de service niet stoppen (service-DACL). Bij een stop door een admin zegt `OnStop` eerst via de Hond: "Iemand zet Spike uit. Vroeg iemand aan de telefoon u dat? Dan is het een oplichter." |
| Spike verwijderen | Kan alleen via `Spike.exe --uninstall` met UAC. Eerst verschijnt een scherm met 15 seconden wachttijd en de vraag "Vraagt iemand aan de telefoon u dit?", daarna het nummer van Sanne |
| Instellingen wijzigen (nummer van Sanne) | Alleen met UAC. Het instellingenvenster is voor de helper, niet voor de gebruiker |
| Tray-menu | Heeft geen "Afsluiten" en geen "Pauzeren" |

Volledig waterdicht is dit niet: een admin die precies weet wat hij doet, komt er altijd langs. Het doel is dat een oplichter met een script het niet ongemerkt kan.

## 7. Bediening

**Tray-menu (hondje met open ogen):**
- Spike waakt (status, geen actie)
- Test het alarm (blaftest; laat het echte scherm zien met "TEST" erop)
- Bewijsmap openen
- Instellingen… (UAC)
- Over Spike

**Installatie door de helper (één keer):**
1. `Spike.exe` downloaden en dubbelklikken. UAC verschijnt.
2. Formulier: naam, telefoonnummer en eventueel foto van één tot drie helpers. Taal. Vinkjes: "Snelle hulp verwijderen" (aan) en "Hulp op afstand van Windows uitzetten" (aan).
3. Spike kopieert zichzelf naar Program Files, registreert de service, start de Hond en toont meteen de blaftest.

**Printbaar kaartje** (pdf, net als de telefoonhandleiding van Tremors): "Microsoft en de bank bellen nooit. Laat nooit iemand meekijken. Bel Sanne: 06-…". Voor naast de monitor.

Talen: Nederlands, Engels, Frans en Duits (zelfde opzet als `Text.cs` in Tremors).

## 8. Projectindeling

```
Spike\
  Spike.sln
  src\Spike\
    Program.cs          rol kiezen: service, hond, install, uninstall
    Catalog.cs          catalog.json lezen
    Identify.cs         bestand -> handtekening en versie-informatie -> catalogus
    Detector.cs         signalen -> Detection (zuivere logica, testbaar)
    Guard\              de Wachter: ProcessWatch, ServiceWatch, NetWatch, Evidence, Panic, Suspend
    Dog\                de Hond: TrayIcon, AlarmForm, BannerForm, ScreenShots, InputWatch
    Pipe.cs             berichten tussen Wachter en Hond
    Installer.cs
    Settings.cs, Text.cs
    catalog.json        (embedded)
  tests\Spike.Tests\
  tools\make_catalog.py LOLRMM -> catalog.json
  docs\ontwerp.md
```

Tests (xUnit, net als Tremors):
- Een hernoemde AnyDesk wordt herkend aan de handtekening en de versie-informatie.
- Teams, Edge en Tremors worden niet herkend.
- Snelle hulp wordt herkend aan het pakketpad.
- Toestandsmachine van het alarm: alarm → rood / Sanne → einde. Een nieuw programma geeft opnieuw alarm, hetzelfde programma na "Sanne" niet.
- Een nagebootste klik telt niet op een alarmknop.
- Het opruimen van de bewijsmap houdt de samenvatting.
- Adapters die Spike uitzette, gaan bij de start weer aan. Andere adapters blijven zoals ze waren.

## 9. Bouwplan

| Fase | Wat | Tijd |
|---|---|---|
| **0. Meten** | In Windows Sandbox of een testmachine: AnyDesk, TeamViewer, RustDesk, UltraViewer en Snelle hulp installeren en gebruiken. Vastleggen: ondertekenaar, versie-informatie, processen, services, verbindingen, logbestanden. Testen of signaal D werkt, of bevriezen de sessie stillegt en of adapters uitzetten de sessie meteen verbreekt | 1-2 dagen |
| **1. Kern** | Catalogus, herkenning, Wachter en Hond, alarmscherm, rode knop, Sanne-knop, logboek, tests | 1 week |
| **2. Bewijs en bescherming** | Verbindingen, processen, bestanden, schermafbeeldingen, programma-logs, samenvatting, opruimen. Service-DACL, herstarten van de Hond, uninstall-scherm. Installateur met instellingen | 1 week |
| **3. Afmaken** | Signaal D en E, blaftest, kaartje, vier talen, README in vier talen, release via GitHub zoals Tremors | 3-5 dagen |

## 10. Open punten

- **Fase 0 vraagt toestemming:** meekijkprogramma's downloaden en installeren. Het liefst in Windows Sandbox (zit in Windows 11 Pro, moet eenmalig aangezet worden via "Windows-onderdelen in- of uitschakelen").
- **Antivirus:** een programma dat processen bevriest, beëindigt en adapters uitzet, kan als verdacht worden gezien. Open broncode helpt. Na de eerste release Spike bij Microsoft aanbieden voor controle op vals alarm.
- **Vaste meekijkhulp van familie:** heeft Sanne TeamViewer vast op de pc gezet, dan geeft elke sessie alarm en drukt de gebruiker elke keer op "Sanne helpt me". Dat is bewust.
- **Concurrenten** (Seraph Secure, Bitdefender Remote Access Protection, Malwarebytes Scam Guard) eerst zelf uitproberen om te zien wat ze wel en niet doen.
- **Naam:** "Spike" wordt ook gebruikt voor een mailprogramma. Dat is een ander soort product. Vóór de eerste release in TMview zoeken naar klasse 9.
