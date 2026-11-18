Option Strict On
Option Explicit On

Imports System
Imports System.Globalization
Imports System.IO
Imports System.Text

''' <summary>
''' Model-Schicht: dauerhafte Programmeinstellungen. Gespeichert werden die
''' Standardvorlage (Solid-Edge-Kantteil *.psm), aus der ALLE Blechteile
''' erzeugt werden – Hauptteil wie Deckel –, und der Schenkel S am Hauptteil.
'''
''' Ablage als einfache "Schluessel=Wert"-Textdatei unter
''' %AppData%\AutoBOX\einstellungen.txt, damit die zuletzt gewaehlten Werte
''' beim naechsten Programmstart wieder vorbelegt sind.
''' </summary>
Public Class Einstellungen

    Private Const SCHLUESSEL_VORLAGE As String = "Vorlage"
    Private Const SCHLUESSEL_SCHENKEL As String = "Schenkel"

    ''' <summary>Dateiendung einer Solid-Edge-Blechvorlage (Kantteil).</summary>
    Public Const VorlagenEndung As String = ".psm"

    ''' <summary>
    ''' Vollstaendiger Pfad der Blechvorlage (*.psm), aus der neue Dokumente
    ''' erzeugt werden. Leer = Standardvorlage von Solid Edge verwenden.
    ''' </summary>
    Public Property VorlagePfad As String = String.Empty

    ''' <summary>Zuletzt verwendeter Schenkel S am Hauptteil [mm].</summary>
    Public Property SchenkelMm As Double = BoxParameter.StandardSchenkelMm

    ''' <summary>Ordner der Einstellungsdatei (%AppData%\AutoBOX).</summary>
    Public Shared ReadOnly Property Ordner As String
        Get
            Return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AutoBOX")
        End Get
    End Property

    ''' <summary>Vollstaendiger Pfad der Einstellungsdatei.</summary>
    Public Shared ReadOnly Property Datei As String
        Get
            Return Path.Combine(Ordner, "einstellungen.txt")
        End Get
    End Property

    ''' <summary>
    ''' Laedt die Einstellungen. Eine fehlende oder defekte Datei ist unkritisch
    ''' und ergibt schlicht die Standardwerte.
    ''' </summary>
    Public Shared Function Laden() As Einstellungen
        Dim werte As New Einstellungen()
        Try
            If Not File.Exists(Datei) Then Return werte

            For Each zeile As String In File.ReadAllLines(Datei, Encoding.UTF8)
                Dim trenner As Integer = zeile.IndexOf("="c)
                If trenner <= 0 Then Continue For

                Dim schluessel As String = zeile.Substring(0, trenner).Trim()
                Dim wert As String = zeile.Substring(trenner + 1).Trim()

                If String.Equals(schluessel, SCHLUESSEL_VORLAGE, StringComparison.OrdinalIgnoreCase) Then
                    werte.VorlagePfad = wert
                ElseIf String.Equals(schluessel, SCHLUESSEL_SCHENKEL, StringComparison.OrdinalIgnoreCase) Then
                    Dim schenkel As Double
                    If Double.TryParse(wert, NumberStyles.Float, CultureInfo.InvariantCulture, schenkel) AndAlso
                       schenkel > 0 Then
                        werte.SchenkelMm = schenkel
                    End If
                End If
            Next
        Catch
            ' Defekte Einstellungsdatei darf den Programmstart nicht verhindern.
        End Try
        Return werte
    End Function

    ''' <summary>Speichert die Einstellungen (Schreibfehler sind unkritisch).</summary>
    Public Sub Speichern()
        Try
            Directory.CreateDirectory(Ordner)
            File.WriteAllLines(Datei,
                New String() {
                    SCHLUESSEL_VORLAGE & "=" & If(VorlagePfad, String.Empty),
                    SCHLUESSEL_SCHENKEL & "=" & SchenkelMm.ToString(CultureInfo.InvariantCulture)},
                Encoding.UTF8)
        Catch
            ' z. B. fehlende Schreibrechte – ohne gespeicherte Einstellung weiterarbeiten.
        End Try
    End Sub

    ''' <summary>
    ''' Prueft einen Vorlagenpfad, bevor Solid Edge damit beauftragt wird.
    ''' Ein leerer Pfad ist zulaessig und bedeutet "Standardvorlage von Solid Edge".
    ''' Rueckgabe: Nothing wenn alles in Ordnung ist, sonst der Fehlertext.
    ''' </summary>
    Public Shared Function PruefeVorlage(pfad As String) As String
        If String.IsNullOrWhiteSpace(pfad) Then Return Nothing

        Dim endung As String = String.Empty
        Try
            endung = Path.GetExtension(pfad)
        Catch ex As ArgumentException
            Return "Der Vorlagenpfad ist ungueltig: " & pfad
        End Try

        If Not String.Equals(endung, VorlagenEndung, StringComparison.OrdinalIgnoreCase) Then
            Return "Die Vorlage muss ein Solid-Edge-Kantteil (*" & VorlagenEndung & ") sein: " & pfad
        End If

        If Not File.Exists(pfad) Then
            Return "Die Vorlagendatei wurde nicht gefunden: " & pfad
        End If

        Return Nothing
    End Function

End Class
