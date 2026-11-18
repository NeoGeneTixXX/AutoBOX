# AutoBOX

Blechteil-Generator für **Solid Edge**: Aus einer Tabelle mit Datensätzen (Breite B, Tiefe T, Länge L, optional Deckel) erzeugt AutoBOX per COM-Automatisierung automatisch Kantteile (`*.psm`) in Solid Edge – im Stapel, ein Datensatz nach dem anderen.

- Hauptteil und optionaler Deckel als getrennte Dokumente
- Erzeugung aus einer wählbaren Blechvorlage (`*.psm`) – Materialstärke, Biegeradius, Werkstoff kommen aus der Vorlage
- Fehler eines einzelnen Datensatzes brechen den Stapel nicht ab; Status je Zeile in der Tabelle
- Einstellungen (Vorlage, Schenkel S) unter `%AppData%\AutoBOX\einstellungen.txt`

## Voraussetzungen

- Windows 10/11 (x64), .NET Framework 4.8
- Solid Edge (64 Bit) installiert

## Installation

Unter [Releases](https://github.com/NeoGeneTixXX/AutoBOX/releases) das Setup `AutoBOX-win-Setup.exe` herunterladen und ausführen. AutoBOX wird ohne Administratorrechte nach `%LOCALAPPDATA%\AutoBOX\` installiert und erhält eine Verknüpfung im Startmenü.

Optional lässt sich AutoBOX als EXE-Makro-Schaltfläche im Solid-Edge-Ribbon einbinden. `AutoBOX.ico` bzw. `AutoBOX.png` im selben Ordner werden dann als Symbol verwendet.

## Bauen

```powershell
dotnet build AutoBOX.sln -c Release
```

Zum Bauen muss Solid Edge auf dem Rechner installiert sein.

- **Visual Studio / MSBuild.exe:** Die Interop-Typen werden über die COM-Verweise direkt aus der Solid-Edge-Installation erzeugt.
- **`dotnet build`:** Hier funktionieren COM-Verweise nicht. Die Interop-Assemblies werden aus `AutoBOX\lib\` erwartet. Sie sind **nicht im Repository enthalten**, weil sie aus den Typbibliotheken von Solid Edge erzeugt werden. Einmal mit Visual Studio/MSBuild.exe bauen und `AutoBOX\obj\Release\net48\Interop.*.dll` nach `AutoBOX\lib\` kopieren.

## Update-Verhalten

AutoBOX aktualisiert sich über [Velopack](https://velopack.io) aus den GitHub Releases dieses Repositorys:

- Beim Start prüft AutoBOX im Hintergrund, ob es eine neue Version gibt. Die Oberfläche bleibt dabei bedienbar.
- Gibt es eine, zeigt ein Dialog die installierte und die neue Version. Nach Bestätigung wird das Update heruntergeladen und installiert, danach startet AutoBOX neu.
- Ohne Internet, bei Fehlern oder wenn AutoBOX nicht über das Setup installiert ist (z. B. beim Debug-Start), startet das Programm normal. Der Grund steht dann nur im Log.
- Log-Dateien liegen unter `%LOCALAPPDATA%\AutoBOX\logs\`.

## Release-Ablauf

1. Version in `AutoBOX\AutoBOX.vbproj` (`<Version>`) erhöhen.
2. `git commit -am "Release vX.Y.Z"`
3. `git tag vX.Y.Z`
4. `git push && git push --tags`
5. GitHub Actions (`.github/workflows/release.yml`) baut das Release, packt es mit Velopack und veröffentlicht es. Installierte Programme finden es beim nächsten Start.

Die Version in der `.vbproj` muss zum Tag passen, sonst bricht der Workflow ab.

Der Workflow holt die Solid-Edge-Interop-Assemblies aus dem privaten Repository `NeoGeneTixXX/AutoBOX-interop`. Dafür braucht er das Repository-Secret **`INTEROP_TOKEN`**: einen Fine-grained Personal Access Token mit Lesezugriff (*Contents: Read-only*) nur auf `AutoBOX-interop`.
