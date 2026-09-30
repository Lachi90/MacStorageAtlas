# Manuelle Update-Abnahme

Diese Anleitung prüft die native Sparkle-Oberfläche mit getrennten, lokal signierten Testpaketen. Die normale Installation, ihre Einstellungen und ihr Verlauf werden nicht verändert. Für x64 steht auf diesem Projekt-Mac Rosetta zur Verfügung; ein echter Intel-Mac ist nicht vorhanden. Rosetta deckt den Intel-Payload und die Architekturwahl ab, ersetzt aber keine Prüfung auf Intel-Hardware.

## Voraussetzungen und bereits vorbereitete Pakete

Benötigt werden ein entsperrter Mac, .NET aus `global.json`, die Xcode Command Line Tools, Python 3, OpenSSL und das vorhandene Developer-ID-Zertifikat mit privatem Schlüssel. Für x64 muss Rosetta bereits installiert sein.

Die in dieser Sitzung erzeugten Pakete liegen hier:

- ARM: `/tmp/atlas-update-acceptance-arm64`
- x64: `/tmp/atlas-update-acceptance-x64`

Die Verzeichnisse unter `/tmp` sind temporär. Sind sie nicht mehr vorhanden, erzeugt das Hilfsprogramm sie erneut. Alle Befehle werden vom Repository-Hauptverzeichnis ausgeführt:

```shell
python3 tests/updater-fixtures.py prepare /tmp/atlas-update-acceptance-arm64 \
  --architecture arm64 --port 8781 \
  --identity "Developer ID Application: Lachmann Thiem Software GbR (2G3HGCLNFN)"

python3 tests/updater-fixtures.py prepare /tmp/atlas-update-acceptance-x64 \
  --architecture x64 --port 8782 \
  --identity "Developer ID Application: Lachmann Thiem Software GbR (2G3HGCLNFN)"
```

`prepare` verlangt ein leeres Verzeichnis. Bei einer vorhandenen, vorbereiteten Umgebung genügt später `reset`. Die Vorbereitung kopiert den aktuellen Quellstand, baut zwei Versionen, signiert beide einschließlich der Sparkle-Helfer und prüft ihre Signaturen. Der native Host bleibt derselbe wie im Produkt. Der Testscanner kann gezielt angehalten werden. Einstellungen und Verlauf liegen ausschließlich im jeweiligen Testverzeichnis.

Der Test verwendet eigene Bundle-Kennungen, temporäre Ed25519-Dateischlüssel und einen Feed auf `127.0.0.1`. Nur der Test-Bridge-Build erlaubt diesen lokalen HTTP-/ZIP-Aufbau. Produktpakete behalten die HTTPS-/DMG-Prüfungen. Die Testschlüssel gehören nicht in Releases; `prepare` erzeugt keinen Produktionsschlüssel und veröffentlicht nichts.

## Test-App und Feed starten

Ein Terminal für den Server geöffnet lassen:

```shell
python3 tests/updater-fixtures.py serve /tmp/atlas-update-acceptance-arm64
```

In einem zweiten Terminal:

```shell
UPDATER_TEST_DIR=/tmp/atlas-update-acceptance-arm64
python3 tests/updater-fixtures.py background-off "$UPDATER_TEST_DIR"
python3 tests/updater-fixtures.py scenario "$UPDATER_TEST_DIR" --mode valid
open "$UPDATER_TEST_DIR/installed/Atlas Update Test arm64.app"
```

Für x64 den Server mit `/tmp/atlas-update-acceptance-x64` starten, `UPDATER_TEST_DIR` darauf setzen und `Atlas Update Test x64.app` öffnen. Der x64-Host läuft auf Apple Silicon unter Rosetta. Beide Server können wegen der getrennten Ports gleichzeitig laufen.

Vor einem neuen Installationsversuch die Test-App beenden und zurücksetzen:

```shell
python3 tests/updater-fixtures.py allow "$UPDATER_TEST_DIR"
python3 tests/updater-fixtures.py reset "$UPDATER_TEST_DIR"
```

Das setzt nur das markierte Testpaket auf 1.0.0 zurück. Test-Einstellungen und Verlauf bleiben erhalten. Ein Reset bei laufender Test-App wird abgewiesen. Ein bereits vorbereitetes, genehmigtes Update kann beim normalen Beenden noch installiert werden; vor dem Reset muss der Installationsvorgang beendet sein.

