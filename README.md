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

`AutoBOX.exe` (aus `AutoBOX\bin\Release\net48\`) in einen beliebigen Ordner kopieren und starten. Optional als EXE-Makro-Schaltfläche im Solid-Edge-Ribbon einbinden – `AutoBOX.ico`/`AutoBOX.png` im selben Ordner werden dann als Symbol verwendet.

## Bauen

```powershell
dotnet build AutoBOX.sln -c Release
```

Zum Bauen muss Solid Edge auf dem Rechner installiert sein.

- **Visual Studio / MSBuild.exe:** Die Interop-Typen werden über die COM-Verweise direkt aus der Solid-Edge-Installation erzeugt.
- **`dotnet build`:** Hier funktionieren COM-Verweise nicht. Die Interop-Assemblies werden aus `AutoBOX\lib\` erwartet. Sie sind **nicht im Repository enthalten**, weil sie aus den Typbibliotheken von Solid Edge erzeugt werden. Einmal mit Visual Studio/MSBuild.exe bauen und `AutoBOX\obj\Release\net48\Interop.*.dll` nach `AutoBOX\lib\` kopieren.

## Update-Verhalten

Derzeit **kein automatisches Update** – neue Versionen werden manuell durch Austausch der EXE verteilt.

## Release-Ablauf

1. Version in `AutoBOX\AutoBOX.vbproj` (`<Version>`) erhöhen.
2. `git commit -am "Release vX.Y.Z"`
3. `git tag vX.Y.Z`
4. `git push && git push --tags`
