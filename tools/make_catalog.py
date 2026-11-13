"""Builds src/Spike/catalog.json: the remote-access programs Spike barks at, and how to
recognise them even when they are renamed.

The data comes from LOLRMM (https://lolrmm.io, Apache 2.0). LOLRMM lists 350+ tools,
many of them company tools or harmless programs (a screen reader, backup software)
that would give false alarms on a home PC. So the list of programs is chosen by hand
below; LOLRMM supplies what each one looks like.

Usage: python tools/make_catalog.py [cache-dir]
"""
import json
import os
import sys
import urllib.request

TOOLS_URL = "https://lolrmm.io/api/rmm_tools.json"
CERTS_URL = "https://raw.githubusercontent.com/magicsword-io/LOLRMM/main/website/public/api/rmm_certificates.json"

# Name shown on the alarm -> LOLRMM entries that describe it.
CHOSEN = {
    "AnyDesk": ["AnyDesk"],
    "TeamViewer": ["TeamViewer"],
    "Quick Assist": ["Quick Assist"],
    "RustDesk": ["RustDesk"],
    "UltraViewer": ["UltraViewer"],
    "ScreenConnect": ["ScreenConnect", "ConnectWise Control", "ConnectWise",
                      "247ithelp.com (ConnectWise)", "ITSupport247 (ConnectWise)"],
    "Supremo": ["Supremo"],
    "Zoho Assist": ["Zoho Assist"],
    "AeroAdmin": ["AeroAdmin"],
    "Ammyy Admin": ["Ammyy Admin"],
    "AnyViewer": ["AnyViewer"],
    "HopToDesk": ["HopToDesk"],
    "DWService": ["DW Service"],
    "Chrome Remote Desktop": ["Chrome Remote Desktop"],
    "RemotePC": ["RemotePC"],
    "Radmin": ["RAdmin"],
    "Splashtop": ["Splashtop", "Splashtop Remote", "Splashtop (Beta)"],
    "LogMeIn": ["LogMeIn", "LogMeIn rescue"],
    "GoTo Resolve": ["GoToAssist", "GoToAssist (GoTo Resolve)", "GoToAssist Agent Desktop Console"],
    "SimpleHelp": ["SimpleHelp"],
    "Iperius Remote": ["Iperius Remote"],
    "LiteManager": ["LiteManager"],
    "Remote Utilities": ["Remote Utilities"],
    "NetSupport": ["NetSupport Manager"],
    "BeyondTrust": ["BeyondTrust", "BeyondTrust (Bomgar)"],
    "ISL Online": ["ISL Online", "ISL Light"],
    "ShowMyPC": ["ShowMyPC"],
    "Mikogo": ["Mikogo"],
    "BeamYourScreen": ["BeamYourScreen"],
    "GetScreen": ["GetScreen"],
    "HelpWire": ["HelpWire"],
    "FixMe.it": ["FixMe.it"],
    "Any Support": ["Any Support"],
    "Alpemix": ["Alpemix"],
    "Anyplace Control": ["Anyplace Control"],
    "Aspia": ["Aspia"],
    "RemSupp": ["RemSupp"],
    "Rocket Remote Desktop": ["Rocket Remote Desktop"],
    "RemoteCall": ["RemoteCall"],
    "Remotely": ["Remotely"],
    "MeshCentral": ["MeshCentral"],
    "Atera": ["Atera"],
    "Action1": ["Action1"],
    "Netop": ["Netop", "Netop Remote Control (aka Impero Connect)", "Netop Remote Control (Impero Connect)"],
    "VNC": ["TightVNC", "UltraVNC", "Ultra VNC", "RealVNC", "VNC Connect", "TigerVNC", "VNC"],
    # Added after comparing with remote-access-tool-blocker (pupontech, Apache 2.0).
    "AweSun": ["AweRay"],
    "Jump Desktop": ["Jump Desktop"],
    "FastViewer": ["FastViewer"],
    "pcvisit": ["Pcvisit"],
    "RuDesktop": ["RuDesktop"],
    "RMS (Remote Manipulator System)": ["Remote Manipulator System"],
    "NinjaOne": ["NinjaRMM"],
    "Syncro": ["Syncro"],
    "SuperOps": ["SuperOps"],
    "Level": ["Level", "Level.io"],
    "FleetDeck": ["FleetDeck.io"],
    "Pilixo": ["Pilixo", "Remote Desktop Plus"],
    "Seetrol": ["Seetrol"],
    "HelpU": ["HelpU"],
    "KHelpDesk": ["KHelpDesk"],
    "NTR Remote": ["NTR Remote"],
    "SetMe PRO": ["SetMe PRO"],
    "GotoHTTP": ["GotoHTTP"],
    "Instant Housecall": ["Instant Housecall"],
    "Xeox": ["Xeox"],
    "Gorelo": ["Gorelo RMM"],
    "SpyAnywhere": ["SpyAnywhere"],
    "Tactical RMM": ["Tactical RMM"],
    "RemoteView": ["RemoteView"],
}