## Prüffälle

### Erfolgreiches Update und Einstellungen

1. In der Test-App eine Einstellung ändern, etwa Include hidden files unter Options. Für Verlaufserhalt unter Options die Scan-Aufzeichnung aktivieren und den Ordner `scan-fixture` aus der Testumgebung scannen.
2. Im Anwendungsmenü **Check for Updates…** wählen. Angeboten werden muss 1.0.1 bei installierter 1.0.0.
3. **Install Update**, danach **Install and Relaunch** wählen.
4. Die App muss aus demselben `installed`-Ordner neu starten. Einstellung und aufgezeichneter Scan müssen erhalten bleiben. Der aktuelle, nur im Speicher gehaltene Scan wird geschlossen.
5. Die Installation prüfen, für x64 den App-Namen entsprechend ersetzen:

```shell
/usr/libexec/PlistBuddy -c 'Print :CFBundleVersion' \
  "$UPDATER_TEST_DIR/installed/Atlas Update Test arm64.app/Contents/Info.plist"
codesign --verify --deep --strict \
  "$UPDATER_TEST_DIR/installed/Atlas Update Test arm64.app"
```

Erwartet: `1.0.1` und erfolgreiche Signaturprüfung.

### Download abbrechen und erneut prüfen

1. App beenden und auf 1.0.0 zurücksetzen.
2. Den Download drosseln und App starten:

```shell
python3 tests/updater-fixtures.py scenario "$UPDATER_TEST_DIR" --mode slow
```

3. Manuell prüfen und **Install Update** wählen. Während **Downloading update…** **Cancel** drücken.
4. App und aktuelle Sitzung müssen weiterlaufen; Version bleibt 1.0.0. Der Server meldet `cancelled` für `/update.zip`, mit weniger übertragenen Bytes als der vollständigen Datei.
5. Erneut prüfen: dieselbe Version muss wieder angeboten werden. Danach das normale Szenario wiederherstellen:

```shell
python3 tests/updater-fixtures.py scenario "$UPDATER_TEST_DIR" --mode valid
```

### Ungültiger oder manipulierter Feed

Bei laufender App nacheinander umschalten und jeweils manuell prüfen:

```shell
python3 tests/updater-fixtures.py scenario "$UPDATER_TEST_DIR" --mode malformed
python3 tests/updater-fixtures.py scenario "$UPDATER_TEST_DIR" --mode tampered
python3 tests/updater-fixtures.py scenario "$UPDATER_TEST_DIR" --mode http-error
```

Zwischen den Befehlen den jeweiligen Prüfvorgang durchführen und den Fehlerdialog mit **Cancel Update** schließen.

- `malformed`: Ein korrekt signierter, syntaktisch ungültiger Feed muss einen Parse-Fehler auslösen.
- `tampered`: Eine nach dem Signieren veränderte Version muss einen Signaturfehler auslösen.
- `http-error`: Der Server antwortet mit HTTP 503; die manuelle Prüfung muss scheitern und erneutes Prüfen ermöglichen.

In keinem Fall darf ein Archiv installiert werden. Die App bleibt bedienbar. Anschließend mit `--mode valid` wiederherstellen und nochmals prüfen. Offline-Verhalten lässt sich zusätzlich durch Beenden des lokalen Servers mit Ctrl+C prüfen; danach Server neu starten und erneut prüfen. Dafür muss die reale Netzwerkverbindung nicht verändert werden.

### Hintergrundfehler bleiben still

1. Test-App beenden. Fehlerfeed wählen und ausschließlich für die eigene Testkennung einen fälligen automatischen Check setzen:

```shell
python3 tests/updater-fixtures.py scenario "$UPDATER_TEST_DIR" --mode http-error
python3 tests/updater-fixtures.py background-on "$UPDATER_TEST_DIR"
python3 tests/updater-fixtures.py busy "$UPDATER_TEST_DIR"
```

2. Test-App starten. Im Terminal muss ein `/appcast.xml`-Request erscheinen. Es darf kein Fehlerdialog erscheinen; der absichtlich angehaltene Scan bleibt aktiv.
3. Scan freigeben:

