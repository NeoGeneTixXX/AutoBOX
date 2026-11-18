' Option Strict Off: Die Geometrie-Traversierung (Body.Faces/Edges/Vertices,
' GetNormal/GetPointData/GetEndPoints) nutzt COM-Objektsammlungen und ByRef-
' Array-Ausgabeparameter, die spaetes Binden erfordern. Bewusste, lokal
' begrenzte Ausnahme – der Rest des Projekts laeuft mit Option Strict On.
Option Strict Off
Option Explicit On

Imports System
Imports SolidEdgePart
Imports SolidEdgeGeometry

''' <summary>
''' Geometrie-Schicht (gemeinsame Helfer): Zugriff auf Blechparameter,
''' Auswahl von Flaechen/Kanten aus der Koerpergeometrie, Erzeugung des
''' Flat Patterns (Abwicklung) samt Zuschnittgroesse.
''' Wird sowohl vom Hauptteil- als auch vom Deckel-Builder verwendet.
''' </summary>
Module SheetMetalHelper

    ''' <summary>Umrechnung Millimeter -> Meter (API-Einheit).</summary>
    Public Const MmZuMeter As Double = 1.0 / 1000.0

    ' =======================================================================
    ' Blechparameter aus dem Dokument lesen
    ' =======================================================================

    ''' <summary>
    ''' Materialstaerke aus dem Blechdokument in mm.
    ''' Offizielle API: SheetMetalDocument.GetGlobalParameter mit
    ''' seSheetMetalGlobalMaterialThickness (= 4, aus Part.tlb ausgelesen und
    ''' live verifiziert). Fallback 2 mm, falls kein plausibler Wert.
    ''' </summary>
    Public Function MaterialstaerkeMm(doc As SheetMetalDocument, log As Action(Of String)) As Double
        Try
            Dim wert As Object = Nothing
            doc.GetGlobalParameter(SheetMetalGlobalConstants.seSheetMetalGlobalMaterialThickness, wert)
            Dim m As Double = CDbl(wert)
            If IstPlausibel(m) Then
                Dim mm As Double = m * 1000.0
                log("Materialstaerke aus Dokument: " & mm.ToString("0.###") & " mm")
                Return mm
            End If
        Catch ex As Exception
            log("WARNUNG: Materialstaerke nicht lesbar: " & ex.Message)
        End Try
        log("Fallback-Materialstaerke 2 mm wird verwendet.")
        Return 2.0
    End Function

    ''' <summary>
    ''' Standard-Biegeradius aus dem Blechdokument in mm.
    ''' Fallback: Materialstaerke (Werkstattregel r = s).
    ''' </summary>
    Public Function BiegeradiusMm(doc As SheetMetalDocument, staerkeMm As Double, log As Action(Of String)) As Double
        Try
            Dim wert As Object = Nothing
            doc.GetGlobalParameter(SheetMetalGlobalConstants.seSheetMetalGlobalBendRadius, wert)
            Dim m As Double = CDbl(wert)
            If IstPlausibel(m) Then
                Dim mm As Double = m * 1000.0
                log("Biegeradius aus Dokument: " & mm.ToString("0.###") & " mm")
                Return mm
            End If
        Catch ex As Exception
            log("WARNUNG: Biegeradius nicht lesbar: " & ex.Message)
        End Try
        log("Fallback-Biegeradius = Materialstaerke (" & staerkeMm & " mm).")
        Return staerkeMm
    End Function

    Private Function IstPlausibel(wertMeter As Double) As Boolean
        Return wertMeter > 0.0001 AndAlso wertMeter < 0.05
    End Function

    ' =======================================================================
    ' Abwicklung (Flat Pattern)
    ' =======================================================================

    ''' <summary>
    ''' Erzeugt die ABWICKLUNG (Flat Pattern) im Blechdokument und laesst
    ''' anschliessend die ZUSCHNITTGROESSE berechnen. Drei fachliche Schritte:
    '''   1) FlatPatternModels.Add(DesignModel) -> legt das Abwicklungsmodell
    '''      (den "Abwicklung"-Knoten) an. Dieser Schritt ALLEIN erzeugt nur den
    '''      LEEREN Container ohne Abwicklungs-Feature.
    '''   2) FlatPatternModel.FlatPatterns.Add(Kante, Flaeche, Eckpunkt, ModelType)
    '''      -> erzeugt das eigentliche Abwicklungs-Feature ("1 Abwicklung").
    '''   3) FlatPatternModel.UpdateCutSize() -> berechnet die Zuschnittgroesse.
    '''      Ohne diesen Schritt bleibt die Zuschnittgroesse 0 (siehe
    '''      AktualisiereZuschnittgroesse).
    ''' Die Referenzgeometrie fuer Schritt 2 stammt aus dem KOERPER DES
    ''' ABWICKLUNGSMODELLS (fpm.Body), nicht aus dem Designmodell: groesste ebene
    ''' Flaeche, deren erste Kante und ein Eckpunkt dieser Kante.
    ''' Rueckgabe False = Abwicklung oder Zuschnittgroesse fehlt; der Aufrufer
    ''' macht daraus einen Fehler des Datensatzes.
    ''' </summary>
    Public Function ErzeugeFlatPattern(doc As SheetMetalDocument,
                                       app As SolidEdgeFramework.Application,
                                       log As Action(Of String)) As Boolean
        Dim fpm As FlatPatternModel = Nothing
        Dim fp As FlatPattern = Nothing
        Try
            ' 1) Abwicklungsmodell holen oder anlegen (nicht doppelt anlegen).
            If doc.FlatPatternModels.Count = 0 Then
                fpm = doc.FlatPatternModels.Add(doc.Models.Item(1))
            Else
                fpm = doc.FlatPatternModels.Item(1)
            End If

            If fpm.FlatPatterns.Count > 0 Then
                ' Es gibt bereits ein Abwicklungs-Feature (z. B. aus der Firmenvorlage
                ' oder aus einem abgebrochenen Lauf). Ein zweites Add wuerde scheitern –
                ' und damit auch die Zuschnittberechnung. Also weiterverwenden.
                fp = fpm.FlatPatterns.Item(1)
                log("Abwicklung war bereits vorhanden und wird weiterverwendet.")
            Else
                ' 2) Referenzgeometrie aus dem Koerper des Abwicklungsmodells.
                Dim body As Body = CType(fpm.Body, Body)
                Dim face As Face = GroessteEbeneFlaeche(body)
                If face Is Nothing Then
                    log("WARNUNG: Keine ebene Flaeche im Abwicklungsmodell gefunden; Abwicklung nicht erzeugt.")
                    Return False
                End If
                Dim edge As Edge = ErsteKante(face)
                If edge Is Nothing Then
                    log("WARNUNG: Referenzflaeche hat keine Kante; Abwicklung nicht erzeugt.")
                    Return False
                End If
                Dim vtx As Object = edge.EndVertex
                If vtx Is Nothing Then
                    log("WARNUNG: Referenzkante hat keinen Eckpunkt; Abwicklung nicht erzeugt.")
                    Return False
                End If

                ' 3) Abwicklungs-Feature erzeugen ("Alles abwickeln").
                fp = fpm.FlatPatterns.Add(edge, face, vtx,
                                          FlattenPatternModelTypeConstants.igFlattenPatternModelTypeFlattenAnything)
                log("Abwicklung erzeugt (FlatPatternModel + FlatPatterns.Add).")
                If app IsNot Nothing Then app.DoIdle()
            End If
        Catch ex As Exception
            log("WARNUNG: Abwicklung konnte nicht erzeugt werden: " & ex.Message)
            Return False
        End Try

        ' 4) Zuschnittgroesse berechnen – erst damit ist die Abwicklung fuer eine
        '    Zeichnung brauchbar. Das Ergebnis entscheidet ueber Erfolg/Misserfolg.
        Return AktualisiereZuschnittgroesse(doc, fpm, fp, app, log)
    End Function
    ''' <summary>
    ''' Traegt die ZUSCHNITTGROESSE dort ein, wo eine Zeichnung sie ausliest:
    ''' in den Systemvariablen "Flat_Pattern_Model_CutSizeX/Y".
    '''
    ''' Am lebenden Modell gemessen (Solid Edge 2026, Vorlage Kantteil.psm):
    '''  - FlatPatternModel.GetCutSize liefert den richtigen Wert sofort nach
    '''    FlatPatterns.Add; er wird live aus der Abwicklungsgeometrie gerechnet.
    '''  - Die Variablen bleiben davon unberuehrt auf 0 und sind schreibgeschuetzt
    '''    (ReadOnly = True) - ein direktes Setzen wird ohne Fehler verworfen.
    '''    Weder fpm.Update noch doc.Recompute noch UpdateCutSize fuellen sie.
    '''  - ERST FlatPattern.SetCutSizeValues traegt die Werte ein. Das ist der
    '''    Befehl hinter dem Dialog "Zuschnittgroesse fuer Abwicklung"; der
    '''    Abwicklungsbefehl in der Oberflaeche fuehrt ihn mit aus, die reine
    '''    API-Erzeugung ueber FlatPatterns.Add nicht.
    '''
    ''' Uebergeben wird der gemessene Ist-Zuschnitt. Umgrenzungsbox und die
    ''' beiden Verletzungswarnungen bleiben aus, damit im Modell nichts aufpoppt.
    ''' Rueckgabe False = Zuschnitt fehlt weiterhin.
    ''' </summary>
    Private Function AktualisiereZuschnittgroesse(doc As SheetMetalDocument,
                                                  fpm As FlatPatternModel,
                                                  fp As FlatPattern,
                                                  app As SolidEdgeFramework.Application,
                                                  log As Action(Of String)) As Boolean
        Dim name As String = Dokumentname(doc)

        ' 1) Ist-Zuschnitt am Modell ermitteln. UpdateCutSize ist Absicherung -
        '    GetCutSize liefert den Wert erfahrungsgemaess auch ohne diesen Aufruf.
        Try
            fpm.UpdateCutSize()
            If app IsNot Nothing Then app.DoIdle()
        Catch ex As Exception
            log("Hinweis: UpdateCutSize fehlgeschlagen: " & ex.Message)
        End Try

        Dim xMeter As Double = 0.0
        Dim yMeter As Double = 0.0
        Try
            fpm.GetCutSize(xMeter, yMeter)
        Catch ex As Exception
            log("WARNUNG: " & name & ": Zuschnittgroesse nicht lesbar: " & ex.Message)
            Return False
        End Try

        If xMeter <= 0.000001 OrElse yMeter <= 0.000001 Then
            log("WARNUNG: " & name & ": Das Modell liefert keine Zuschnittgroesse (0 x 0).")
            Return False
        End If

        ' 2) Werte in die Variablen eintragen lassen.
        If Not SetzeZuschnittwerte(fp, xMeter, yMeter, app, log) Then Return False

        If Not ZuschnittVariablenGefuellt(doc) Then
            ' Manche Vorlagen bringen keine zulaessige Rohteilgroesse mit
            ' (GetDefaultCutSizeValues = 0 x 0). Einmal setzen und erneut versuchen.
            Try
                doc.SetDefaultCutSizeValues(xMeter, yMeter)
            Catch ex As Exception
                log("Hinweis: SetDefaultCutSizeValues fehlgeschlagen: " & ex.Message)
            End Try
            SetzeZuschnittwerte(fp, xMeter, yMeter, app, log)

            If Not ZuschnittVariablenGefuellt(doc) Then
                log("WARNUNG: " & name & ": Der Zuschnitt steht nicht in den Variablen " &
                    "Flat_Pattern_Model_CutSizeX/Y - eine Zeichnung wuerde 0 mm x 0 mm anzeigen.")
                Return False
            End If
        End If

        log(String.Format("Zuschnitt {0}: {1} mm x {2} mm",
                          name, (xMeter * 1000.0).ToString("0.##"), (yMeter * 1000.0).ToString("0.##")))
        Return True
    End Function

    ''' <summary>
    ''' Traegt die Zuschnittwerte am Abwicklungs-Feature ein.
    ''' Signatur: SetCutSizeValues(MaxCutSizeX, MaxCutSizeY, ShowRangeBox,
    ''' AlarmOnX, AlarmOnY, UseDefaultValues) - die drei Schalter bleiben aus,
    ''' damit weder eine Umgrenzungsbox noch eine Warnung erscheint.
    ''' </summary>
    Private Function SetzeZuschnittwerte(fp As FlatPattern,
                                         xMeter As Double, yMeter As Double,
                                         app As SolidEdgeFramework.Application,
                                         log As Action(Of String)) As Boolean
        Try
            fp.SetCutSizeValues(xMeter, yMeter, False, False, False, False)
            If app IsNot Nothing Then app.DoIdle()
            Return True
        Catch ex As Exception
            log("WARNUNG: Zuschnittgroesse konnte nicht gesetzt werden: " & ex.Message)
            Return False
        End Try
    End Function

    ''' <summary>True, wenn beide Ist-Zuschnittvariablen einen Wert groesser 0 tragen.</summary>
    Private Function ZuschnittVariablenGefuellt(doc As SheetMetalDocument) As Boolean
        Dim x As Double = 0.0
        Dim y As Double = 0.0
        If Not LiesZuschnittVariablen(doc, x, y) Then Return False
        Return x > 0.000001 AndAlso y > 0.000001
    End Function

    ''' <summary>
    ''' Liest die beiden Ist-Zuschnittvariablen (Werte in Metern).
    ''' Rueckgabe False, wenn sie nicht gefunden wurden.
    ''' </summary>
    Private Function LiesZuschnittVariablen(doc As SheetMetalDocument,
                                            ByRef xMeter As Double,
                                            ByRef yMeter As Double) As Boolean
        xMeter = 0.0
        yMeter = 0.0
        Dim x As Object = FindeVariable(doc, "Flat_Pattern_Model_CutSizeX")
        Dim y As Object = FindeVariable(doc, "Flat_Pattern_Model_CutSizeY")
        If x Is Nothing OrElse y Is Nothing Then Return False
        Try
            xMeter = CDbl(x.Value)
            yMeter = CDbl(y.Value)
        Catch
            Return False
        End Try
        Return True
    End Function

    ''' <summary>Sucht eine Variable ueber ihren Solid-Edge-Systemnamen.</summary>
    Private Function FindeVariable(doc As SheetMetalDocument, sysName As String) As Object
        Try
            Dim vars As Object = doc.Variables

            Dim liste As Object = Nothing
            Try
                liste = vars.Query("*")
                If CInt(liste.Count) = 0 Then liste = Nothing
            Catch
                liste = Nothing
            End Try
            If liste Is Nothing Then liste = vars

            For i As Integer = 1 To CInt(liste.Count)
                Try
                    Dim v As Object = liste.Item(i)
                    Dim n As String = String.Empty
                    Try
                        n = CStr(vars.GetSystemName(v))
                    Catch
                    End Try
                    If String.Equals(n, sysName, StringComparison.OrdinalIgnoreCase) Then Return v
                Catch
                    ' einzelne Variable nicht lesbar - weiter
                End Try
            Next
        Catch
            ' Variablentabelle nicht erreichbar
        End Try
        Return Nothing
    End Function

    ''' <summary>Fenstername des Dokuments fuer das Protokoll (Fehler unkritisch).</summary>
    Private Function Dokumentname(doc As SheetMetalDocument) As String
        Try
            Return doc.Name
        Catch
            Return "Blechteil"
        End Try
    End Function

    ' =======================================================================
    ' Flaechen-/Kantenauswahl aus der Koerpergeometrie
    ' =======================================================================

    ''' <summary>
    ''' Groesste ebene Flaeche des Koerpers; bei Flaechengleichheit die hoehere
    ''' (groesseres Z). Geeignet als Referenzflaeche fuer die Abwicklung.
    ''' </summary>
    Public Function GroessteEbeneFlaeche(body As Body) As Face
        Dim faces As Object = body.Faces(SolidEdgeGeometry.FeatureTopologyQueryTypeConstants.igQueryPlane)
        Dim best As Face = Nothing
        Dim bestA As Double = -1.0
        Dim bestZ As Double = Double.MinValue
        For i As Integer = 1 To CInt(faces.Count)
            Dim f As Face = CType(faces.Item(i), Face)
            Dim z As Double = FlaechenMittelZ(f)
            If f.Area > bestA + 0.0000001 OrElse
               (Math.Abs(f.Area - bestA) < 0.0000001 AndAlso z > bestZ) Then
                best = f : bestA = f.Area : bestZ = z
            End If
        Next
        Return best
    End Function

    ''' <summary>
    ''' Hoechste, nach oben zeigende ebene Flaeche (Deckel-Oberseite). Dient als
    ''' Grundlage, um die Kanten fuer die Mehrkantlappen zu bestimmen.
    ''' </summary>
    Public Function HoechsteEbeneFlaeche(body As Body) As Face
        Dim faces As Object = body.Faces(SolidEdgeGeometry.FeatureTopologyQueryTypeConstants.igQueryPlane)
        Dim best As Face = Nothing
        Dim bestZ As Double = Double.MinValue
        For i As Integer = 1 To CInt(faces.Count)
            Dim f As Face = CType(faces.Item(i), Face)
            Dim n(2) As Double
            Dim params() As Double = New Double() {0.5, 0.5}
            f.GetNormal(1, params, n)
            If n(2) < 0.9 Then Continue For ' nur nahezu waagerechte, nach oben zeigende Flaechen
            Dim z As Double = FlaechenMittelZ(f)
            If z > bestZ + 0.0000001 Then best = f : bestZ = z
        Next
        Return best
    End Function

    ''' <summary>Mittlere Z-Hoehe einer Flaeche ueber ihre Eckpunkte [m].</summary>
    Public Function FlaechenMittelZ(f As Face) As Double
        Dim vs As Object = f.Vertices
        Dim zsum As Double = 0.0
        Dim n As Integer = 0
        For j As Integer = 1 To CInt(vs.Count)
            Dim p(2) As Double
            vs.Item(j).GetPointData(p)
            zsum += p(2) : n += 1
        Next
        Return If(n > 0, zsum / n, 0.0)
    End Function

    ''' <summary>
    ''' Findet in einer Flaeche die Kante, deren Mittelpunkt (X/Y) der Zielposition
    ''' entspricht. Wird beim Deckel genutzt, um nach jedem Flansch die naechste
    ''' Zielkante neu (topologiesicher) zu bestimmen. Positionen in Metern.
    ''' </summary>
    Public Function KanteAnPosition(f As Face, zielXm As Double, zielYm As Double, tolMeter As Double) As Edge
        Dim es As Object = f.Edges
        For i As Integer = 1 To CInt(es.Count)
            Dim ed As Edge = CType(es.Item(i), Edge)
            Dim sp(2) As Double, ep(2) As Double
            ed.GetEndPoints(sp, ep)
            Dim mx As Double = (sp(0) + ep(0)) / 2.0
            Dim my As Double = (sp(1) + ep(1)) / 2.0
            If Math.Abs(mx - zielXm) < tolMeter AndAlso Math.Abs(my - zielYm) < tolMeter Then
                Return ed
            End If
        Next
        Return Nothing
    End Function

    ''' <summary>Erste Kante einer Flaeche (Orientierungskante fuer die Abwicklung).</summary>
    Public Function ErsteKante(f As Face) As Edge
        Dim es As Object = f.Edges
        If CInt(es.Count) < 1 Then Return Nothing
        Return CType(es.Item(1), Edge)
    End Function

End Module
