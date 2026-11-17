#!/usr/bin/env python3
"""Legt in einem frischen openHAB den ersten Administrator an und erzeugt ein API-Token.

  openhab-token.py URL BENUTZER PASSWORT TOKENNAME   -> gibt das Token aus
Nutzt die Formulare /auth (Konto erstellen) und /createApiToken wie ein Browser.
"""
import html
import re
import sys
import urllib.parse
import urllib.request

basis, benutzer, passwort, name = sys.argv[1].rstrip("/"), *sys.argv[2:5]


def holen(pfad):
    with urllib.request.urlopen(basis + pfad, timeout=30) as r:
        return r.read().decode()


def senden(pfad, felder):
    daten = urllib.parse.urlencode(felder).encode()
    req = urllib.request.Request(basis + pfad, data=daten, method="POST")

    class KeinUmleiten(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, *a, **k):
            return None
    try:
        with urllib.request.build_opener(KeinUmleiten).open(req, timeout=30) as r:
            return r.status, r.read().decode()
    except urllib.error.HTTPError as e:      # 302 nach erfolgreicher Anmeldung
        return e.code, e.headers.get("Location", "")


def versteckt(seite):
    return {m.group(1): html.unescape(m.group(2)) for m in re.finditer(
        r'<input type="hidden" name="([^"]+)" value="([^"]*)"', seite)}


q = urllib.parse.urlencode({"response_type": "code", "client_id": basis,
                            "redirect_uri": basis, "scope": "admin", "state": "1",
                            "code_challenge": "w" * 43, "code_challenge_method": "S256"})
seite = holen("/auth?" + q)
if "password_repeat" in seite and 'type="password" placeholder' in seite:
    felder = versteckt(seite)
    felder.update(username=benutzer, password=passwort, password_repeat=passwort)
    status, antwort = senden("/auth", felder)
    print("Konto:", status, antwort[:120], file=sys.stderr)

seite = holen("/createApiToken")
felder = versteckt(seite)
felder.update(username=benutzer, password=passwort, token_name=name, token_scope="")
status, antwort = senden("/createApiToken", felder)
m = re.search(r"oh\.%s\.[A-Za-z0-9]+" % re.escape(name), antwort)
if not m:
    text = re.sub(r"\s+", " ", re.sub(r"<[^>]+>", " ", antwort))
    print("kein Token:", status, text[-400:], file=sys.stderr)
    sys.exit(1)
print(m.group(0))