```shell
python3 tests/updater-fixtures.py idle "$UPDATER_TEST_DIR"
```

4. Der Scan muss erfolgreich abschließen. Danach App beenden und den Ablauf mit `--mode malformed` sowie `--mode tampered` wiederholen. `background-on` setzt den letzten Prüfzeitpunkt der Testkennung zurück; so muss keine 24-Stunden-Frist abgewartet werden.
5. Zum Schluss bei beendeter App `background-off` setzen und `--mode valid` wiederherstellen. Im Produkt bleibt das Minimum von 24 Stunden unverändert.

### Neustart bei laufendem Scan aufschieben

1. App beenden und auf 1.0.0 zurücksetzen. `busy` aktivieren und Test-App starten.
2. Das normale Update herunterladen und **Install and Relaunch** wählen.
3. Erwartet: **Update restart postponed**. Der Scan und Version 1.0.0 bleiben bestehen.
4. Mit `idle` den Scan freigeben. Nach seinem Abschluss darf die App nicht von allein neu starten.
5. Im Anwendungsmenü **Finish Installing Update…** wählen. Erst diese ausdrückliche Wiederholung darf Installation und Neustart auslösen.

### Installation wird vom Dateisystem verweigert

1. Test-App auf 1.0.0 zurücksetzen, normales Szenario wählen und App starten.
2. Die ausschließlich zum Test gehörende Ablage sperren:

```shell
python3 tests/updater-fixtures.py deny "$UPDATER_TEST_DIR"
```

3. Update herunterladen und Installation anfordern. Der Testordner ist schreibgeschützt und mit dem Benutzer-Dateisystemflag `uchg` gesperrt. Ein einfacher Schreibschutz allein reicht hier nicht, weil Sparkle ihn bei eigener Ordnerinhaberschaft handhaben kann.
4. Falls macOS eine Autorisierung anfordert, **Cancel** wählen; für diesen Negativtest kein Passwort eingeben. Erwartet ist eine verständliche Verweigerung oder Installationsfehlermeldung und unveränderte Version. Scheitert der Austausch erst nach dem Beenden der App, die alte App erneut öffnen: **The update installation did not complete** muss einmal erscheinen und die Wiederholung erklären. Einstellungen und Verlauf müssen erhalten bleiben. Für einen Fehler erst beim Austausch kann `deny` auch nach **Ready to Install** gesetzt werden; so bleibt die Download-Vorbereitung ungesperrt.
5. Testablage immer wieder freigeben:

```shell
python3 tests/updater-fixtures.py allow "$UPDATER_TEST_DIR"
```

6. App erneut öffnen, Version und Signatur prüfen und mit wiederhergestellten Rechten erneut installieren. Keine Berechtigungen der normalen Installation ändern.

## Store-Paket und Sandbox

Der unveränderte Produktionsbefehl bleibt:

```shell
./build-dmg.sh appstore both 1.0.903 \
  "Apple Distribution: Lachmann Thiem Software GbR (2G3HGCLNFN)" \
  "3rd Party Mac Developer Installer: Lachmann Thiem Software GbR (2G3HGCLNFN)"
```

Für einen späteren Release eine neue Version verwenden. Der Befehl benötigt keine Sparkle-Schlüssel oder zusätzlichen Entitlements. Er prüft beide Payloads auf Updater-Ausschluss. Das signierte Paket kann zusätzlich mit `pkgutil --check-signature` geprüft werden.

Eine Apple-Distribution-Signatur ist nicht gleichbedeutend mit einer lokal startbaren Entwicklungs-App. Dieser Mac weist die direkte Ausführung des Produktionsprofils mit `No matching profile found` ab. Ein tatsächlicher Store-Vertriebstest erfolgt deshalb über TestFlight/App Store oder mit einem passenden macOS-Entwicklungsprofil. Keine Sicherheitsprüfung abschalten.

Für den lokalen Sandbox-Smoke-Test lassen sich dieselben Store-Payloads isoliert mit den bestehenden Sandbox-Rechten und Developer ID signieren. Zunächst das bereits signierte Store-Bundle aus dem Build-Staging-Verzeichnis als Quelle verwenden:

