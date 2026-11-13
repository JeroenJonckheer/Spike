<div align="center">

<img src="docs/icon.png" width="96" alt="">

# Spike

**A loyal watchdog for your computer.**
Free, private and always on guard. One small program for Windows.

[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

**English** · [Nederlands](README.nl.md)

</div>

---

> **Spike is being built.** There is no download yet. This page describes what Spike will do.
> The design is in [docs/ontwerp.md](docs/ontwerp.md) (Dutch).

You can always count on a faithful dog. It knows your home, it knows your family and it
barks the moment a stranger is at the door. Spike watches over your computer the same way.
It lies quietly in the corner of the screen and you won't notice it. Until someone tries to get in.

**Spike knows when you are in danger, before it is too late.**

## Why

The phone rings. "This is the fraud department of your bank. Your account is under attack.
Please install this small program so we can help you." Ten minutes later the savings are gone.

Helpdesk fraud was the most reported type of fraud in the Netherlands in 2025, and it works the
same way everywhere: the scammer asks the victim to install AnyDesk, TeamViewer or Windows Quick
Assist, then takes over the computer and the online banking. The tools themselves are legitimate.
That is why antivirus software lets them through.

Older people are hit hardest. In the United States, people over 60 lost more than a billion dollars
to tech-support scams in 2025 alone.

## What Spike will do

- **Barks the moment someone takes over the computer.** As soon as any remote-access tool starts
  (AnyDesk, TeamViewer, Quick Assist, RustDesk, UltraViewer, ScreenConnect and about 60 others),
  Spike barks once and covers the screen with a big red warning.
- **Shows someone you trust, not the scammer.** The warning shows the name and phone number of
  a son, daughter or friend: *"Stop. Call Sanne first: 06-12 34 56 78."*
- **Two big buttons.**
  - **"I don't trust this"** closes the remote-access tool and takes the computer off the internet
    until the next restart.
  - **"Sanne is on the phone, she's helping me"** lets the session continue, for when family helps.
- **The scammer can't click for you.** The buttons only respond to a real hand on the real mouse,
  not to clicks sent from far away.
- **Recognises renamed programs.** Spike looks at what a program really is (its digital signature
  and built-in product information), not at its file name.
- **Keeps the evidence.** Times, programs, IP addresses, changed files and a screenshot every few
  seconds, plus a one-page summary for the police or the bank. Everything stays on the computer.
- **Hard to switch off.** Spike runs as a Windows service. A scammer with the same rights as the
  user cannot stop it, and every attempt is logged.

## What Spike will not do

- Spike never sends anything anywhere. No account, no server, no subscription.
- Spike does not read your messages, passwords or bank details.
- Spike does not protect phones or tablets, and it cannot stop a phone call.

## Made by

Spike is made by Jeroen Jonckheer, who also made [Tremors](https://github.com/JeroenJonckheer/Tremors),
a free calm mouse for shaky hands.

The list of remote-access tools is based on [LOLRMM](https://github.com/magicsword-io/LOLRMM)
(Apache 2.0).

## License

[MIT](LICENSE). Free for everyone, and it will stay that way.