# Who signs each program, chosen by hand. The signer lists in LOLRMM and derived lists
# contain mistakes (Realtek, WinRAR, PuTTY, Unity and an antivirus company all appear as
# "remote-access" signers), probably from repackaged samples. A signer is only listed here
# when that company makes nothing but remote-access software; companies that also make
# ordinary programs (AOMEI, IDrive, Famatech, Zoho, GoTo, Electronic Team, ENTER) are left
# out, so those programs are recognised by file and product name only.
# Every entry is to be checked against a real download in phase 0.
SIGNERS = {
    "AnyDesk": ["AnyDesk Software GmbH", "philandro Software GmbH"],
    "TeamViewer": ["TeamViewer Germany GmbH", "TeamViewer GmbH"],
    "RustDesk": ["PURSLANE"],
    "UltraViewer": ["DUC FABULOUS CO.,LTD"],
    "ScreenConnect": ["ConnectWise, LLC", "ConnectWise, Inc."],
    "Supremo": ["Nanosystems S.r.l."],
    "AeroAdmin": ["Aeroadmin LLC"],
    "HopToDesk": ["Begonia Holdings LLC"],
    "DWService": ["DWSNET srl"],
    "Splashtop": ["Splashtop Inc."],
    "LiteManager": ["IP Ter-Osipov Aleksey Vladimirovich"],
    "Remote Utilities": ["REMOTE UTILITIES PTE. LTD."],
    "RMS (Remote Manipulator System)": ["REMOTE UTILITIES PTE. LTD."],
    "NetSupport": ["NetSupport Ltd", "NETSUPPORT LTD."],
    "BeyondTrust": ["Bomgar Corporation"],
    "ISL Online": ["ISL Online Ltd."],
    "ShowMyPC": ["ShowMyPC INC"],
    "Mikogo": ["BeamYourScreen GmbH"],
    "GetScreen": ["Kopetra Ltd."],
    "Any Support": ["KOINO Co., Ltd."],
    "Alpemix": ["TEKNOPARS BİLİŞİM TEKNOLOJİLERİ SANAYİ VE TİCARET LTD.ŞTİ."],
    "Anyplace Control": ["Anyplace Control Software"],
    "RemoteCall": ["Rsupport Co., Ltd."],
    "RemoteView": ["Rsupport Co., Ltd."],
    "Atera": ["Atera Networks Ltd"],
    "Action1": ["Action1 Corporation"],
    "Netop": ["NETOP TECH SRL", "Impero Solutions Limited"],
    "VNC": ["RealVNC Ltd", "uvnc bvba", "GlavSoft OOO"],
    "AweSun": ["上海贝锐信息科技股份有限公司"],
    "Jump Desktop": ["Phase Five Systems LLC", "PhaseFive Systems LLC"],
    "FastViewer": ["FastViewer GmbH"],
    "pcvisit": ["pcvisit software ag"],
    "RuDesktop": ["Advanced Technologies LLC"],
    "NinjaOne": ["NinjaOne LLC"],
    "Syncro": ["Servably Inc."],
    "SuperOps": ["Superops Inc."],
    "Level": ["Level Software, Inc."],
    "FleetDeck": ["FleetDeck Inc", "FLEETDECK INC."],
    "Pilixo": ["Plooto Star Inc"],
    "Seetrol": ["Knowhow Electel Inc."],
    "HelpU": ["helpU Inc."],
    "KHelpDesk": ["KHELPDESK TECHNOLOGY LTDA"],
    "NTR Remote": ["Net Transmit & Receive SL"],
    "SetMe PRO": ["Techinline Ltd"],
    "GotoHTTP": ["合肥屏播网络科技有限公司"],
    "Xeox": ["hs2n Informationstechnologie GmbH"],
    "Gorelo": ["GORELO TECH, INC."],
    "SpyAnywhere": ["Spytech Software and Design, Inc."],
}

# Seen in our own measurements (phase 0, Windows Sandbox, 13-11-2026); LOLRMM misses these.
MEASURED = {
    "AnyDesk": {"products": ["AnyDesk"]},
    "TeamViewer": {"originalFileNames": ["TeamViewer.exe", "tv_w32.exe", "tv_x64.exe", "tv_ARM64.exe"]},
    "RustDesk": {"products": ["RustDesk"], "originalFileNames": ["rustdesk.exe"]},
    "UltraViewer": {"products": ["UltraViewer", "UltraViewerService"],
                    "originalFileNames": ["UltraViewer_Service.exe"], "services": ["UltraViewService"]},
    # Windows 11 installs Quick Assist as a Store app.
    "Quick Assist": {"paths": [r"*\WindowsApps\MicrosoftCorporationII.QuickAssist_*\*"],
                     "fileNames": ["QuickAssist.exe"]},
}

