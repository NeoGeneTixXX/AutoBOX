Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports SolidEdgeFramework
Imports SolidEdgePart

''' <summary>
''' Automatisierungs-Schicht (Orchestrierung): steuert den Gesamtablauf und
''' delegiert die eigentliche Geometrie an MainPartBuilder und LidBuilder.
''' Kennt selbst keine Skizzendetails – nur die Reihenfolge der Schritte.
'''
''' Fuer die Stapelverarbeitung (Batch) wird EINMAL mit Solid Edge verbunden,
''' danach werden alle Datensaetze nacheinander abgearbeitet und am Ende die
''' Verbindung freigegeben. Der Fehler eines einzelnen Datensatzes bricht den
''' Batch nicht ab – er wird protokolliert und der naechste Datensatz folgt.
'''
''' Alle Dokumente (Hauptteil und Deckel) werden aus der uebergebenen
''' Blechvorlage (*.psm) erzeugt. Damit stammen Materialstaerke, Biegeradius,
''' Werkstoff und Eigenschaften aus der Firmenvorlage statt aus der
''' Standardvorlage von Solid Edge.
''' </summary>
Public Class SolidEdgeAutomation

    Private Const PROGID_SHEETMETAL As String = "SolidEdge.SheetMetalDocument"

    Private ReadOnly _log As Action(Of String)

    ''' <summary>Pfad der Blechvorlage (*.psm); leer = Standardvorlage von Solid Edge.</summary>
    Private ReadOnly _vorlagePfad As String

    Public Sub New(log As Action(Of String))
        Me.New(log, String.Empty)
    End Sub

    ''' <param name="vorlagePfad">
    ''' Vollstaendiger Pfad der Blechvorlage (*.psm), aus der die Teile erzeugt
    ''' werden. Leer/Nothing = Standardvorlage von Solid Edge.
    ''' </param>
    Public Sub New(log As Action(Of String), vorlagePfad As String)
        _log = If(log, Sub(t As String) System.Diagnostics.Debug.WriteLine(t))
        _vorlagePfad = If(vorlagePfad, String.Empty).Trim()
    End Sub

    ''' <summary>
    ''' Verarbeitet eine ganze Liste von Datensaetzen in einem Rutsch:
    ''' einmal mit Solid Edge verbinden, alle Datensaetze abarbeiten, am Ende
    ''' freigeben. Fehler eines einzelnen Datensatzes brechen den Batch NICHT ab.
    ''' Der Rueckruf <paramref name="meldeErgebnis"/> wird nach jedem Datensatz
    ''' aufgerufen (Listenindex, Erfolg, Fehlermeldung), damit die GUI den Status
    ''' je Zeile anzeigen kann.
    ''' </summary>
    ''' <param name="parameterListe">Die abzuarbeitenden Datensaetze (bereits geprueft).</param>
    ''' <param name="meldeErgebnis">
    ''' Optionaler Rueckruf pro Datensatz: (index, erfolg, fehlermeldung).
    ''' </param>
    Public Sub ErzeugeBatch(parameterListe As IList(Of BoxParameter),
                            meldeErgebnis As Action(Of Integer, Boolean, String))
        If parameterListe Is Nothing OrElse parameterListe.Count = 0 Then
            _log("Keine Datensaetze zu verarbeiten.")
            Return
        End If

        ' Vorlage pruefen, BEVOR Solid Edge gestartet/belegt wird.
        Dim vorlagenFehler As String = Einstellungen.PruefeVorlage(_vorlagePfad)
        If vorlagenFehler IsNot Nothing Then
            Throw New InvalidOperationException(vorlagenFehler)
        End If

        If _vorlagePfad.Length = 0 Then
            _log("Vorlage: keine gewaehlt – es wird die Standardvorlage von Solid Edge verwendet.")
        Else
            _log("Vorlage: " & _vorlagePfad)
        End If

        ' OLE-Message-Filter: wiederholt COM-Aufrufe automatisch, die Solid Edge
        ' ablehnt, weil es gerade beschaeftigt ist (RPC_E_CALL_REJECTED).
        Using filter As New OleMessageFilter()
            ErzeugeBatchVerbunden(parameterListe, meldeErgebnis)
        End Using
    End Sub

    ''' <summary>Batch-Ablauf mit Solid-Edge-Verbindung (Message-Filter ist bereits aktiv).</summary>
    Private Sub ErzeugeBatchVerbunden(parameterListe As IList(Of BoxParameter),
                                      meldeErgebnis As Action(Of Integer, Boolean, String))
        Dim verbindung As New SolidEdgeConnection(_log)
        Dim app As SolidEdgeFramework.Application = verbindung.Verbinde()

        ' Modaldialoge waehrend der Automatisierung EINMAL unterdruecken (sie
        ' wuerden den Batch blockieren), danach wiederherstellen.
        Dim alertsVorher As Boolean = True
        Try
            alertsVorher = app.DisplayAlerts
        Catch
        End Try

        Try
            Try
                app.DisplayAlerts = False
            Catch
            End Try

            For i As Integer = 0 To parameterListe.Count - 1
                Dim nummer As Integer = i + 1
                Dim p As BoxParameter = parameterListe(i)
                Try
                    _log("--- Datensatz " & nummer & " von " & parameterListe.Count &
                         ": B=" & p.BreiteB & " T=" & p.TiefeT & " L=" & p.LaengeL &
                         " Deckel=" & If(p.MitDeckel, "ja", "nein"))
                    ErzeugeEinen(app, p)
                    _log("Datensatz " & nummer & " abgeschlossen.")
                    If meldeErgebnis IsNot Nothing Then meldeErgebnis(i, True, "")
                Catch ex As Exception
                    ' Einzelfehler protokollieren und mit dem naechsten Datensatz fortfahren.
                    _log("FEHLER bei Datensatz " & nummer & ": " & ex.Message)
                    If meldeErgebnis IsNot Nothing Then meldeErgebnis(i, False, ex.Message)
                End Try
            Next

            verbindung.AnsichtEinpassen(app)
            _log("Batch abgeschlossen.")
        Finally
            Try
                app.DisplayAlerts = alertsVorher
            Catch
            End Try
            SolidEdgeConnection.GibFrei(app)
        End Try
    End Sub

    ''' <summary>
    ''' Erzeugt fuer EINEN Datensatz das Hauptteil und – falls gewuenscht –
    ''' zusaetzlich den Deckel in einem zweiten Dokument. Die Solid-Edge-
    ''' Verbindung wird vom Aufrufer bereitgestellt und nicht hier geoeffnet.
    ''' </summary>
    Private Sub ErzeugeEinen(app As SolidEdgeFramework.Application, p As BoxParameter)
        Dim hauptDoc As SheetMetalDocument = Nothing
        Dim deckelDoc As SheetMetalDocument = Nothing
        Try
            ' --- Hauptteil ---
            hauptDoc = NeuesBlechdokument(app)
            _log("Hauptteil-Dokument erstellt (" & VorlagenBeschreibung() & ").")
            Dim hauptBuilder As New MainPartBuilder(app, _log)
            hauptBuilder.Baue(hauptDoc, p)

            ' --- Deckel (optional) ---
            If p.MitDeckel Then
                _log("Option 'Mit Deckel?' aktiv – zweites Dokument (Deckel) wird erstellt.")
                deckelDoc = NeuesBlechdokument(app)
                _log("Deckel-Dokument erstellt (" & VorlagenBeschreibung() & ").")
                Dim deckelBuilder As New LidBuilder(app, _log)
                deckelBuilder.Baue(deckelDoc, p)
            End If
        Finally
            SolidEdgeConnection.GibFrei(deckelDoc)
            SolidEdgeConnection.GibFrei(hauptDoc)
        End Try
    End Sub

    ''' <summary>
    ''' Legt ein neues Blechdokument an – aus der gewaehlten Vorlage (*.psm),
    ''' sonst aus der Standardvorlage von Solid Edge.
    ''' API: Documents.Add(ProgID, TemplateDoc); TemplateDoc ist der vollstaendige
    ''' Pfad der Vorlagendatei und darf weggelassen werden.
    ''' </summary>
    Private Function NeuesBlechdokument(app As SolidEdgeFramework.Application) As SheetMetalDocument
        If _vorlagePfad.Length = 0 Then
            Return CType(app.Documents.Add(PROGID_SHEETMETAL), SheetMetalDocument)
        End If

        Try
            Return CType(app.Documents.Add(PROGID_SHEETMETAL, _vorlagePfad), SheetMetalDocument)
        Catch ex As Exception
            Throw New InvalidOperationException(
                "Das Blechdokument konnte nicht aus der Vorlage """ & _vorlagePfad & """ erstellt werden: " &
                ex.Message, ex)
        End Try
    End Function

    ''' <summary>Kurztext fuer das Protokoll: welche Vorlage wurde verwendet.</summary>
    Private Function VorlagenBeschreibung() As String
        If _vorlagePfad.Length = 0 Then Return "Standardvorlage, " & PROGID_SHEETMETAL
        Return "Vorlage " & System.IO.Path.GetFileName(_vorlagePfad)
    End Function

End Class