```shell
python3 tests/updater-fixtures.py store /tmp/atlas-update-acceptance-x64 \
  --store-bundle "/Pfad/zum/gebauten/AtlasStorageAnalyzer.app" \
  --identity "Apple Distribution: Lachmann Thiem Software GbR (2G3HGCLNFN)"

python3 tests/updater-fixtures.py store-local /tmp/atlas-update-acceptance-x64 \
  --identity "Developer ID Application: Lachmann Thiem Software GbR (2G3HGCLNFN)"
```

`store` kopiert das Bundle und baut die aktuellen Store-Payloads mit getrennten Testdaten. `store-local` erstellt eine zweite Kopie mit eigener Testkennung und den unveränderten Sandbox-/JIT-/Dateiauswahl-Rechten aus den Store-Entitlement-Dateien. Das Produktionspaket bleibt erhalten. Diese lokale Variante ersetzt den späteren Test des eigentlichen Store-Vertriebs nicht.

Für die bereits vorbereitete lokale Variante:

```shell
STORE_TEST_APP=/tmp/atlas-update-acceptance-x64/store-local/AtlasStorageAnalyzer.app
arch -arm64 "$STORE_TEST_APP/Contents/MacOS/AtlasStorageAnalyzer"
```

Hauptfenster öffnen, Anwendungsmenü prüfen, dann beenden. Es dürfen weder **Check for Updates…** noch **Automatically Check for Updates** erscheinen. Für x64:

```shell
arch -x86_64 "$STORE_TEST_APP/Contents/MacOS/AtlasStorageAnalyzer"
```

Hauptfenster und Menü erneut prüfen. Beide Prozesse müssen aus demselben Universal-Bundle starten. Die x64-Ausführung läuft unter Rosetta. Die Sandbox-Rechte prüfen:

```shell
codesign --display --entitlements :- "$STORE_TEST_APP"
packaging/verify-no-updater.sh "$STORE_TEST_APP"
```

Erwartet: App Sandbox und JIT aktiviert, Dateiauswahl-Rechte wie bisher, keine Updater-Abhängigkeit und keine Update-Metadaten. Beim TestFlight-/Store-Build dieselben Menü- und Startprüfungen wiederholen, zusätzlich einen selbst gewählten Testordner scannen und die Store-gesteuerten Updates prüfen.

## Abnahme der echten Release-Dateien

Die lokalen Testpakete sind keine notarisierten Produktions-Releases. Vor Veröffentlichung zusätzlich:

1. Produktions-Ed25519-Schlüssel und Notarisierungsprofil gemäß [PACKAGING.md](PACKAGING.md) einrichten; privaten Schlüssel sichern.
2. Beide endgültigen Direct-DMGs bauen. `codesign`, `spctl`, Stapler und SHA-256-Prüfung müssen erfolgreich sein.
3. Finale DMG-Signaturen und signierte Appcasts prüfen. Assets zuerst bereitstellen, Feeds danach.
4. Den ersten updaterfähigen Release einmal manuell installieren. Beim nächsten Release das echte alte-zu-neue Update samt Einstellungen und Verlauf prüfen.
5. Auf einem separaten Benutzerkonto oder geeigneten Test-Mac den normalen Download und Gatekeeper-Start prüfen. Kein Bypass verwenden.

Das fehlende Intel-Gerät wird als Hardware-Lücke dokumentiert; die verfügbare x64-Abnahme erfolgt mit Rosetta. Aktuelle Ergebnisse und verbleibende Release-Voraussetzungen stehen im [OpenSpec-Nachweis](../openspec/changes/archive/2026-09-30-add-in-app-updates/spike-findings.md).

## Protokoll und Aufräumen

Je Fall Architektur, alte/neue Version, beobachteten Dialog, Ergebnis der Versions-/Signaturprüfung und Erhalt von Einstellungen/Verlauf notieren. Die Serverausgabe enthält Pfad, Query und User-Agent der Fixture-Anfragen. Es dürfen keine Scanpfade, Ergebnisse oder Profiling-Parameter übertragen werden.

Nach den Tests beide Test-Apps beenden, `allow`, `idle` und `background-off` für beide Umgebungen ausführen. Server mit Ctrl+C beenden. Die markierten Testverzeichnisse können anschließend entfernt werden; sie enthalten auch die temporären Testschlüssel. Produktionsschlüssel und normale App-Daten werden nicht berührt.