# Services that other programs install as well.
GENERIC_SERVICES = {"winring0_1_2_0"}

# Values that also belong to ordinary programs; never match on these alone.
GENERIC_FILES = {
    "setup.exe", "install.exe", "installer.exe", "launcher.exe", "update.exe", "updater.exe",
    "agent.exe", "service.exe", "client.exe", "server.exe", "uninstall.exe", "unins000.exe",
    "msiexec.exe", "rundll32.exe", "java.exe", "javaw.exe", "node.exe", "python.exe",
    "pythonw.exe", "electron.exe", "chrome.exe", "msedge.exe", "explorer.exe", "svchost.exe",
    "conhost.exe", "cmd.exe", "powershell.exe", "mstsc.exe", "",
}
GENERIC_PRODUCTS = {
    "microsoft® windows® operating system", "microsoft windows operating system",
    "google chrome", "java", "java(tm) platform se", "node.js", "python", "electron", "",
}
# Companies that sign many other programs as well: their signature alone proves nothing.
BROAD_SIGNERS = {
    "microsoft corporation", "microsoft windows", "microsoft windows publisher",
    "google llc", "google inc", "apple inc.", "adobe inc.", "adobe systems incorporated",
}


def fetch(url, cache):
    path = os.path.join(cache, os.path.basename(url))
    if not os.path.exists(path):
        os.makedirs(cache, exist_ok=True)
        req = urllib.request.Request(url, headers={"User-Agent": "spike-catalog"})
        with urllib.request.urlopen(req, timeout=60) as r, open(path, "wb") as f:
            f.write(r.read())
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def pe_entries(details):
    pe = details.get("PEMetadata") or []
    if isinstance(pe, dict):
        return [pe]
    return [p for p in pe if isinstance(p, dict)]


def add(bucket, value, generic=frozenset()):
    if isinstance(value, str):
        v = value.strip()
        if v and v.lower() not in generic and v.lower() not in {b.lower() for b in bucket}:
            bucket.append(v)


def windows_path(p):
    return isinstance(p, str) and (":" in p or p.startswith("%") or "\\" in p)


def build(tools, certs):
    by_name = {t["Name"]: t for t in tools}
    certs_by_name = {c["name"]: c for c in certs}
    catalog, missing = [], []
    for shown, sources in CHOSEN.items():
        entry = {"name": shown, "originalFileNames": [], "fileNames": [], "products": [],
                 "signers": [], "services": [], "paths": [], "logs": [], "domains": [], "ports": []}
        found = False
        for src in sources:
            t = by_name.get(src)
            if t is None:
                continue
            found = True
            d = t.get("Details") or {}
            for p in pe_entries(d):
                add(entry["originalFileNames"], p.get("OriginalFileName"), GENERIC_FILES)
                add(entry["fileNames"], p.get("Filename"), GENERIC_FILES)
                add(entry["products"], p.get("Product"), GENERIC_PRODUCTS)
            for p in d.get("InstallationPaths") or []:
                if windows_path(p):
                    add(entry["paths"], p)
            a = t.get("Artifacts") or {}
            for e in a.get("EventLog") or []:
                if isinstance(e, dict) and str(e.get("EventID")) in ("7045", "4697"):
                    add(entry["services"], e.get("ServiceName"), GENERIC_SERVICES)
            for f in a.get("Disk") or []:
                if isinstance(f, dict) and f.get("OS", "Windows") == "Windows" and windows_path(f.get("File")):
                    add(entry["logs"], f["File"])
            for n in a.get("Network") or []:
                if isinstance(n, dict):
                    for dom in n.get("Domains") or []:
                        add(entry["domains"], dom)
                    for port in n.get("Ports") or []:
                        if isinstance(port, int) and port not in entry["ports"]:
                            entry["ports"].append(port)
        for s in SIGNERS.get(shown, []):
            add(entry["signers"], s, BROAD_SIGNERS)
        for key, values in MEASURED.get(shown, {}).items():
            for v in values:
                add(entry[key], v)
        if not found:
            missing.append(shown)
            continue
        catalog.append(entry)
    return catalog, missing


if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    cache = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "lolrmm-cache")
    catalog, missing = build(fetch(TOOLS_URL, cache), fetch(CERTS_URL, cache))
    out = os.path.join(here, "..", "src", "Spike", "catalog.json")
    with open(out, "w", encoding="utf-8", newline="\r\n") as f:
        json.dump({"source": "LOLRMM (https://lolrmm.io), Apache License 2.0", "tools": catalog},
                  f, indent=2, ensure_ascii=False)
        f.write("\n")
    weak = [e["name"] for e in catalog if not (e["originalFileNames"] or e["products"] or e["signers"])]
    print(f"{len(catalog)} programs written to {os.path.normpath(out)}")
    if missing:
        print("not in LOLRMM:", ", ".join(missing))
    if weak:
        print("nothing to recognise them by:", ", ".join(weak))
