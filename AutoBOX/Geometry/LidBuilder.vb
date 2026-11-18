' Option Strict Off: MultiEdgeFlanges.Add erwartet ein ByRef-Object-Array
' (Kantenliste) und die Kantensuche traversiert COM-Geometrie – beides
' erfordert spaetes Binden. Bewusste, lokal begrenzte Ausnahme.
Option Strict Off
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports SolidEdgeFramework
Imports SolidEdgePart
Imports SolidEdgeFrameworkSupport
Imports SolidEdgeGeometry

''' <summary>
''' Geometrie-Schicht: erzeugt den DECKEL in einem eigenen Blechdokument.
''' Ablauf: geschlossene Rechteckskizze (A = B-1, B = T-1) auf Oben (XY) ->
''' Grundlasche (Base Tab) -> 3 Mehrkantlappen (je 20 mm, nach aussen) ->
''' Abwicklung (Flat Pattern) mit Zuschnittgroesse.
''' </summary>
Public Class LidBuilder

    Private Const IG_LINE_START As Integer = 0
    Private Const IG_LINE_END As Integer = 1

    Private ReadOnly _log As Action(Of String)
    Private ReadOnly _app As SolidEdgeFramework.Application

    Public Sub New(app As SolidEdgeFramework.Application, log As Action(Of String))
        _app = app
        _log = If(log, Sub(t As String) System.Diagnostics.Debug.WriteLine(t))
    End Sub

    ''' <summary>Baut den kompletten Deckel in das uebergebene Dokument.</summary>
    Public Sub Baue(doc As SheetMetalDocument, p As BoxParameter)
        Dim s As Double = SheetMetalHelper.MaterialstaerkeMm(doc, _log)

        ' 1) Geschlossene Rechteckskizze + Grundlasche
        Dim profil As Profile = ZeichneRechteck(doc, p)
        Dim modell As Model = ErzeugeGrundlasche(doc, profil)
        _app.DoIdle()

        ' 2) 3 Mehrkantlappen (2x Tiefe-Seite, 1x Breite-Seite)
        ErzeugeMehrkantlappen(modell, p)

        ' 3) Abwicklung (Flat Pattern) inkl. Zuschnittgroesse
        If Not SheetMetalHelper.ErzeugeFlatPattern(doc, _app, _log) Then
            Throw New InvalidOperationException(
                "Die Abwicklung bzw. deren Zuschnittgroesse konnte nicht erzeugt werden - " &
                "eine Zeichnung wuerde ""0 mm x 0 mm"" anzeigen. Details siehe Protokoll.")
        End If

        _log(String.Format("Deckel fertig: Rechteck {0} x {1} mm, s={2} mm, 3 Flansche je {3} mm.",
                          p.DeckelSeiteXmm, p.DeckelSeiteYmm, s, BoxParameter.DeckelFlanschLaengeMm))
    End Sub

    ''' <summary>
    ''' Geschlossenes Rechteck, mittig auf dem Ursprung.
    ''' Seitenlaengen: X = B - 1 mm, Y = T - 1 mm. Vier Linien, per Keypoint
    ''' zur geschlossenen Kontur verbunden und mit igProfileClosed validiert.
    ''' </summary>
    Private Function ZeichneRechteck(doc As SheetMetalDocument, p As BoxParameter) As Profile
        Dim refPlaneOben As RefPlane = doc.RefPlanes.Item(1) ' Oben (XY)
        Dim profileSet As ProfileSet = doc.ProfileSets.Add()
        Dim profil As Profile = profileSet.Profiles.Add(refPlaneOben)
        Dim l2 As Lines2d = profil.Lines2d

        Dim hx As Double = (p.DeckelSeiteXmm / 2.0) * SheetMetalHelper.MmZuMeter
        Dim hy As Double = (p.DeckelSeiteYmm / 2.0) * SheetMetalHelper.MmZuMeter

        Dim eOben As Line2d = l2.AddBy2Points(-hx, hy, hx, hy)
        Dim eRechts As Line2d = l2.AddBy2Points(hx, hy, hx, -hy)
        Dim eUnten As Line2d = l2.AddBy2Points(hx, -hy, -hx, -hy)
        Dim eLinks As Line2d = l2.AddBy2Points(-hx, -hy, -hx, hy)

        Dim rel As Relations2d = CType(profil.Relations2d, Relations2d)
        rel.AddKeypoint(eOben, IG_LINE_END, eRechts, IG_LINE_START)
        rel.AddKeypoint(eRechts, IG_LINE_END, eUnten, IG_LINE_START)
        rel.AddKeypoint(eUnten, IG_LINE_END, eLinks, IG_LINE_START)
        rel.AddKeypoint(eLinks, IG_LINE_END, eOben, IG_LINE_START)

        Dim status As Integer = profil.[End](ProfileValidationType.igProfileClosed)
        If status <> 0 Then
            Throw New InvalidOperationException(
                "Die Rechteckvalidierung des Deckels ist fehlgeschlagen (Status " & status & ").")
        End If
        _log(String.Format("Deckel-Rechteck validiert: {0} x {1} mm (geschlossen).",
                          p.DeckelSeiteXmm, p.DeckelSeiteYmm))
        Return profil
    End Function

    ''' <summary>
    ''' Grundlasche (Base Tab) aus dem geschlossenen Rechteck.
    ''' API: Models.AddBaseTab(Profile, ExtentSide). igLeft = flaches Grundblech
    ''' der Materialstaerke (live verifiziert).
    ''' </summary>
    Private Function ErzeugeGrundlasche(doc As SheetMetalDocument, profil As Profile) As Model
        Dim modell As Model
        Try
            modell = doc.Models.AddBaseTab(profil, FeaturePropertyConstants.igLeft)
        Catch ex As Runtime.InteropServices.COMException
            Throw New InvalidOperationException(
                "AddBaseTab fehlgeschlagen (0x" & ex.ErrorCode.ToString("X8") & "): " & ex.Message, ex)
        End Try
        If modell Is Nothing Then Throw New InvalidOperationException("Grundlasche (Deckel) wurde nicht erzeugt.")
        _log("Deckel-Grundlasche erzeugt (AddBaseTab).")
        Return modell
    End Function

    ''' <summary>
    ''' Erzeugt die 3 Mehrkantlappen (je 20 mm, nach aussen/unten).
    ''' Zielkanten (Mittelpunkt der Deckel-Oberseite):
    '''   - rechte Tiefe-Seite:  X = +hx  (Kante laeuft in Y)
    '''   - linke  Tiefe-Seite:  X = -hx
    '''   - eine Breite-Seite:   Y = +hy  (Kante laeuft in X)
    ''' Nach JEDEM Flansch wird die Oberseite neu abgefragt und die naechste
    ''' Zielkante ueber ihre Position bestimmt (topologiesicher). Als eigenes
    ''' MultiEdgeFlange-Feature pro Kante -> drei getrennte Mehrkantlappen.
    ''' Je Lappen werden die geforderten Einstellungen gesetzt: Bemassung aussen
    ''' (Messpunkte), Materialseite "Biegung aussen", aktivierte Biegeausklinkung
    ''' (rechteckig) und Eckausklinkung vom Typ "Bieg. und Teilfl.".
    ''' </summary>
    Private Sub ErzeugeMehrkantlappen(modell As Model, p As BoxParameter)
        Dim hx As Double = (p.DeckelSeiteXmm / 2.0) * SheetMetalHelper.MmZuMeter
        Dim hy As Double = (p.DeckelSeiteYmm / 2.0) * SheetMetalHelper.MmZuMeter
        Dim laengeM As Double = BoxParameter.DeckelFlanschLaengeMm * SheetMetalHelper.MmZuMeter
        Dim tolM As Double = 0.5 * SheetMetalHelper.MmZuMeter

        ' Zielpositionen (X, Y) in Metern und sprechende Namen
        Dim ziele As New List(Of Tuple(Of Double, Double, String)) From {
            Tuple.Create(hx, 0.0, "rechte Tiefe-Seite"),
            Tuple.Create(-hx, 0.0, "linke Tiefe-Seite"),
            Tuple.Create(0.0, hy, "Breite-Seite")
        }

        Dim nummer As Integer = 0
        For Each ziel In ziele
            nummer += 1
            Dim body As Body = CType(modell.Body, Body)
            Dim oberseite As Face = SheetMetalHelper.HoechsteEbeneFlaeche(body)
            If oberseite Is Nothing Then
                _log("WARNUNG: Mehrkantlappen " & nummer & " (" & ziel.Item3 & "): keine Oberseite gefunden.")
                Continue For
            End If

            Dim kante As Edge = SheetMetalHelper.KanteAnPosition(oberseite, ziel.Item1, ziel.Item2, tolM)
            If kante Is Nothing Then
                _log("WARNUNG: Mehrkantlappen " & nummer & " (" & ziel.Item3 & "): keine passende Kante gefunden.")
                Continue For
            End If

            Try
                Dim edgeArr(0) As Object
                edgeArr(0) = kante

                ' Messpunkte -> "Bemassung Aussen": Die Flanschlaenge wird von der
                ' Aussenseite gemessen. DimSide hat KEINE nachtraeglich setzbare
                ' Eigenschaft und muss deshalb direkt beim Erzeugen uebergeben werden
                ' (igOutsideDimension, aus Part.tlb verifiziert).
                Dim flansch As MultiEdgeFlange = modell.MultiEdgeFlanges.Add(
                    1, edgeArr, FeaturePropertyConstants.igLeft, laengeM,
                    DimSide:=CObj(CInt(FeaturePropertyConstants.igOutsideDimension)))

                ' Restliche Einstellungen ueber die typisierten Feature-Eigenschaften
                ' (FlangeFeatureConstants, compilergeprueft und eindeutig):
                '  - Materialseite       -> "Biegung Aussen"
                '  - Biegeausklinkung     aktivieren (rechteckig)
                '  - Eckausklinkung      -> "Bieg. und Teilfl." (Biegung und Teilflaeche)
                flansch.FlangeMaterialSide = FlangeFeatureConstants.seFlangeBendOutside
                flansch.BendReliefType = FlangeFeatureConstants.seFlangeBendReliefSquare
                flansch.CornerReliefType = FlangeFeatureConstants.seFlangeBendAndFaceCornerRelief

                _app.DoIdle()
                _log("Mehrkantlappen " & nummer & " (" & ziel.Item3 & ") erzeugt, 20 mm nach aussen; " &
                     "Bemassung aussen, Biegung aussen, Biegeausklinkung + Eckausklinkung (Bieg. und Teilfl.).")
            Catch ex As Exception
                _log("WARNUNG: Mehrkantlappen " & nummer & " (" & ziel.Item3 & ") fehlgeschlagen: " & ex.Message)
            End Try
        Next
    End Sub

End Class
