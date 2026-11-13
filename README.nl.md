<div align="center">

<img src="docs/icon.png" width="96" alt="">

# Spike

**Een trouwe waakhond voor uw computer.**
Gratis, privé en altijd op wacht. Eén klein programma voor Windows.

[![Licentie: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

[English](README.md) · **Nederlands**

</div>

---

> **Spike wordt nog gebouwd.** Er is nog niets te downloaden. Deze pagina beschrijft wat Spike gaat doen.
> Het ontwerp staat in [docs/ontwerp.md](docs/ontwerp.md).

Op een trouwe viervoeter kunt u altijd rekenen. Hij kent uw huis, hij kent uw familie en hij
slaat aan zodra er een vreemde aan de deur staat. Zo waakt Spike over uw computer. Hij ligt
rustig in de hoek van het scherm en u merkt niets van hem. Tot iemand probeert binnen te komen.

**Spike weet wanneer u gevaar loopt, nog voordat het te laat is.**

## Waarom

De telefoon gaat. "U spreekt met de fraudeafdeling van uw bank. Uw rekening wordt aangevallen.
Installeert u even dit programmaatje, dan kunnen wij u helpen." Tien minuten later is het spaargeld weg.

Helpdeskfraude was in 2025 de meest gemelde vorm van fraude in Nederland. Het werkt overal hetzelfde:
de oplichter laat het slachtoffer AnyDesk, TeamViewer of Snelle hulp van Windows installeren. Daarna
neemt hij de computer en het internetbankieren over. De programma's zelf zijn echt. Daarom laat een
virusscanner ze gewoon door.

Ouderen worden het hardst getroffen. In de Verenigde Staten verloren 60-plussers alleen al in 2025
meer dan een miljard dollar aan nep-helpdesks.

## Wat Spike gaat doen

- **Blaft zodra iemand de computer overneemt.** Start er een meekijkprogramma (AnyDesk, TeamViewer,
  Snelle hulp, RustDesk, UltraViewer, ScreenConnect en zo'n 60 andere), dan blaft Spike één keer.
  Er verschijnt een grote rode waarschuwing over het hele scherm.
- **Toont iemand die u vertrouwt, niet de oplichter.** Op de waarschuwing staan de naam en het
  telefoonnummer van een zoon, dochter of vriend: *"Stop! Bel eerst Sanne: 06-12 34 56 78."*
- **Twee grote knoppen.**
  - **"Ik vertrouw dit niet"** sluit het meekijkprogramma en haalt de computer van het internet tot
    de volgende herstart.
  - **"Ik heb Sanne aan de lijn, ze helpt me"** laat de sessie doorgaan, voor als familie helpt.
- **De oplichter kan niet voor u klikken.** De knoppen reageren alleen op een echte hand op de echte
  muis, niet op klikken van ver weg.
- **Herkent hernoemde programma's.** Spike kijkt naar wat een programma echt is (de digitale
  handtekening en de ingebouwde productinformatie), niet naar de bestandsnaam.
- **Bewaart het bewijs.** Tijden, programma's, IP-adressen, gewijzigde bestanden en om de paar
  seconden een schermafbeelding. Plus een samenvatting van één pagina voor politie of bank.
  Alles blijft op de computer.
- **Moeilijk uit te zetten.** Spike draait als Windows-service. Een oplichter met dezelfde rechten
  als de gebruiker kan Spike niet stoppen. Elke poging wordt vastgelegd.

## Wat Spike niet doet

- Spike stuurt nooit iets weg. Geen account, geen server, geen abonnement.
- Spike leest geen berichten, wachtwoorden of bankgegevens.
- Spike beschermt geen telefoons of tablets en kan een telefoongesprek niet tegenhouden.

## Gemaakt door

Spike is gemaakt door Jeroen Jonckheer, die ook [Tremors](https://github.com/JeroenJonckheer/Tremors)
maakte: een gratis rustige muis voor trillende handen.

De lijst met meekijkprogramma's is gebaseerd op [LOLRMM](https://github.com/magicsword-io/LOLRMM)
(Apache 2.0).

## Licentie

[MIT](LICENSE). Gratis voor iedereen en dat blijft zo.
