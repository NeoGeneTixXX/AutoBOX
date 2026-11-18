Option Strict On
Option Explicit On

Imports System.Collections.Generic

''' <summary>
''' Reine Datenklasse (Model-Schicht): Eingabeparameter des Blechteils und die
''' daraus abgeleiteten Deckelmasse. Alle Laengen in Millimeter.
''' Enthaelt ausserdem die fachlichen Plausibilitaetspruefungen, damit GUI und
''' Automatisierung dieselben Regeln verwenden.
''' </summary>
Public Class BoxParameter

    ' --- Benutzereingaben -------------------------------------------------

    ''' <summary>Breite B [mm]. Bestimmt die obere Skizzenlinie des Hauptteils (B - 2*s).</summary>
    Public Property BreiteB As Double

    ''' <summary>Tiefe T [mm]. Bestimmt die senkrechten Skizzenlinien des Hauptteils (T - s).</summary>
    Public Property TiefeT As Double

    ''' <summary>Laenge L [mm]. Ausdehnung (Tiefe) des Konturlappens.</summary>
    Public Property LaengeL As Double

    ''' <summary>Option "Mit Deckel?" – erzeugt zusaetzlich ein zweites Blechdokument.</summary>
    Public Property MitDeckel As Boolean

    ''' <summary>
    ''' Schenkel S [mm]: Laenge der beiden unteren Flanschlinien am Hauptteil
    ''' (je S mm nach aussen). Gilt fuer den gesamten Batch.
    ''' </summary>
    Public Property SchenkelMm As Double = StandardSchenkelMm

    ' --- feste Konstruktionsvorgaben --------------------------------------

    ''' <summary>Vorbelegung des Schenkels S am Hauptteil [mm].</summary>
    Public Const StandardSchenkelMm As Double = 20.0

    ''' <summary>Laenge der drei Mehrkantlappen am Deckel [mm].</summary>
    Public Const DeckelFlanschLaengeMm As Double = 20.0

    ' --- abgeleitete Deckelmasse ------------------------------------------
    ' Vorgabe: Deckelrechteck A x B mit A = Breite - 1 mm, B = Tiefe - 1 mm.

    ''' <summary>Deckelrechteck-Seite entlang X [mm] = Breite B - 1.</summary>
    Public ReadOnly Property DeckelSeiteXmm As Double
        Get
            Return BreiteB - 1.0
        End Get
    End Property

    ''' <summary>Deckelrechteck-Seite entlang Y [mm] = Tiefe T - 1.</summary>
    Public ReadOnly Property DeckelSeiteYmm As Double
        Get
            Return TiefeT - 1.0
        End Get
    End Property

    ' --- Pruefungen --------------------------------------------------------

    ''' <summary>
    ''' Grundpruefung ohne Kenntnis der Materialstaerke (fuer den Dialog).
    ''' </summary>
    Public Function PruefeGrundwerte() As List(Of String)
        Dim fehler As New List(Of String)

        If BreiteB <= 0 Then fehler.Add("Breite (B) muss groesser als 0 mm sein.")
        If TiefeT <= 0 Then fehler.Add("Tiefe (T) muss groesser als 0 mm sein.")
        If LaengeL <= 0 Then fehler.Add("Laenge (L) muss groesser als 0 mm sein.")
        If SchenkelMm <= 0 Then fehler.Add("Schenkel (S) muss groesser als 0 mm sein.")

        If BreiteB > 10000.0 OrElse TiefeT > 10000.0 OrElse LaengeL > 10000.0 OrElse SchenkelMm > 10000.0 Then
            fehler.Add("Werte ueber 10000 mm sind nicht plausibel. Bitte Eingaben pruefen.")
        End If

        ' Deckelmasse muessen positiv sein
        If MitDeckel Then
            If DeckelSeiteXmm <= 0 Then fehler.Add("Fuer den Deckel muss Breite (B) groesser als 1 mm sein.")
        End If

        Return fehler
    End Function

    ''' <summary>
    ''' Geometriepruefung des Hauptteils, erst mit bekannter Materialstaerke moeglich.
    ''' </summary>
    Public Function PruefeMitMaterialstaerke(staerkeMm As Double) As List(Of String)
        Dim fehler As New List(Of String)

        If staerkeMm <= 0 Then
            fehler.Add("Die Materialstaerke aus dem Dokument ist ungueltig (" & staerkeMm & " mm).")
            Return fehler
        End If

        If BreiteB - 2.0 * staerkeMm <= 0 Then
            fehler.Add(String.Format(
                "Breite (B) = {0} mm ist zu klein: B - 2 x Materialstaerke ({1} mm) ergibt keine positive Linienlaenge.",
                BreiteB, staerkeMm))
        End If

        If TiefeT - staerkeMm <= 0 Then
            fehler.Add(String.Format(
                "Tiefe (T) = {0} mm ist zu klein: T - Materialstaerke ({1} mm) ergibt keine positive Linienlaenge.",
                TiefeT, staerkeMm))
        End If

        Return fehler
    End Function

End Class
