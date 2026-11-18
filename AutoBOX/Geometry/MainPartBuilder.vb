Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports SolidEdgeFramework
Imports SolidEdgePart
Imports SolidEdgeFrameworkSupport

''' <summary>
''' Geometrie-Schicht: erzeugt den HAUPTTEIL in einem bestehenden Blechdokument.
''' Ablauf: Skizze auf Oben (XY) -> Konturlappen nach aussen (Laenge L) ->
''' Abwicklung (Flat Pattern) mit Zuschnittgroesse.
''' </summary>
Public Class MainPartBuilder

    ' KeypointIndexConstants (Constant.tlb, verifiziert): Linienanfang/-ende
    Private Const IG_LINE_START As Integer = 0
    Private Const IG_LINE_END As Integer = 1

    Private ReadOnly _log As Action(Of String)
    Private ReadOnly _app As SolidEdgeFramework.Application

    Public Sub New(app As SolidEdgeFramework.Application, log As Action(Of String))
        _app = app
        _log = If(log, Sub(t As String) System.Diagnostics.Debug.WriteLine(t))
    End Sub

    ''' <summary>Baut den kompletten Hauptteil in das uebergebene Dokument.</summary>
    Public Sub Baue(doc As SheetMetalDocument, p As BoxParameter)
        ' 1) Blechparameter aus dem Dokument
        Dim s As Double = SheetMetalHelper.MaterialstaerkeMm(doc, _log)
        Dim r As Double = SheetMetalHelper.BiegeradiusMm(doc, s, _log)

        ' 2) Geometriepruefung mit bekannter Materialstaerke
        Dim fehler As List(Of String) = p.PruefeMitMaterialstaerke(s)
        If fehler.Count > 0 Then
            Throw New InvalidOperationException(String.Join(System.Environment.NewLine, fehler))
        End If

        ' 3) Skizze zeichnen + Konturlappen
        Dim profil As Profile = ZeichneProfil(doc, p, s)
        ErzeugeKonturlappen(doc, profil, p.LaengeL, r)
        _app.DoIdle()

        ' 4) Abwicklung (Flat Pattern) inkl. Zuschnittgroesse
        If Not SheetMetalHelper.ErzeugeFlatPattern(doc, _app, _log) Then
            Throw New InvalidOperationException(
                "Die Abwicklung bzw. deren Zuschnittgroesse konnte nicht erzeugt werden - " &
                "eine Zeichnung wuerde ""0 mm x 0 mm"" anzeigen. Details siehe Protokoll.")
        End If

        _log(String.Format("Hauptteil fertig: B={0} mm, T={1} mm, L={2} mm, s={3} mm.",
                          p.BreiteB, p.TiefeT, p.LaengeL, s))
    End Sub

    ''' <summary>
    ''' Offenes Profil auf Oben (XY):
    '''  - obere horizontale Linie, symmetrisch zum Ursprung, Laenge B - 2*s
    '''  - je eine senkrechte Linie nach unten, Laenge T - s
    '''  - je eine Linie der Laenge Schenkel S nach aussen an den unteren Enden
    ''' Alle Linien werden per Keypoint-Beziehung zur zusammenhaengenden Kette
    ''' verbunden (sonst schlaegt die Profilvalidierung fehl).
    ''' </summary>
    Private Function ZeichneProfil(doc As SheetMetalDocument, p As BoxParameter, staerkeMm As Double) As Profile
        Dim refPlaneOben As RefPlane = doc.RefPlanes.Item(1) ' 1 = Oben (XY)
        _log("Skizzierebene: Oben (XY) [RefPlanes.Item(1)]")

        Dim profileSet As ProfileSet = doc.ProfileSets.Add()
        Dim profil As Profile = profileSet.Profiles.Add(refPlaneOben)
        Dim lines2d As Lines2d = profil.Lines2d

        Dim halbeBreiteM As Double = (p.BreiteB / 2.0 - staerkeMm) * SheetMetalHelper.MmZuMeter
        Dim hoeheM As Double = (p.TiefeT - staerkeMm) * SheetMetalHelper.MmZuMeter
        Dim flanschM As Double = p.SchenkelMm * SheetMetalHelper.MmZuMeter

        Dim linieOben As Line2d = lines2d.AddBy2Points(-halbeBreiteM, 0.0, halbeBreiteM, 0.0)
        Dim linieRechts As Line2d = lines2d.AddBy2Points(halbeBreiteM, 0.0, halbeBreiteM, -hoeheM)
        Dim linieLinks As Line2d = lines2d.AddBy2Points(-halbeBreiteM, 0.0, -halbeBreiteM, -hoeheM)
        Dim flanschRechts As Line2d = lines2d.AddBy2Points(halbeBreiteM, -hoeheM, halbeBreiteM + flanschM, -hoeheM)
        Dim flanschLinks As Line2d = lines2d.AddBy2Points(-halbeBreiteM, -hoeheM, -halbeBreiteM - flanschM, -hoeheM)

        Dim relations As Relations2d = CType(profil.Relations2d, Relations2d)
        relations.AddKeypoint(linieOben, IG_LINE_END, linieRechts, IG_LINE_START)
        relations.AddKeypoint(linieOben, IG_LINE_START, linieLinks, IG_LINE_START)
        relations.AddKeypoint(linieRechts, IG_LINE_END, flanschRechts, IG_LINE_START)
        relations.AddKeypoint(linieLinks, IG_LINE_END, flanschLinks, IG_LINE_START)

        _log(String.Format("Skizze: obere Linie {0} mm, senkrechte Linien {1} mm, Schenkel je {2} mm.",
                          p.BreiteB - 2.0 * staerkeMm, p.TiefeT - staerkeMm, p.SchenkelMm))

        Dim status As Integer = profil.[End](ProfileValidationType.igProfileSingle)
        If status <> 0 Then
            Throw New InvalidOperationException(
                "Die Profilvalidierung des Hauptteils ist fehlgeschlagen (Status " & status & ").")
        End If
        _log("Hauptteil-Skizze als offenes Profil validiert (Status 0).")
        Return profil
    End Function

    ''' <summary>
    ''' Konturlappen als Basis-Feature. Signatur aus Part.tlb (Solid Edge 2026),
    ''' live verifiziert. Materialseite igRight = Material nach aussen -> Aussenmasse B x T.
    ''' Ausdehnung igFinite/igRight ergab beim Test exakt Z = 0..L.
    ''' </summary>
    Private Sub ErzeugeKonturlappen(doc As SheetMetalDocument, profil As Profile, laengeMm As Double, biegeradiusMm As Double)
        Dim modell As Model
        Try
            modell = doc.Models.AddBaseContourFlange(
                profil,
                CObj(CInt(FeaturePropertyConstants.igRight)),  ' Materialseite: nach aussen
                CObj(CInt(FeaturePropertyConstants.igFinite)), ' endliche Ausdehnung
                CObj(CInt(FeaturePropertyConstants.igRight)),  ' Richtung +Z
                CObj(laengeMm * SheetMetalHelper.MmZuMeter),    ' Ausdehnung = Laenge L
                CObj(biegeradiusMm * SheetMetalHelper.MmZuMeter))
        Catch ex As Runtime.InteropServices.COMException
            Throw New InvalidOperationException(
                "AddBaseContourFlange fehlgeschlagen (0x" & ex.ErrorCode.ToString("X8") & "): " & ex.Message, ex)
        End Try
        If modell Is Nothing Then Throw New InvalidOperationException("Konturlappen wurde nicht erzeugt.")
        _log("Konturlappen erzeugt: L = " & laengeMm & " mm, Biegeradius = " & biegeradiusMm & " mm.")
    End Sub

End Class
